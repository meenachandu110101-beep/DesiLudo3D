"""DesiLudo3D — reference rules engine (pure functions, deterministic).

Apply contract: `apply(state, command)` NEVER mutates the input state; it
returns ApplyResult(new_state, events, error). Replaying the same command
sequence on the same initial state always yields identical states/events
(verified by the conformance vectors and the determinism tests).
"""
from __future__ import annotations

from typing import Dict, List, NamedTuple, Optional, Sequence, Set, Tuple

from ludo.model import (
    Command, GameEvent, GameState, LeaveMatchCommand, MatchConfig,
    MatchEnded, MoveTokenCommand, NoLegalMove, PlayerFinished, PlayerLeft,
    PROG_BASE, PROG_COLUMN_FIRST, PROG_FINISHED, PROG_LOOP_MAX, RollDiceCommand,
    Seat, Standing, TOKENS_PER_SEAT, TokenCaptured, TokenFinished, TokenMoved,
    TurnEnded, TurnPhase, TurnVoided, DiceRolled, global_cell, is_safe_cell,
    parse_command, start_cell,
)

# ------------------------------------------------------------------ public types


class LegalMove(NamedTuple):
    seat: Seat
    token_index: int
    frm: int
    to: int
    is_release: bool
    captures: bool
    victims: Tuple[Tuple[Seat, int], ...]


class ApplyResult(NamedTuple):
    new_state: Optional[GameState]
    events: Tuple[GameEvent, ...]
    error: Optional[str]

    @property
    def ok(self) -> bool:
        return self.error is None


# ------------------------------------------------------------------ helpers


def _loop_tokens_by_cell(state: GameState) -> Dict[int, Dict[Seat, List[int]]]:
    occ: Dict[int, Dict[Seat, List[int]]] = {}
    for seat in state.config.seats:
        for idx in range(TOKENS_PER_SEAT):
            p = state.token(seat, idx).progress
            if 0 <= p <= PROG_LOOP_MAX:
                cell = global_cell(seat, p)
                occ.setdefault(cell, {}).setdefault(seat, []).append(idx)
    return occ


def _blockaded_cells_for(state: GameState, mover: Seat) -> Set[int]:
    """Cells where another seat has >= 2 tokens (IL-002: any loop cell,
    safe cells included; owner is never blocked by own stacks)."""
    out: Set[int] = set()
    for cell, by_seat in _loop_tokens_by_cell(state).items():
        for seat, idxs in by_seat.items():
            if seat != mover and len(idxs) >= 2:
                out.add(cell)
    return out


def _should_end(state: GameState) -> bool:
    if state.phase == TurnPhase.MATCH_OVER:
        return True
    cfg = state.config
    if state.finished_order:
        if not cfg.placings:
            return True
        if len(cfg.seats) == 2 and cfg.two_player_end:
            return True
    return state.active_count() <= 1


def _end_turn(state: GameState, events: List[GameEvent]) -> None:
    nxt = state.next_active_seat()
    if nxt is None:                      # defensive; callers check _should_end
        _end_match(state, events)
        return
    state.turn_count += 1
    state.current_seat = nxt
    state.phase = TurnPhase.AWAIT_ROLL
    state.last_die = 0
    state.consec_sixes = 0
    state.rolls_this_turn = 0
    state.clear_snapshot()
    events.append(TurnEnded(nxt, state.turn_count))


def _end_match(state: GameState, events: List[GameEvent]) -> None:
    standings: List[Standing] = []
    place = 1
    for seat in state.finished_order:            # finishers in finish order
        standings.append(Standing(seat, place, TOKENS_PER_SEAT))
        place += 1
    remaining = [s for s in state.config.seats if s not in state.finished_order]
    # IL-011: tokens-home desc, then non-leavers above leavers, then seat order.
    remaining.sort(key=lambda s: (-state.tokens_home(s), state.is_left(s),
                                  state.config.seats.index(s)))
    for s in remaining:
        standings.append(Standing(s, place, state.tokens_home(s)))
        place += 1
    state.final_standings = standings
    state.phase = TurnPhase.MATCH_OVER
    state.last_die = 0
    state.consec_sixes = 0
    state.rolls_this_turn = 0
    state.clear_snapshot()
    events.append(MatchEnded(tuple(standings)))


