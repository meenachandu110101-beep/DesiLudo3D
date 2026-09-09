# -*- coding: utf-8 -*-
"""Bot tier tests (M4): heuristic preferences, danger awareness, tier
strength ordering, determinism. The C# Core HeuristicAgent mirrors this."""

import os
import sys

sys.path.insert(0, os.path.join(os.path.dirname(__file__), ".."))

from ludo.agent import (  # noqa: E402
    BotTier, base_score, full_score, make_chooser, threat_count,
)
from ludo.engine import get_legal_moves  # noqa: E402
from ludo.model import (  # noqa: E402
    LOOP_CELLS, PROG_FINISHED, PROG_LOOP_MAX, Seat, TokenState,
    global_cell, is_safe_cell,
)
from ludo.simulation import play_random_match  # noqa: E402
from ludo.model import MatchConfig, seats_for_player_count  # noqa: E402


def make_state(players=4, placements=None, current=Seat.RED, last_die=3):
    """placements: {(seat, index): progress}. Fresh state, tokens placed.
    Default 4 players so GREEN (the usual opponent in these setups) is in
    play — 2-player configs only have RED + YELLOW."""
    from ludo.model import GameState, TurnPhase
    cfg = MatchConfig(seats_for_player_count(players))
    state = GameState(cfg)
    for (seat, idx), prog in (placements or {}).items():
        state.tokens[int(seat) * 4 + idx] = TokenState(seat, idx, prog)
    state.current_seat = current
    state.phase = TurnPhase.AWAIT_MOVE
    state.last_die = last_die
    return state


# ---------------------------------------------------------------- unit: threats

def test_threat_count_detects_range_1_to_6():
    # red cell 10 is reachable by green within 1..6 for exactly one placement
    red_cell = global_cell(Seat.RED, 10)
    green_progress = None
    for p in range(0, PROG_LOOP_MAX + 1):
        if (red_cell - global_cell(Seat.GREEN, p)) % LOOP_CELLS in (1, 2, 3, 4, 5, 6):
            green_progress = p
            break
    assert green_progress is not None
    state = make_state(placements={(Seat.GREEN, 0): green_progress})
    assert threat_count(state, Seat.RED, red_cell) == 1
    assert threat_count(state, Seat.RED, (red_cell + 8) % LOOP_CELLS) == 0


# ---------------------------------------------------------------- unit: choices

def test_medium_prefers_capture():
    # red0 at progress 20 (cell 20); die 3 -> cell 23 where green sits
    # (green start 13 + progress 10 = 23; not a safe cell).
    # red1 at progress 30 also movable (die 3 -> cell 33) so there IS a choice.
    state = make_state(
        placements={(Seat.RED, 0): 20, (Seat.RED, 1): 30, (Seat.GREEN, 0): 10},
        last_die=3)
    moves = get_legal_moves(state, Seat.RED, 3)
    assert any(m.captures for m in moves)
    assert len(moves) >= 2

    def capture_rate(tier, n=80):
        hits = 0
        for seed in range(n):
            c = make_chooser(tier, seed=seed)
            if moves[c(None, moves, state)].captures:
                hits += 1
        return hits / n

    # MEDIUM: 75% greedy + 25% random(1/2) -> ~87% expected; allow variance
    assert capture_rate(BotTier.MEDIUM) >= 0.75
    # HARD: 95% greedy + 5% random -> ~97.5% expected
    assert capture_rate(BotTier.HARD) >= 0.90


def test_hard_prefers_finish():
    state = make_state(
        placements={(Seat.RED, 0): 52, (Seat.RED, 1): 10}, last_die=4)
    moves = get_legal_moves(state, Seat.RED, 4)
    assert any(m.to == PROG_FINISHED for m in moves)
    assert len(moves) >= 2
    assert any(m.to == PROG_FINISHED for m in moves)
    choose = make_chooser(BotTier.HARD, seed=3)
    assert moves[choose(None, moves, state)].to == PROG_FINISHED


def test_medium_prefers_release_on_six():
    state = make_state(
        placements={(Seat.RED, 0): 10, (Seat.GREEN, 0): 30}, last_die=6)
    moves = get_legal_moves(state, Seat.RED, 6)
    assert any(m.is_release for m in moves)
    choose = make_chooser(BotTier.MEDIUM, seed=5)
    assert moves[choose(None, moves, state)].is_release


