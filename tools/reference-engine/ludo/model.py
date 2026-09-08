"""DesiLudo3D — reference rules engine data model (pure stdlib).

This module mirrors the canonical C# library `src/DesiLudo3D.Rules` exactly.
Both implementations are locked together by the conformance vectors in
`tests/conformance-vectors/` (see docs/IMPLEMENTATION-LOG.md IL-001/IL-008).

Semantics authorities:
  - docs/MASTER-SPEC.md §4 (locked decisions D2-D5)
  - docs/RULES-FORMAT.md (text codecs)
  - docs/IMPLEMENTATION-LOG.md (micro-decisions IL-002..IL-012)
"""
from __future__ import annotations

import enum
from dataclasses import dataclass, field
from typing import Dict, List, Optional, Sequence, Set, Tuple

# ------------------------------------------------------------------ board constants

LOOP_CELLS = 52
TOKENS_PER_SEAT = 4

PROG_BASE = -1            # token in base (yard)
PROG_LOOP_MAX = 50        # progress 0..50 on the shared loop
PROG_COLUMN_FIRST = 51    # 51..55 = private home column cells
PROG_FINISHED = 56        # centre (all 4 tokens here = player finished)


class Seat(enum.IntEnum):
    RED = 0
    GREEN = 1
    YELLOW = 2
    BLUE = 3

    def __str__(self) -> str:  # "Red" (serialization form)
        return self.name.capitalize()

    @staticmethod
    def parse(text: str) -> "Seat":
        try:
            return Seat[text.strip().upper()]
        except KeyError:
            raise ValueError("unknown seat: %r" % text)


def start_cell(seat: Seat) -> int:
    """Global loop cell of a seat's start square."""
    return (int(seat) * 13) % LOOP_CELLS


def global_cell(seat: Seat, progress: int) -> int:
    """Global loop cell for a seat-relative progress (0..50); -1 otherwise."""
    if 0 <= progress <= PROG_LOOP_MAX:
        return (start_cell(seat) + progress) % LOOP_CELLS
    return -1


def _build_safe_cells() -> frozenset:
    cells = set()
    for s in Seat:
        cells.add(start_cell(s))                    # 4 start squares
        cells.add((start_cell(s) + 8) % LOOP_CELLS)  # 4 star squares
    return frozenset(cells)


SAFE_CELLS = _build_safe_cells()  # {0, 8, 13, 21, 26, 34, 39, 47}


def is_safe_cell(cell: int) -> bool:
    return cell in SAFE_CELLS


def seats_for_player_count(players: int) -> Tuple[Seat, ...]:
    if players == 2:
        return (Seat.RED, Seat.YELLOW)          # opposite corners
    if players == 3:
        return (Seat.RED, Seat.GREEN, Seat.YELLOW)
    if players == 4:
        return (Seat.RED, Seat.GREEN, Seat.YELLOW, Seat.BLUE)
    raise ValueError("players must be 2, 3 or 4")


# ------------------------------------------------------------------ match config

@dataclass(frozen=True)
class MatchConfig:
    seats: Tuple[Seat, ...]
    blockades: bool = True        # D4: per-match toggle, default ON
    three_sixes: bool = True      # D2: third consecutive six voids the turn
    capture_extra: bool = True    # D3: capture grants an extra roll
    placings: bool = True         # D5: continue for placings after first winner
    two_player_end: bool = True   # D5 corollary: 2-player matches end at first winner

    def __post_init__(self):
        if not 2 <= len(self.seats) <= 4:
            raise ValueError("2..4 seats required")
        if len(set(self.seats)) != len(self.seats):
            raise ValueError("seats must be distinct")
        if self.placings is False and len(self.seats) == 2 and self.two_player_end:
            pass  # both settings agree for 2p anyway

    @staticmethod
    def default(players: int, **overrides) -> "MatchConfig":
        kw = dict(seats=seats_for_player_count(players))
        kw.update(overrides)
        return MatchConfig(**kw)


