"""Core rules tests — hand-computed ground truth (IL-010 trust-chain root).

Board math used throughout (memorize):
  starts: Red=0, Green=13, Yellow=26, Blue=39 (global cells)
  safe cells: {0, 8, 13, 21, 26, 34, 39, 47}
  progress: -1 base, 0..50 loop, 51..55 column, 56 finished
  global cell of (seat, progress) = (start + progress) % 52
"""
import unittest

from helpers import BLUE, D, GREEN, RED, YELLOW


class TestReleaseAndMovement(unittest.TestCase):

    def test_new_match_initial_state(self):
        d = D(4)
        self.assertEqual(str(d.s.current_seat), "Red")
        self.assertEqual(d.s.phase.value, "AwaitRoll")
        self.assertEqual(d.s.flat_progress(), [-1] * 16)
        self.assertEqual(d.s.turn_count, 0)
        self.assertIsNone(d.s.final_standings)

    def test_release_requires_six(self):
        d = D(4)
        self.assertEqual(d.legal(RED, 3), [])
        self.assertEqual(len(d.legal(RED, 6)), 4)  # one release move per base token
        d.roll(RED, 3)
        self.assertEqual(d.last(3), ["dice Red 3 1 0", "nomove Red 3", "turnend Green 1"])
        self.assertEqual(str(d.s.current_seat), "Green")

    def test_release_lands_on_start_and_grants_extra_roll(self):
        d = D(4)
        d.roll(RED, 6)
        self.assertEqual(d.s.phase.value, "AwaitMove")
        d.move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 0)
        self.assertEqual(d.last(2), ["dice Red 6 1 0", "move Red:0 -1 0 1"])
        self.assertEqual(d.s.phase.value, "AwaitRoll")     # extra roll
        self.assertEqual(str(d.s.current_seat), "Red")
        d.roll(RED, 2)                                     # second roll this turn
        self.assertIn("dice Red 2 2 1", d.lines())          # was_extra = 1
        d.move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 2)

    def test_plain_movement_ends_turn(self):
        d = D(4)
        d.place(RED, 0, 2)
        d.roll(RED, 4).move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 6)
        self.assertEqual(str(d.s.current_seat), "Green")    # die 4, no 6, no capture
        self.assertEqual(d.last(3)[2], "turnend Green 1")

    def test_overshoot_is_illegal_exact_finish_required(self):
        d = D(4)
        d.place(RED, 0, 53)          # needs exactly 3 to finish
        self.assertEqual(d.legal(RED, 5), [])
        d.roll(RED, 5)
        self.assertEqual(d.last(2), ["nomove Red 5", "turnend Green 1"])
        d2 = D(4)
        d2.place(RED, 0, 53)
        d2.roll(RED, 3).move(RED, 0)
        self.assertEqual(d2.prog(RED, 0), 56)
        self.assertIn("finish Red:0", d2.lines())
        self.assertNotIn("playerfinish Red 1", d2.lines())  # 1 of 4 home only
        self.assertEqual(str(d2.s.current_seat), "Green")

    def test_home_column_movement_no_capture(self):
        d = D(2)
        d.place(RED, 0, 51)          # inside private column
        d.place(YELLOW, 0, 30)       # yellow on loop cell 4 — irrelevant
        d.roll(RED, 4).move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 55)
        self.assertNotIn("capture", " ".join(d.lines()))