def test_hard_avoids_dangerous_landing():
    # red has two 6-releasable tokens? Simpler: two plain advances — one lands
    # next to a green token (capturable), one lands on a safe star cell.
    # Build: red 0 at progress 1 (cell 1, not safe), red 1 at progress 10.
    # With die 2: red0 -> cell 3, red1 -> cell 12. Place green so cell 3 is
    # threatened and 12 is a safe star (12 is not a star — SAFE = {0,8,13,...}).
    # Use die 7-like paths instead: die=2, red1 at 6 -> cell 8 (SAFE star). red0 at 1 -> cell 3.
    green_prog = None
    for p in range(0, PROG_LOOP_MAX + 1):
        if (3 - global_cell(Seat.GREEN, p)) % LOOP_CELLS in (1, 2, 3, 4, 5, 6):
            green_prog = p
            break
    assert green_prog is not None
    state = make_state(
        placements={(Seat.RED, 0): 1, (Seat.RED, 1): 6, (Seat.GREEN, 0): green_prog},
        last_die=2)
    moves = get_legal_moves(state, Seat.RED, 2)
    to3 = [m for m in moves if m.token_index == 0]
    to8 = [m for m in moves if m.token_index == 1]
    assert to3 and to8
    assert is_safe_cell(8) and not is_safe_cell(3)
    assert threat_count(state, Seat.RED, 3) >= 1
    choose = make_chooser(BotTier.HARD, seed=7)
    assert moves[choose(None, moves, state)].token_index == 1   # safe star


def test_medium_ignores_danger_hard_does_not():
    # same setup as above: MEDIUM (no threat terms) may pick the advanced
    # token; verify the score functions diverge as designed.
    green_prog = None
    for p in range(0, PROG_LOOP_MAX + 1):
        if (3 - global_cell(Seat.GREEN, p)) % LOOP_CELLS in (1, 2, 3, 4, 5, 6):
            green_prog = p
            break
    assert green_prog is not None
    state = make_state(
        placements={(Seat.RED, 0): 1, (Seat.RED, 1): 6, (Seat.GREEN, 0): green_prog},
        last_die=2)
    moves = get_legal_moves(state, Seat.RED, 2)
    m0 = next(m for m in moves if m.token_index == 0)   # -> cell 3 (danger)
    m1 = next(m for m in moves if m.token_index == 1)   # -> cell 8 (safe)
    assert full_score(state, Seat.RED, m1) > full_score(state, Seat.RED, m0)
    # base (MEDIUM) scoring prefers the farther token m0 (to=3 vs to=8? no:
    # m1.to=8 > m0.to=3, so base also prefers m1). Use a reversed setup:
    # red0 at 10 (-> 12, dangerous), red1 at 1 (-> 3 safe star? 3 not safe).
    # Simpler invariant: full_score(m0) < base_score(m0) when landing threatened.
    assert full_score(state, Seat.RED, m0) < base_score(state, Seat.RED, m0)


# ---------------------------------------------------------------- integration

def test_hard_beats_easy_over_many_matches():
    wins = 0
    matches = 120
    for seed in range(matches):
        cfg = MatchConfig(seats_for_player_count(2))
        chooser_hard = make_chooser(BotTier.HARD, seed=seed)
        chooser_easy = make_chooser(BotTier.EASY, seed=seed)

        def chooser(rng, moves, state):
            if state.current_seat == Seat.RED:
                return chooser_hard(rng, moves, state)
            return chooser_easy(rng, moves, state)

        state, _, _ = play_random_match(cfg, seed, chooser=chooser)
        winner = state.final_standings[0].seat
        if winner == Seat.RED:
            wins += 1
    assert wins >= int(matches * 0.60), f"HARD won only {wins}/{matches}"


def test_agent_determinism():
    cfg = MatchConfig(seats_for_player_count(2))
    hashes = []
    for _ in range(2):
        ch = make_chooser(BotTier.HARD, seed=42)
        state, _, _ = play_random_match(cfg, 42, chooser=ch)
        hashes.append(state.state_hash() if hasattr(state, "state_hash") else str(state.flat_progress()))
    assert hashes[0] == hashes[1]