# ------------------------------------------------------------------ turn phase

class TurnPhase(enum.Enum):
    AWAIT_ROLL = "AwaitRoll"
    AWAIT_MOVE = "AwaitMove"
    MATCH_OVER = "MatchOver"

    @staticmethod
    def parse(text: str) -> "TurnPhase":
        for ph in TurnPhase:
            if ph.value == text.strip():
                return ph
        raise ValueError("unknown phase: %r" % text)


# ------------------------------------------------------------------ tokens / state

@dataclass
class TokenState:
    seat: Seat
    index: int
    progress: int = PROG_BASE

    def clone(self) -> "TokenState":
        return TokenState(self.seat, self.index, self.progress)


@dataclass(frozen=True)
class Standing:
    seat: Seat
    place: int
    tokens_home: int

    def line(self) -> str:
        return "%s:%d:%d" % (self.seat, self.place, self.tokens_home)

    @staticmethod
    def parse(text: str) -> "Standing":
        seat_s, place_s, home_s = text.split(":")
        return Standing(Seat.parse(seat_s), int(place_s), int(home_s))


class GameState:
    """Complete logical match state. Treated as immutable by the engine
    (apply() clones before mutation). The turn-start snapshot is part of the
    logical state (IL-009): a resumed mid-turn match must still void correctly
    on a third consecutive six."""

    __slots__ = (
        "config", "tokens", "current_seat", "phase", "last_die",
        "consec_sixes", "rolls_this_turn", "turn_count",
        "finished_order", "left", "final_standings",
        "snap_progress", "snap_finished",
    )

    def __init__(self, config: MatchConfig):
        self.config = config
        self.tokens: List[TokenState] = [
            TokenState(seat, i)
            for seat in Seat                      # all 16, flat seat-major
            for i in range(TOKENS_PER_SEAT)
        ]
        self.current_seat: Seat = config.seats[0]
        self.phase = TurnPhase.AWAIT_ROLL
        self.last_die = 0
        self.consec_sixes = 0
        self.rolls_this_turn = 0
        self.turn_count = 0
        self.finished_order: List[Seat] = []
        self.left: Set[Seat] = set()
        self.final_standings: Optional[List[Standing]] = None
        self.snap_progress: Optional[Tuple[int, ...]] = None
        self.snap_finished: Optional[Tuple[Seat, ...]] = None

    # -------------------------------------------------- accessors

    def token(self, seat: Seat, index: int) -> TokenState:
        return self.tokens[int(seat) * TOKENS_PER_SEAT + index]

    def tokens_home(self, seat: Seat) -> int:
        return sum(1 for i in range(TOKENS_PER_SEAT)
                   if self.token(seat, i).progress == PROG_FINISHED)

    def is_finished(self, seat: Seat) -> bool:
        return seat in self.finished_order

    def is_left(self, seat: Seat) -> bool:
        return seat in self.left

    def is_active(self, seat: Seat) -> bool:
        return seat in self.config.seats and not self.is_finished(seat) and not self.is_left(seat)

    def active_count(self) -> int:
        return sum(1 for s in self.config.seats if self.is_active(s))

    def next_active_seat(self) -> Optional[Seat]:
        order = self.config.seats
        cur = order.index(self.current_seat)
        for step in range(1, len(order) + 1):
            cand = order[(cur + step) % len(order)]
            if self.is_active(cand):
                return cand
        return None

    def flat_progress(self) -> List[int]:
        return [t.progress for t in self.tokens]

    # -------------------------------------------------- snapshot (D2 / IL-003)

    def take_snapshot(self) -> None:
        self.snap_progress = tuple(t.progress for t in self.tokens)
        self.snap_finished = tuple(self.finished_order)

    def restore_snapshot(self) -> None:
        if self.snap_progress is None:
            raise RuntimeError("no turn snapshot to restore")
        for t, p in zip(self.tokens, self.snap_progress):
            t.progress = p
        self.finished_order = list(self.snap_finished or ())

    def clear_snapshot(self) -> None:
        self.snap_progress = None
        self.snap_finished = None

    # -------------------------------------------------- clone / hash

    def clone(self) -> "GameState":
        s = GameState.__new__(GameState)
        s.config = self.config
        s.tokens = [t.clone() for t in self.tokens]
        s.current_seat = self.current_seat
        s.phase = self.phase
        s.last_die = self.last_die
        s.consec_sixes = self.consec_sixes
        s.rolls_this_turn = self.rolls_this_turn
        s.turn_count = self.turn_count
        s.finished_order = list(self.finished_order)
        s.left = set(self.left)
        s.final_standings = list(self.final_standings) if self.final_standings else None
        s.snap_progress = self.snap_progress          # immutable tuples: share
        s.snap_finished = self.snap_finished
        return s

    def _canonical(self) -> str:
        flags = "".join("1" if x else "0" for x in (
            self.config.blockades, self.config.three_sixes,
            self.config.capture_extra, self.config.placings,
            self.config.two_player_end))
        return "\n".join([
            "v1",
            ",".join(str(s) for s in self.config.seats),
            flags,
            str(self.current_seat),
            self.phase.value,
            str(self.last_die),
            str(self.consec_sixes),
            str(self.rolls_this_turn),
            str(self.turn_count),
            ",".join(str(s) for s in self.finished_order),
            ",".join(str(s) for s in sorted(self.left)),
            ",".join(str(p) for p in self.flat_progress()),
            ",".join(str(p) for p in self.snap_progress) if self.snap_progress else "-",
            ",".join(str(s) for s in self.snap_finished) if self.snap_finished else "-",
        ])

    def state_hash(self) -> str:
        """FNV-1a 32-bit over canonical state (includes snapshot — IL-009)."""
        h = 0x811C9DC5
        for byte in self._canonical().encode("utf-8"):
            h ^= byte
            h = (h * 0x01000193) & 0xFFFFFFFF
        return "%08x" % h


