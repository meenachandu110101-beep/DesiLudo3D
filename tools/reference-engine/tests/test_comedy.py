# -*- coding: utf-8 -*-
"""Comedy engine tests (M3): selection algorithm + data integrity + event
mapping. These validate the Python reference; the C# Core comedy layer is a
structural mirror, and the pool DATA is generated from this module by
tools/generate_comedy_pools.py (hash-checked)."""

import sys
import os

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from ludo.comedy import (  # noqa: E402
    CATEGORIES, CONTEXTS, EMOTIONS, LINES, POOLS, ComedyDirector, Rng,
    reactions_for_event,
)
from ludo.model import (  # noqa: E402
    DiceRolled, MatchEnded, NoLegalMove, PlayerFinished, Seat, Standing,
    TokenCaptured, TokenFinished, TokenMoved, TurnEnded, TurnVoided,
)

SEATS = (Seat.RED, Seat.GREEN, Seat.YELLOW, Seat.BLUE)


# ---------------------------------------------------------------- data integrity

def test_every_context_category_has_lines():
    for ctx in CONTEXTS:
        for cat in CATEGORIES:
            assert len(POOLS[(ctx, cat)]) >= 2, (ctx, cat)


def test_line_ids_unique_and_wellformed():
    ids = [l.id for l in LINES]
    assert len(set(ids)) == len(ids)
    for l in LINES:
        assert l.id.startswith(l.context + "." + l.category + ".")
        assert l.en.strip() and l.hi.strip()
        assert isinstance(l.personalities, tuple)  # () = any personality
        assert set(l.personalities) <= {"balanced", "overconfident", "drama", "sweet", "excited"}


def test_no_cross_category_word_swap_lines():
    # A3: female/child pools are natively written — no line may appear in two
    # categories (which is what a mechanical "bhai->behen" swap would produce).
    en_by_cat = {}
    for l in LINES:
        en_by_cat.setdefault(l.category, set()).add(l.en.strip().lower())
    cats = sorted(en_by_cat)
    for i, a in enumerate(cats):
        for b in cats[i + 1:]:
            assert not (en_by_cat[a] & en_by_cat[b]), (a, b)


def test_emotions_cover_all_contexts():
    assert set(EMOTIONS) == set(CONTEXTS)
    for ctx in CONTEXTS:
        assert EMOTIONS[ctx] in {"happy", "excited", "laugh", "angry", "sad", "victory", "tired"}


# ---------------------------------------------------------------- rng golden anchors

def test_splitmix64_golden():
    # Golden anchors from the validated first run — these pin the EXACT
    # algorithm so the C# mirror can be checked against the same values.
    r = Rng(0)
    vals = [r.next() for _ in range(4)]
    r2 = Rng(1)
    vals2 = [r2.next() for _ in range(4)]
    assert vals == [
        0xE220A8397B1DCDAF, 0x6E789E6AA1B965F4, 0x6C45D188009454F, 0xF88BB8A8724C81EC,
    ]
    assert vals2 == [
        0x910A2DEC89025CC1, 0xBEEB8DA1658EEC67, 0xF893A2EEFB32555E, 0x71C18690EE42C90B,
    ]


# ---------------------------------------------------------------- selector

def test_category_routing():
    d = ComedyDirector(7)
    for i in range(30):
        line = d.select(0, "capture", "female", "balanced", 10000.0 * i)
        assert line is not None
        assert line.category == "female"


def test_determinism_same_seed_same_picks():
    def run():
        d = ComedyDirector(123)
        out = []
        now = 0.0
        for ctx in ("greeting", "six", "capture", "captured", "void", "victory"):
            line = d.select(2, ctx, "child", "excited", now)
            out.append(None if line is None else line.id)
            now += 100.0
        return out
    assert run() == run()


def test_no_immediate_repeat():
    # Semantics: never the same line twice in a row for a seat (pools >= 2).
    d = ComedyDirector(99)
    prev = None
    now = 0.0
    for _ in range(40):
        line = d.select(1, "six", "male", "balanced", now)
        now += 100.0
        assert line.id != prev
        prev = line.id


def test_cooldown_blocks_then_allows():
    d = ComedyDirector(5)
    assert d.select(0, "capture", "male", "balanced", 10.0) is not None
    assert d.select(0, "capture", "male", "balanced", 12.0) is None   # blocked
    assert d.select(0, "capture", "male", "balanced", 30.0) is not None  # allowed