# ------------------------------------------------------------------ public API


def new_match(config: MatchConfig) -> GameState:
    return GameState(config)


def get_legal_moves(state: GameState, seat: Seat, die: int) -> List[LegalMove]:
    """All legal moves for (seat, die) given the current board. Pure."""
    if state.phase == TurnPhase.MATCH_OVER:
        return []
    if not 1 <= die <= 6:
        return []
    cfg = state.config
    blockaded = _blockaded_cells_for(state, seat) if cfg.blockades else frozenset()
    occ = _loop_tokens_by_cell(state)
    moves: List[LegalMove] = []

    for idx in range(TOKENS_PER_SEAT):
        p = state.token(seat, idx).progress
        if p == PROG_FINISHED:
            continue
        if p == PROG_BASE:
            if die == 6:
                cell = start_cell(seat)
                if cfg.blockades and cell in blockaded:
                    continue                # release blocked by opponent blockade
                moves.append(LegalMove(seat, idx, PROG_BASE, 0, True, False, ()))
            continue

        to = p + die
        if to > PROG_FINISHED:
            continue                        # overshoot of finish is illegal

        if cfg.blockades and p <= PROG_LOOP_MAX:
            hi = min(to, PROG_LOOP_MAX)
            if any(global_cell(seat, pr) in blockaded for pr in range(p + 1, hi + 1)):
                continue                    # path or destination blockaded

        captures = False
        victims: Tuple[Tuple[Seat, int], ...] = ()
        if to <= PROG_LOOP_MAX:
            cell = global_cell(seat, to)
            if not is_safe_cell(cell):
                victims = tuple(
                    (s, i)
                    for s, idxs in occ.get(cell, {}).items() if s != seat
                    for i in idxs
                )
                captures = bool(victims)

        moves.append(LegalMove(seat, idx, p, to, False, captures, victims))
    return moves


def apply(state: GameState, command: Command) -> ApplyResult:
    if state is None:
        return ApplyResult(None, (), "state is null")
    if command is None:
        return ApplyResult(None, (), "command is null")
    if isinstance(command, RollDiceCommand):
        return _apply_roll(state, command)
    if isinstance(command, MoveTokenCommand):
        return _apply_move(state, command)
    if isinstance(command, LeaveMatchCommand):
        return _apply_leave(state, command)
    return ApplyResult(None, (), "unknown command type: %r" % type(command).__name__)


# ------------------------------------------------------------------ roll


def _apply_roll(state: GameState, cmd: RollDiceCommand) -> ApplyResult:
    if state.phase == TurnPhase.MATCH_OVER:
        return ApplyResult(None, (), "match is over")
    if state.phase != TurnPhase.AWAIT_ROLL:
        return ApplyResult(None, (), "a move is pending; move before rolling")
    if cmd.seat != state.current_seat:
        return ApplyResult(None, (), "not %s's turn (current: %s)" % (cmd.seat, state.current_seat))
    if not 1 <= cmd.die <= 6:
        return ApplyResult(None, (), "die must be 1..6")

    s = state.clone()
    events: List[GameEvent] = []

    if s.rolls_this_turn == 0:
        s.take_snapshot()                       # IL-003: turn-start snapshot
    s.rolls_this_turn += 1
    if cmd.die == 6:
        s.consec_sixes += 1
    else:
        s.consec_sixes = 0
    events.append(DiceRolled(cmd.seat, cmd.die, s.rolls_this_turn, s.rolls_this_turn > 1))

    # D2: third consecutive six voids the entire turn (IL-003).
    if cmd.die == 6 and s.consec_sixes >= 3 and s.config.three_sixes:
        s.restore_snapshot()
        s.consec_sixes = 0
        s.rolls_this_turn = 0
        s.last_die = 0
        events.append(TurnVoided(cmd.seat, tuple(s.flat_progress()), tuple(s.finished_order)))
        _end_turn(s, events)
        return ApplyResult(s, tuple(events), None)

    s.last_die = cmd.die
    moves = get_legal_moves(s, cmd.seat, cmd.die)
    if not moves:
        events.append(NoLegalMove(cmd.seat, cmd.die))
        if cmd.die == 6:
            # IL-004: a 6 always earns its extra roll, even with no legal move.
            s.phase = TurnPhase.AWAIT_ROLL
            return ApplyResult(s, tuple(events), None)
        _end_turn(s, events)
        return ApplyResult(s, tuple(events), None)

    s.phase = TurnPhase.AWAIT_MOVE
    return ApplyResult(s, tuple(events), None)