# ------------------------------------------------------------------ commands

class Command:
    def line(self) -> str:
        raise NotImplementedError

    def __repr__(self) -> str:
        return self.line()


class RollDiceCommand(Command):
    def __init__(self, seat: Seat, die: int):
        self.seat = seat
        self.die = die

    def line(self) -> str:
        return "roll %s %d" % (self.seat, self.die)


class MoveTokenCommand(Command):
    def __init__(self, seat: Seat, token_index: int):
        self.seat = seat
        self.token_index = token_index

    def line(self) -> str:
        return "move %s %d" % (self.seat, self.token_index)


class LeaveMatchCommand(Command):
    def __init__(self, seat: Seat):
        self.seat = seat

    def line(self) -> str:
        return "leave %s" % self.seat


def parse_command(line: str) -> Command:
    parts = line.split()
    if len(parts) >= 3 and parts[0] == "roll":
        return RollDiceCommand(Seat.parse(parts[1]), int(parts[2]))
    if len(parts) >= 3 and parts[0] == "move":
        return MoveTokenCommand(Seat.parse(parts[1]), int(parts[2]))
    if len(parts) >= 2 and parts[0] == "leave":
        return LeaveMatchCommand(Seat.parse(parts[1]))
    raise ValueError("bad command line: %r" % line)


# ------------------------------------------------------------------ events

class GameEvent:
    def line(self) -> str:
        raise NotImplementedError

    def __repr__(self) -> str:
        return self.line()

    def __eq__(self, other):
        return type(other) is type(self) and self.line() == other.line()

    def __hash__(self):
        return hash((type(self).__name__, self.line()))


@dataclass(frozen=True)
class DiceRolled(GameEvent):
    seat: Seat
    die: int
    roll_index: int      # 1-based within the current turn
    was_extra: bool

    def line(self) -> str:
        return "dice %s %d %d %d" % (self.seat, self.die, self.roll_index, 1 if self.was_extra else 0)


