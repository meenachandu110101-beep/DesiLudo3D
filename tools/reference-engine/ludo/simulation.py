"""Deterministic random-match simulation (headless driver).

Used by: the M0 CLI runner, determinism/replay tests, conformance-vector
generation, and (later) fuzz-oracle + AI training-data generation.
"""
from __future__ import annotations

import random
from typing import Callable, List, Optional, Tuple

from ludo import codec
from ludo.engine import ApplyResult, LegalMove, apply, get_legal_moves, new_match
from ludo.model import Command, GameEvent, GameState, MatchConfig, MoveTokenCommand, RollDiceCommand, TurnPhase

Chooser = Callable[[random.Random, List[LegalMove], GameState], int]


def default_chooser(rng: random.Random, moves: List[LegalMove], state: GameState) -> int:
    return rng.randrange(len(moves))


def play_random_match(
    config: MatchConfig,
    seed: int,
    max_commands: int = 100_000,
    chooser: Optional[Chooser] = None,
) -> Tuple[GameState, List[Command], List[GameEvent]]:
    rng = random.Random(seed)
    choose = chooser or default_chooser
    state = new_match(config)
    log: List[Command] = []
    events: List[GameEvent] = []

    for _ in range(max_commands):
        if state.phase == TurnPhase.MATCH_OVER:
            return state, log, events
        if state.phase == TurnPhase.AWAIT_ROLL:
            cmd = RollDiceCommand(state.current_seat, rng.randint(1, 6))
        else:
            moves = get_legal_moves(state, state.current_seat, state.last_die)
            if not moves:
                raise RuntimeError("engine invariant broken: AwaitMove with no legal moves")
            cmd = MoveTokenCommand(state.current_seat, moves[choose(rng, moves, state)].token_index)
        res = apply(state, cmd)
        if not res.ok:
            raise RuntimeError("driver produced illegal command %r: %s" % (cmd, res.error))
        log.append(cmd)
        events.extend(res.events)
        state = res.new_state

    raise TimeoutError("match did not finish within %d commands" % max_commands)


def replay(config: MatchConfig, commands: List[Command]) -> Tuple[GameState, List[GameEvent]]:
    """Deterministically re-apply a command log from a fresh match."""
    state = new_match(config)
    events: List[GameEvent] = []
    for cmd in commands:
        res = apply(state, cmd)
        if not res.ok:
            raise RuntimeError("replay hit illegal command %r: %s" % (cmd, res.error))
        events.extend(res.events)
        state = res.new_state
    return state, events


def verify_replay(config: MatchConfig, commands: List[Command],
                  final_state: GameState, final_events: List[GameEvent]) -> bool:
    """True iff replaying the log reproduces the exact final state + events."""
    state, events = replay(config, commands)
    return state.state_hash() == final_state.state_hash() and events == final_events
