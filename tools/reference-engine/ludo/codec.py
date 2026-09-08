"""DesiLudo3D reference engine — deterministic text codecs.

Formats are the AUTHORITATIVE cross-implementation contract (C# must read and
write byte-identical text). See docs/RULES-FORMAT.md.
"""
from __future__ import annotations

from typing import List, Tuple

from ludo.model import (
    Command, GameEvent, GameState, MatchConfig, PROG_BASE, Seat,
    Standing, TOKENS_PER_SEAT, TurnPhase, parse_command, parse_event,
    seats_for_player_count,
)

STATE_HEADER = "DESI-LUDO-STATE v1"
CMDLOG_HEADER = "DESI-LUDO-CMDLOG v1"
EVENTLOG_HEADER = "DESI-LUDO-EVENTLOG v1"


def _seat_list(text: str) -> Tuple[Seat, ...]:
    return tuple(Seat.parse(t) for t in text.split(",")) if text else ()


def _int_list(text: str) -> List[int]:
    return [int(t) for t in text.split(",")] if text else []


def save_state(state: GameState) -> str:
    cfg = state.config
    lines = [
        STATE_HEADER,
        "seats=" + ",".join(str(s) for s in cfg.seats),
        "blockades=%d" % (1 if cfg.blockades else 0),
        "threesixes=%d" % (1 if cfg.three_sixes else 0),
        "capturex=%d" % (1 if cfg.capture_extra else 0),
        "placings=%d" % (1 if cfg.placings else 0),
        "twopend=%d" % (1 if cfg.two_player_end else 0),
        "current=" + str(state.current_seat),
        "phase=" + state.phase.value,
        "lastdie=%d" % state.last_die,
        "consec=%d" % state.consec_sixes,
        "rolls=%d" % state.rolls_this_turn,
        "turns=%d" % state.turn_count,
        "finished=" + ",".join(str(s) for s in state.finished_order),
        "left=" + ",".join(str(s) for s in sorted(state.left)),
    ]
    prog = ";".join(
        "%s:%s" % (seat, ",".join(str(state.token(seat, i).progress)
                                  for i in range(TOKENS_PER_SEAT)))
        for seat in Seat
    )
    lines.append("progress=" + prog)
    # Turn-start snapshot is part of logical state (IL-009).
    if state.snap_progress is not None:
        lines.append("snap=" + ",".join(str(p) for p in state.snap_progress))
        lines.append("snapfin=" + (",".join(str(s) for s in state.snap_finished)
                                   if state.snap_finished else ""))
    if state.final_standings is not None:
        lines.append("standings=" + ",".join(st.line() for st in state.final_standings))
    return "\n".join(lines) + "\n"


def load_state(text: str) -> GameState:
    lines = [ln for ln in text.splitlines() if ln.strip()]
    if not lines or lines[0] != STATE_HEADER:
        raise ValueError("bad state header: %r" % (lines[0] if lines else ""))
    kv = {}
    for ln in lines[1:]:
        if "=" not in ln:
            raise ValueError("bad state line: %r" % ln)
        k, v = ln.split("=", 1)
        if k in kv:
            raise ValueError("duplicate state key: %r" % k)
        kv[k] = v

    def need(key: str) -> str:
        if key not in kv:
            raise ValueError("missing state key: %r" % key)
        return kv[key]

    seats = _seat_list(need("seats"))
    cfg = MatchConfig(
        seats=seats,
        blockades=need("blockades") == "1",
        three_sixes=need("threesixes") == "1",
        capture_extra=need("capturex") == "1",
        placings=need("placings") == "1",
        two_player_end=need("twopend") == "1",
    )
    state = GameState(cfg)
    state.current_seat = Seat.parse(need("current"))
    state.phase = TurnPhase.parse(need("phase"))
    state.last_die = int(need("lastdie"))
    state.consec_sixes = int(need("consec"))
    state.rolls_this_turn = int(need("rolls"))
    state.turn_count = int(need("turns"))
    state.finished_order = list(_seat_list(need("finished")))
    state.left = set(_seat_list(need("left")))

    prog_part = need("progress")
    for seat_part in prog_part.split(";"):
        seat_s, idxs = seat_part.split(":")
        seat = Seat.parse(seat_s)
        vals = _int_list(idxs)
        if len(vals) != TOKENS_PER_SEAT:
            raise ValueError("progress for %s must have %d values" % (seat_s, TOKENS_PER_SEAT))
        for i, v in enumerate(vals):
            state.token(seat, i).progress = v

    if "snap" in kv:
        snap = _int_list(kv["snap"])
        if len(snap) != len(state.tokens):
            raise ValueError("snap must have %d values" % len(state.tokens))
        state.snap_progress = tuple(snap)
        state.snap_finished = tuple(_seat_list(kv.get("snapfin", "")))
    if "standings" in kv:
        state.final_standings = [Standing.parse(t) for t in kv["standings"].split(",")]
    return state


def save_commands(commands: List[Command]) -> str:
    return "\n".join([CMDLOG_HEADER] + [c.line() for c in commands]) + "\n"


def load_commands(text: str) -> List[Command]:
    lines = [ln for ln in text.splitlines() if ln.strip()]
    if not lines or lines[0] != CMDLOG_HEADER:
        raise ValueError("bad command log header")
    return [parse_command(ln) for ln in lines[1:]]


def save_events(events: List[GameEvent]) -> str:
    return "\n".join([EVENTLOG_HEADER] + [e.line() for e in events]) + "\n"


def load_events(text: str) -> List[GameEvent]:
    lines = [ln for ln in text.splitlines() if ln.strip()]
    if not lines or lines[0] != EVENTLOG_HEADER:
        raise ValueError("bad event log header")
    return [parse_event(ln) for ln in lines[1:]]