@dataclass(frozen=True)
class NoLegalMove(GameEvent):
    seat: Seat
    die: int

    def line(self) -> str:
        return "nomove %s %d" % (self.seat, self.die)


@dataclass(frozen=True)
class TokenMoved(GameEvent):
    seat: Seat
    token_index: int
    frm: int
    to: int
    is_release: bool

    def line(self) -> str:
        return "move %s:%d %d %d %d" % (self.seat, self.token_index, self.frm, self.to,
                                        1 if self.is_release else 0)


@dataclass(frozen=True)
class TokenCaptured(GameEvent):
    victim_seat: Seat
    victim_index: int
    attacker_seat: Seat
    attacker_index: int
    cell: int

    def line(self) -> str:
        return "capture %s:%d %s:%d %d" % (self.victim_seat, self.victim_index,
                                           self.attacker_seat, self.attacker_index, self.cell)


@dataclass(frozen=True)
class TokenFinished(GameEvent):
    seat: Seat
    token_index: int

    def line(self) -> str:
        return "finish %s:%d" % (self.seat, self.token_index)


@dataclass(frozen=True)
class PlayerFinished(GameEvent):
    seat: Seat
    place: int

    def line(self) -> str:
        return "playerfinish %s %d" % (self.seat, self.place)


@dataclass(frozen=True)
class TurnVoided(GameEvent):
    seat: Seat
    restored_progress: Tuple[int, ...]
    restored_finished: Tuple[Seat, ...]

    def line(self) -> str:
        return "voided %s %s %s" % (
            self.seat,
            ",".join(str(p) for p in self.restored_progress),
            ",".join(str(s) for s in self.restored_finished) or "-")


@dataclass(frozen=True)
class TurnEnded(GameEvent):
    next_seat: Seat
    turn_number: int

    def line(self) -> str:
        return "turnend %s %d" % (self.next_seat, self.turn_number)


@dataclass(frozen=True)
class PlayerLeft(GameEvent):
    seat: Seat

    def line(self) -> str:
        return "left %s" % self.seat


@dataclass(frozen=True)
class MatchEnded(GameEvent):
    standings: Tuple[Standing, ...]

    def line(self) -> str:
        return "end " + ",".join(st.line() for st in self.standings)


def parse_event(line: str) -> GameEvent:
    parts = line.split()
    kind = parts[0]
    if kind == "dice":
        return DiceRolled(Seat.parse(parts[1]), int(parts[2]), int(parts[3]), bool(int(parts[4])))
    if kind == "nomove":
        return NoLegalMove(Seat.parse(parts[1]), int(parts[2]))
    if kind == "move":
        seat_s, idx_s = parts[1].split(":")
        return TokenMoved(Seat.parse(seat_s), int(idx_s), int(parts[2]), int(parts[3]), bool(int(parts[4])))
    if kind == "capture":
        vseat, vidx = parts[1].split(":")
        aseat, aidx = parts[2].split(":")
        return TokenCaptured(Seat.parse(vseat), int(vidx), Seat.parse(aseat), int(aidx), int(parts[3]))
    if kind == "finish":
        seat_s, idx_s = parts[1].split(":")
        return TokenFinished(Seat.parse(seat_s), int(idx_s))
    if kind == "playerfinish":
        return PlayerFinished(Seat.parse(parts[1]), int(parts[2]))
    if kind == "voided":
        seat = Seat.parse(parts[1])
        prog = tuple(int(p) for p in parts[2].split(","))
        fin = tuple(Seat.parse(s) for s in parts[3].split(",")) if parts[3] != "-" else ()
        return TurnVoided(seat, prog, fin)
    if kind == "turnend":
        return TurnEnded(Seat.parse(parts[1]), int(parts[2]))
    if kind == "left":
        return PlayerLeft(Seat.parse(parts[1]))
    if kind == "end":
        return MatchEnded(tuple(Standing.parse(t) for t in parts[1].split(",")))
    raise ValueError("bad event line: %r" % line)
