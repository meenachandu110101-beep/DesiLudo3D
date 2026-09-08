"""Shared test helpers (sys.path bootstrap + ergonomic driver)."""
import os
import sys

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from ludo import engine, model  # noqa: E402
from ludo.model import (  # noqa: E402
    LeaveMatchCommand, MatchConfig, MoveTokenCommand, PROG_FINISHED,
    RollDiceCommand, Seat, TOKENS_PER_SEAT, TurnPhase,
)

RED, GREEN, YELLOW, BLUE = Seat.RED, Seat.GREEN, Seat.YELLOW, Seat.BLUE


class D:
    """Ergonomic deterministic driver around the pure engine."""

    def __init__(self, players=4, **cfg_overrides):
        self.s = engine.new_match(MatchConfig.default(players, **cfg_overrides))
        self.events = []

    def place(self, seat, idx, progress):
        """Direct board setup (test-only; bypasses rules)."""
        self.s.token(seat, idx).progress = progress
        return self

    def place_all(self, seat, progress):
        for i in range(TOKENS_PER_SEAT):
            self.place(seat, i, progress)
        return self

    def roll(self, seat, die):
        res = engine.apply(self.s, RollDiceCommand(seat, die))
        assert res.ok, "roll(%s, %d) failed: %s" % (seat, die, res.error)
        self.s = res.new_state
        self.events.extend(res.events)
        return self

    def move(self, seat, idx):
        res = engine.apply(self.s, MoveTokenCommand(seat, idx))
        assert res.ok, "move(%s, %d) failed: %s" % (seat, idx, res.error)
        self.s = res.new_state
        self.events.extend(res.events)
        return self

    def leave(self, seat):
        res = engine.apply(self.s, LeaveMatchCommand(seat))
        assert res.ok, "leave(%s) failed: %s" % (seat, res.error)
        self.s = res.new_state
        self.events.extend(res.events)
        return self

    def try_roll(self, seat, die):
        return engine.apply(self.s, RollDiceCommand(seat, die))

    def try_move(self, seat, idx):
        return engine.apply(self.s, MoveTokenCommand(seat, idx))

    def try_leave(self, seat):
        return engine.apply(self.s, LeaveMatchCommand(seat))

    def legal(self, seat, die):
        return engine.get_legal_moves(self.s, seat, die)

    def lines(self):
        return [e.line() for e in self.events]

    def last(self, n):
        return [e.line() for e in self.events[-n:]]

    def prog(self, seat, idx):
        return self.s.token(seat, idx).progress

    def tokens_home(self, seat):
        return self.s.tokens_home(seat)
