#!/usr/bin/env python3
"""Generate conformance vectors (IL-001 / IL-008 / IL-010).

Vectors lock the Python reference engine and the canonical C# library
together: every vector records config + command sequence + exact event log +
exact final state + state hash. The C# test suite must reproduce all of them
byte-identically. Trust chain: hand-written tests (independent ground truth)
validated the engine -> only then does it generate vectors.
"""
from __future__ import annotations

import io
import os
import sys

HERE = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, HERE)

from ludo import codec, engine, simulation  # noqa: E402
from ludo.model import (  # noqa: E402
    LeaveMatchCommand, MatchConfig, MoveTokenCommand, RollDiceCommand, Seat,
)

REPO = os.path.dirname(os.path.dirname(HERE))
OUT = os.path.join(REPO, "tests", "conformance-vectors")

RED, GREEN, YELLOW, BLUE = Seat.RED, Seat.GREEN, Seat.YELLOW, Seat.BLUE


def run(config: MatchConfig, commands):
    state = engine.new_match(config)
    events = []
    for cmd in commands:
        res = engine.apply(state, cmd)
        assert res.ok, "generator produced illegal command %r: %s" % (cmd, res.error)
        events.extend(res.events)
        state = res.new_state
    return state, events


def write_vector(name: str, config: MatchConfig, commands, state, events) -> None:
    lines = [
        "# DESI-LUDO-VECTOR v1",
        "name=" + name,
        "players=%d" % len(config.seats),
        "blockades=%d" % (1 if config.blockades else 0),
        "threesixes=%d" % (1 if config.three_sixes else 0),
        "capturex=%d" % (1 if config.capture_extra else 0),
        "placings=%d" % (1 if config.placings else 0),
        "twopend=%d" % (1 if config.two_player_end else 0),
    ]
    for c in commands:
        lines.append("cmd=" + c.line())
    for e in events:
        lines.append("event=" + e.line())
    for sl in codec.save_state(state).splitlines():
        lines.append("state=" + sl)
    lines.append("hash=" + state.state_hash())
    path = os.path.join(OUT, name + ".vec")
    io.open(path, "w", encoding="utf-8", newline="\n").write("\n".join(lines) + "\n")
    return path


def reverify(name: str, config: MatchConfig, commands, state, events) -> bool:
    """Self-consistency: replay the vector's commands, compare everything."""
    rstate, revents = simulation.replay(config, commands)
    ok = (rstate.state_hash() == state.state_hash()
          and codec.save_state(rstate) == codec.save_state(state)
          and [e.line() for e in revents] == [e.line() for e in events])
    if not ok:
        print("!! SELF-VERIFY FAILED: " + name)
    return ok


def main() -> int:
    os.makedirs(OUT, exist_ok=True)
    vectors = []

    def scenario(name, players, commands, **cfg_overrides):
        cfg = MatchConfig.default(players, **cfg_overrides)
        state, events = run(cfg, commands)
        write_vector(name, cfg, commands, state, events)
        assert reverify(name, cfg, commands, state, events)
        vectors.append((name, cfg, commands, state, events))

    # ---------------- hand-authored deterministic scenarios ----------------

    scenario("release_and_extra_roll", 4, [
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),
        RollDiceCommand(RED, 3), MoveTokenCommand(RED, 0),
    ])

    scenario("no_move_passes_turn", 4, [
        RollDiceCommand(RED, 3),
    ])

    scenario("turn_rotation_all_pass", 4, [
        RollDiceCommand(RED, 3), RollDiceCommand(GREEN, 3),
        RollDiceCommand(YELLOW, 3), RollDiceCommand(BLUE, 3),
    ])

    scenario("three_sixes_void_two_turns", 4, [
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),
        RollDiceCommand(RED, 6),                      # Red voids
        RollDiceCommand(GREEN, 6), MoveTokenCommand(GREEN, 0),
        RollDiceCommand(GREEN, 6), MoveTokenCommand(GREEN, 0),
        RollDiceCommand(GREEN, 6),                    # Green voids independently
    ])

    scenario("three_sixes_disabled", 4, [
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),   # no void
    ], three_sixes=False)

    scenario("leave_early_skips_seat", 4, [
        LeaveMatchCommand(GREEN),
        RollDiceCommand(RED, 3),                      # nomove -> next active = Yellow
    ])

    scenario("two_player_fragment", 2, [
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 0),
        RollDiceCommand(RED, 2), MoveTokenCommand(RED, 0),
        RollDiceCommand(YELLOW, 4),                   # all base: nomove -> back to Red
    ])

    scenario("multi_roll_turn", 4, [
        # 6 (extra roll), 6 (extra roll), then a non-six whose move ends the
        # turn: three rolls inside one turn, no void.
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 1),
        RollDiceCommand(RED, 6), MoveTokenCommand(RED, 2),
        RollDiceCommand(RED, 3), MoveTokenCommand(RED, 1),
    ])

    # ---------------- full random matches (cover captures/blockades/finishes) ----------------

    full_match_specs = [
        ("full_match_4p_seed1", MatchConfig.default(4), 1),
        ("full_match_4p_seed7", MatchConfig.default(4), 7),
        ("full_match_4p_blockades_off_seed500", MatchConfig.default(4, blockades=False), 500),
        ("full_match_2p_seed100", MatchConfig.default(2), 100),
        ("full_match_3p_seed900", MatchConfig.default(3), 900),
        ("full_match_4p_threesixes_off_seed1300", MatchConfig.default(4, three_sixes=False), 1300),
    ]
    for name, cfg, seed in full_match_specs:
        state, commands, events = simulation.play_random_match(cfg, seed)
        assert simulation.verify_replay(cfg, commands, state, events)
        write_vector(name, cfg, commands, state, events)
        assert reverify(name, cfg, commands, state, events)
        vectors.append((name, cfg, commands, state, events))

    # ---------------- coverage report ----------------

    all_lines = []
    for _, _, _, _, events in vectors:
        all_lines.extend(e.line() for e in events)
    stats = {
        "captures": sum(1 for l in all_lines if l.startswith("capture ")),
        "voided_turns": sum(1 for l in all_lines if l.startswith("voided ")),
        "finishes": sum(1 for l in all_lines if l.startswith("finish ")),
        "player_finishes": sum(1 for l in all_lines if l.startswith("playerfinish ")),
        "nomoves": sum(1 for l in all_lines if l.startswith("nomove ")),
        "matches_ended": sum(1 for l in all_lines if l.startswith("end ")),
    }
    print("vectors written: %d -> %s" % (len(vectors), OUT))
    print("coverage:", stats)
    assert stats["captures"] >= 10, "vector set should exercise captures heavily"
    assert stats["voided_turns"] >= 2, "vector set should exercise D2 voids"
    assert stats["player_finishes"] >= 12
    print("ALL VECTORS SELF-VERIFIED")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