class TestCapture(unittest.TestCase):

    def test_capture_sends_victim_home_and_grants_extra_roll(self):
        d = D(2)
        d.place(RED, 0, 2)           # cell 2 (non-safe)
        d.place(YELLOW, 0, 30)       # (26+30)%52 = cell 4 (non-safe)
        moves = d.legal(RED, 2)
        self.assertEqual(len(moves), 1)
        self.assertTrue(moves[0].captures)
        d.roll(RED, 2).move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 4)
        self.assertEqual(d.prog(YELLOW, 0), -1)             # victim home
        self.assertEqual(d.last(2), [
            "move Red:0 2 4 0",
            "capture Yellow:0 Red:0 4",
        ])
        self.assertIn("capture Yellow:0 Red:0 4", d.lines())
        self.assertEqual(d.s.phase.value, "AwaitRoll")      # D3 extra roll
        self.assertEqual(str(d.s.current_seat), "Red")

    def test_capture_extra_roll_toggle_off(self):
        d = D(2, capture_extra=False)
        d.place(RED, 0, 2)
        d.place(YELLOW, 0, 30)
        d.roll(RED, 2).move(RED, 0)
        self.assertEqual(str(d.s.current_seat), "Yellow")   # no extra roll
        self.assertEqual(d.last(1), ["turnend Yellow 1"])

    def test_safe_cell_never_captures(self):
        d = D(2)
        d.place(YELLOW, 0, 34)       # Yellow on cell (26+34)%52 = 8 (star, safe)
        d.place(RED, 0, 4)           # Red will move 4 -> 8 onto the star
        moves = d.legal(RED, 4)
        self.assertEqual(len(moves), 1)
        self.assertFalse(moves[0].captures)
        d.roll(RED, 4).move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 8)
        self.assertEqual(d.prog(YELLOW, 0), 34)             # coexist on cell 8
        self.assertNotIn("capture", " ".join(d.lines()))
        self.assertEqual(str(d.s.current_seat), "Yellow")   # plain die ends turn

    def test_capture_entire_stack_when_blockades_off(self):
        d = D(4, blockades=False)
        d.place(YELLOW, 0, 20)       # both on cell (26+20)%52 = 46
        d.place(YELLOW, 1, 20)
        d.place(RED, 0, 40)
        moves = d.legal(RED, 6)
        sel = [m for m in moves if m.token_index == 0]
        self.assertEqual(len(sel), 1)
        self.assertTrue(sel[0].captures)
        d.roll(RED, 6).move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 46)
        self.assertEqual(d.prog(YELLOW, 0), -1)
        self.assertEqual(d.prog(YELLOW, 1), -1)             # both captured

    def test_stack_is_blockade_when_toggle_on(self):
        d = D(4, blockades=True)
        d.place(YELLOW, 0, 20)       # blockade on cell 46
        d.place(YELLOW, 1, 20)
        d.place(RED, 0, 40)
        sel = [m for m in d.legal(RED, 6) if m.token_index == 0]
        self.assertEqual(sel, [])                            # landing blocked


class TestBlockades(unittest.TestCase):

    def test_blockade_blocks_passage(self):
        d = D(4)
        d.place(GREEN, 0, 44)        # (13+44)%52 = cell 5
        d.place(GREEN, 1, 44)
        d.place(RED, 0, 1)
        self.assertEqual(d.legal(RED, 4), [])                # path 2..5 blocked
        d.roll(RED, 4)
        self.assertEqual(d.last(2), ["nomove Red 4", "turnend Green 1"])

    def test_blockade_blocks_even_on_six_path(self):
        d = D(4)
        d.place(GREEN, 0, 44)        # cell 5 blockade
        d.place(GREEN, 1, 44)
        d.place(RED, 0, 1)
        sel = [m for m in d.legal(RED, 6) if m.token_index == 0]
        self.assertEqual(sel, [])                            # path 2..7 blocked
        # base-token releases are still legal (start cell 0 is clear);
        # Red0 is already on the track, so exactly 3 releases exist
        self.assertEqual(len(d.legal(RED, 6)), 3)

    def test_own_stack_never_blocks_owner(self):
        d = D(4)
        d.place(RED, 0, 5)
        d.place(RED, 1, 5)           # own pair on cell 5
        d.place(RED, 2, 1)
        idxs = sorted(m.token_index for m in d.legal(RED, 4))
        self.assertEqual(idxs, [0, 1, 2])                    # owner passes/stacks

    def test_blockade_on_start_cell_blocks_release(self):
        d = D(4)
        d.place(YELLOW, 0, 26)       # (26+26)%52 = cell 0 = Red's start (safe)
        d.place(YELLOW, 1, 26)
        self.assertEqual(d.legal(RED, 6), [])                # release blocked
        d.roll(RED, 6)                                       # IL-004: 6 still grants its roll
        self.assertEqual(d.last(2), ["dice Red 6 1 0", "nomove Red 6"])
        self.assertEqual(d.s.phase.value, "AwaitRoll")
        self.assertEqual(str(d.s.current_seat), "Red")

    def test_blockades_toggle_off_allows_passage(self):
        d = D(4, blockades=False)
        d.place(GREEN, 0, 44)
        d.place(GREEN, 1, 44)
        d.place(RED, 0, 1)
        moves = d.legal(RED, 4)
        self.assertEqual([m.token_index for m in moves], [0])  # pass + land freely