# ------------------------------------------------------------------ move


def _apply_move(state: GameState, cmd: MoveTokenCommand) -> ApplyResult:
    if state.phase == TurnPhase.MATCH_OVER:
        return ApplyResult(None, (), "match is over")
    if state.phase != TurnPhase.AWAIT_MOVE:
        return ApplyResult(None, (), "no move pending; roll first")
    if cmd.seat != state.current_seat:
        return ApplyResult(None, (), "not %s's turn (current: %s)" % (cmd.seat, state.current_seat))
    if not 0 <= cmd.token_index < TOKENS_PER_SEAT:
        return ApplyResult(None, (), "token index out of range")

    moves = get_legal_moves(state, cmd.seat, state.last_die)
    sel = next((m for m in moves if m.token_index == cmd.token_index), None)
    if sel is None:
        return ApplyResult(None, (), "no legal move for token %d with die %d" %
                           (cmd.token_index, state.last_die))

    s = state.clone()
    events: List[GameEvent] = []

    tok = s.token(cmd.seat, cmd.token_index)
    tok.progress = sel.to
    events.append(TokenMoved(cmd.seat, cmd.token_index, sel.frm, sel.to, sel.is_release))

    cell = global_cell(cmd.seat, sel.to)
    for vs, vi in sel.victims:
        s.token(vs, vi).progress = PROG_BASE
        events.append(TokenCaptured(vs, vi, cmd.seat, cmd.token_index, cell))
    captured = bool(sel.victims)

    if sel.to == PROG_FINISHED:
        events.append(TokenFinished(cmd.seat, cmd.token_index))
        if s.tokens_home(cmd.seat) == TOKENS_PER_SEAT:
            place = len(s.finished_order) + 1
            s.finished_order.append(cmd.seat)
            events.append(PlayerFinished(cmd.seat, place))

    if _should_end(s):
        _end_match(s, events)
        return ApplyResult(s, tuple(events), None)

    # Extra roll: a 6, or a capture when D3 is on (IL-005: never for finishing).
    # A player who just finished their last token never rolls again.
    extra = (not s.is_finished(cmd.seat)) and (
        state.last_die == 6 or (captured and s.config.capture_extra))
    s.last_die = 0
    s.phase = TurnPhase.AWAIT_ROLL
    if not extra:
        _end_turn(s, events)
    return ApplyResult(s, tuple(events), None)


# ------------------------------------------------------------------ leave


def _apply_leave(state: GameState, cmd: LeaveMatchCommand) -> ApplyResult:
    if state.phase == TurnPhase.MATCH_OVER:
        return ApplyResult(None, (), "match is over")
    if cmd.seat not in state.config.seats:
        return ApplyResult(None, (), "seat %s is not in this match" % cmd.seat)
    if state.is_left(cmd.seat):
        return ApplyResult(None, (), "%s has already left" % cmd.seat)

    s = state.clone()
    events: List[GameEvent] = []
    s.left.add(cmd.seat)
    events.append(PlayerLeft(cmd.seat))

    if _should_end(s):
        _end_match(s, events)
        return ApplyResult(s, tuple(events), None)
    if cmd.seat == s.current_seat:
        _end_turn(s, events)
    return ApplyResult(s, tuple(events), None)