def test_cooldown_exempt_contexts():
    d = ComedyDirector(6)
    assert d.select(0, "greeting", "male", "balanced", 10.0) is not None
    assert d.select(0, "greeting", "male", "balanced", 11.0) is not None
    assert d.select(0, "victory", "male", "balanced", 12.0) is not None
    assert d.select(0, "victory", "male", "balanced", 12.5) is not None


def test_personality_bias():
    # The 75% preferred-pool rule must ENRICH personality-tagged lines for
    # that personality (~46% here vs ~16% for a balanced seat on the same
    # small pool — uniform baseline would be 33%). Absolute ratios are pool
    # size dependent; the enrichment ratio is the invariant.
    def tagged_ratio(personality):
        d = ComedyDirector(11)
        hits = 0
        draws = 0
        now = 0.0
        for _ in range(600):
            line = d.select(3, "capture", "male", personality, now)
            now += 100.0
            draws += 1
            if "overconfident" in line.personalities:
                hits += 1
        return hits / draws

    oc = tagged_ratio("overconfident")
    ba = tagged_ratio("balanced")
    assert oc > 0.38, oc
    assert ba < 0.30, ba
    assert oc / ba > 1.5, (oc, ba)


def test_unknown_context_returns_none():
    d = ComedyDirector(3)
    assert d.select(0, "party", "male", "balanced", 0.0) is None


# ---------------------------------------------------------------- event mapping

def test_events_dice():
    assert reactions_for_event(DiceRolled(Seat.RED, 6, 1, False), SEATS) == [
        (0, "six", "excited", -1),
    ][0:0] or True  # Reaction is not a tuple; compare fields instead:
    r = reactions_for_event(DiceRolled(Seat.RED, 6, 1, False), SEATS)
    assert len(r) == 1 and r[0].context == "six" and r[0].seat_index == 0
    r = reactions_for_event(DiceRolled(Seat.RED, 3, 2, True), SEATS)
    assert len(r) == 1 and r[0].context == "extra"
    r = reactions_for_event(DiceRolled(Seat.RED, 3, 1, False), SEATS)
    assert r == []


def test_events_nomove_release_move():
    r = reactions_for_event(NoLegalMove(Seat.YELLOW, 4), SEATS)
    assert len(r) == 1 and r[0].context == "nomove" and r[0].seat_index == 2
    r = reactions_for_event(TokenMoved(Seat.GREEN, 1, -1, 0, True), SEATS)
    assert len(r) == 1 and r[0].context == "release" and r[0].token_index == 1
    r = reactions_for_event(TokenMoved(Seat.GREEN, 1, 0, 3, False), SEATS)
    assert r == []


def test_events_capture_two_perspectives():
    r = reactions_for_event(
        TokenCaptured(Seat.BLUE, 2, Seat.RED, 0, 14), SEATS)
    assert len(r) == 2
    att, vic = r
    assert att.seat_index == 0 and att.context == "capture" and att.emotion == "laugh"
    assert vic.seat_index == 3 and vic.context == "captured" and vic.emotion == "angry"


def test_events_finish_place_void():
    r = reactions_for_event(TokenFinished(Seat.RED, 2), SEATS)
    assert r[0].context == "finish"
    r = reactions_for_event(PlayerFinished(Seat.GREEN, 2), SEATS)
    assert r[0].context == "place"
    r = reactions_for_event(PlayerFinished(Seat.GREEN, 1), SEATS)
    assert r[0].context == "victory"
    r = reactions_for_event(TurnVoided(Seat.RED, (1, 2), ()), SEATS)
    assert len(r) == 1 and r[0].context == "void"


def test_events_match_end_everyone_reacts():
    standings = (
        Standing(Seat.YELLOW, 1, 4),
        Standing(Seat.RED, 2, 3),
        Standing(Seat.GREEN, 3, 1),
        Standing(Seat.BLUE, 4, 0),
    )
    r = reactions_for_event(MatchEnded(standings), SEATS)
    ctx = {x.seat_index: x.context for x in r}
    assert ctx == {2: "victory", 0: "defeat", 1: "defeat", 3: "defeat"}


def test_events_silent():
    assert reactions_for_event(TurnEnded(Seat.GREEN, 5), SEATS) == []
