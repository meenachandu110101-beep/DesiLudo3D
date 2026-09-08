"""Board-map consistency tests (VALIDATED here; the C# mirror must match)."""
import os
import sys
import unittest

sys.path.insert(0, os.path.abspath(os.path.join(os.path.dirname(__file__), "..")))

from ludo import boardmap  # noqa: E402
from ludo.model import Seat, start_cell  # noqa: E402


class TestBoardMap(unittest.TestCase):

    def test_self_check_passes(self):
        boardmap.self_check()  # raises on any inconsistency

    def test_known_corners(self):
        # Spot checks against the classic layout (hand-verified).
        self.assertEqual(boardmap.LOOP[0], (1, 6))      # Red start
        self.assertEqual(boardmap.LOOP[13], (8, 1))     # Green start
        self.assertEqual(boardmap.LOOP[26], (13, 8))    # Yellow start
        self.assertEqual(boardmap.LOOP[39], (6, 13))    # Blue start
        self.assertEqual(boardmap.LOOP[50], (0, 7))     # Red column entrance
        self.assertEqual(boardmap.LOOP[11], (7, 0))     # top middle
        self.assertEqual(boardmap.LOOP[24], (14, 7))    # right middle
        self.assertEqual(boardmap.LOOP[37], (7, 14))    # bottom middle

    def test_seat_starts_on_arm_lanes(self):
        for seat in Seat:
            x, y = boardmap.LOOP[start_cell(seat)]
            self.assertTrue(x in (6, 8) or y in (6, 8),
                            "%s start %r not on an arm lane" % (seat, (x, y)))

    def test_position_walk_red_full_path(self):
        seen = [boardmap.position(Seat.RED, p) for p in range(0, 57)]
        self.assertEqual(len(set(seen[:51])), 51)       # loop part unique
        self.assertEqual(seen[56], (7, 7))              # centre


if __name__ == "__main__":
    unittest.main()