class TestThreeConsecutiveSixes(unittest.TestCase):

    def test_third_six_voids_entire_turn(self):
        d = D(4)
        d.roll(RED, 6).move(RED, 0)          # release to 0
        d.roll(RED, 6).move(RED, 0)          # 0 -> 6
        d.roll(RED, 6)                       # third six: void
        self.assertEqual(d.prog(RED, 0), -1)  # back to base (IL-003)
        self.assertIn("voided Red " + ",".join(["-1"] * 16) + " -", d.lines())
        self.assertEqual(str(d.s.current_seat), "Green")
        self.assertEqual(d.last(1), ["turnend Green 1"])

    def test_void_restores_captures_and_column_entry(self):
        d = D(4)
        d.place(RED, 0, 40)                  # cell 40
        d.place(YELLOW, 0, 20)               # cell (26+20)%52 = 46
        d.roll(RED, 6).move(RED, 0)          # 40 -> 46, captures Yellow:0
        self.assertEqual(d.prog(YELLOW, 0), -1)
        d.roll(RED, 6).move(RED, 0)          # 46 -> 52 (home column)
        d.roll(RED, 6)                       # void
        self.assertEqual(d.prog(RED, 0), 40)          # restored
        self.assertEqual(d.prog(YELLOW, 0), 20)       # capture undone
        self.assertEqual(d.s.finished_order, [])
        self.assertIn("voided Red 40,-1,-1,-1,-1,-1,-1,-1,20,-1,-1,-1,-1,-1,-1,-1 -",
                      d.lines())

    def test_three_sixes_toggle_off(self):
        d = D(4, three_sixes=False)
        d.roll(RED, 6).move(RED, 0)
        d.roll(RED, 6).move(RED, 0)
        d.roll(RED, 6)                       # no void when disabled
        self.assertNotIn("voided", " ".join(d.lines()))
        self.assertEqual(d.s.phase.value, "AwaitMove")
        self.assertEqual(str(d.s.current_seat), "Red")
        d.move(RED, 0)
        self.assertEqual(d.prog(RED, 0), 12)

    def test_non_six_ends_chain_and_turn(self):
        d = D(4)
        d.roll(RED, 6).move(RED, 0)          # six #1 (extra roll granted)
        d.roll(RED, 3).move(RED, 0)          # non-six move ends the turn cleanly
        self.assertEqual(str(d.s.current_seat), "Green")
        self.assertNotIn("voided", " ".join(d.lines()))

    def test_void_counter_does_not_leak_across_turns(self):
        d = D(4)
        d.roll(RED, 6).move(RED, 0)
        d.roll(RED, 6).move(RED, 0)
        d.roll(RED, 6)                       # Red voids
        self.assertIn("voided Red", " ".join(d.lines()))
        d.roll(GREEN, 6).move(GREEN, 0)      # Green's fresh turn
        d.roll(GREEN, 6).move(GREEN, 0)
        d.roll(GREEN, 6)                     # Green voids independently
        self.assertIn("voided Green", " ".join(d.lines()))
        self.assertEqual(d.prog(GREEN, 0), -1)
        d.roll(YELLOW, 6).move(YELLOW, 0)
        d.roll(YELLOW, 6).move(YELLOW, 0)
        d.roll(YELLOW, 6)                    # third turn also voids
        self.assertEqual(d.s.phase.value, "AwaitRoll")
        self.assertEqual(str(d.s.current_seat), "Blue")

    def test_no_move_six_chain_voids_on_third(self):
        # IL-004 + D2 interplay: 6 with no legal move still rolls again,
        # and the third such six voids (no moves were made).
        d = D(4)
        d.place(YELLOW, 0, 26)               # blockade on Red's start cell 0
        d.place(YELLOW, 1, 26)
        d.place(RED, 0, 53)                  # 53 + 6 overshoots
        d.roll(RED, 6)
        d.roll(RED, 6)
        self.assertEqual(d.s.phase.value, "AwaitRoll")
        d.roll(RED, 6)
        self.assertIn("voided", " ".join(d.lines()))
        self.assertEqual(str(d.s.current_seat), "Green")
        self.assertEqual(d.prog(RED, 0), 53)


if __name__ == "__main__":
    unittest.main()
