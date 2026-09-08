#!/usr/bin/env python3
"""DesiLudo3D — headless match runner CLI (M0 deliverable).

Runs deterministic random matches (seeded), prints results, and verifies that
replaying each command log reproduces the exact final state and event stream.

Usage:
  python3 tools/reference-engine/cli.py --players 4 --blockades on --seed 1 --games 25
"""
from __future__ import annotations

import argparse
import sys
from typing import List, Optional

import os
sys.path.insert(0, os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

from ludo import simulation  # noqa: E402
from ludo.model import MatchConfig  # noqa: E402


def main(argv: Optional[List[str]] = None) -> int:
    p = argparse.ArgumentParser(description="DesiLudo3D headless match runner")
    p.add_argument("--players", type=int, default=4, choices=[2, 3, 4])
    p.add_argument("--blockades", choices=["on", "off"], default="on")
    p.add_argument("--threesixes", choices=["on", "off"], default="on")
    p.add_argument("--capturex", choices=["on", "off"], default="on")
    p.add_argument("--seed", type=int, default=1)
    p.add_argument("--games", type=int, default=1)
    p.add_argument("--max-commands", type=int, default=100_000)
    p.add_argument("--quiet", action="store_true")
    args = p.parse_args(argv)

    cfg = MatchConfig.default(
        args.players,
        blockades=args.blockades == "on",
        three_sixes=args.threesixes == "on",
        capture_extra=args.capturex == "on",
    )

    failures = 0
    total_cmds = 0
    for g in range(args.games):
        seed = args.seed + g
        state, log, events = simulation.play_random_match(cfg, seed, args.max_commands)
        ok = simulation.verify_replay(cfg, log, state, events)
        total_cmds += len(log)
        if not args.quiet:
            standings = " ".join(st.line() for st in (state.final_standings or []))
            print("seed=%-6d commands=%-5d turns=%-4d replay_ok=%s hash=%s %s"
                  % (seed, len(log), state.turn_count, ok, state.state_hash(), standings))
        if not ok:
            failures += 1

    print("games=%d failures=%d avg_commands=%.1f"
          % (args.games, failures, total_cmds / max(1, args.games)))
    return 1 if failures else 0


if __name__ == "__main__":
    raise SystemExit(main())
