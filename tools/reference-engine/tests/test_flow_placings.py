"""Turn flow, validation errors, placings (D5) and leave-with-placing tests."""
import unittest

from helpers import BLUE, D, GREEN, RED, YELLOW


class TestCommandValidation(unittest.TestCase):

    def test_move_before_roll_rejected(self):
        d = D(4)
        res = d.try_move(RED, 0)
        self.assertFalse(res.ok)
        self.assertIn("roll first", res.error)

    def test_wrong_seat_roll_rejected(self):
        d = D(4)
        res = d.try_roll(GREEN, 1)
        self.assertFalse(res.ok)
        self.assertIn("not Green's turn", res.error)

    def test_invalid_die_rejected(self):
        d = D(4)
        for die in (0, 7, -1):
            res = d.try_roll(RED, die)
            self.assertFalse(res.ok)

    def test_roll_while_move_pending_rejected(self):
        d = D(4)
        d.roll(RED, 6)
        res = d.try_roll(RED, 3)
        self.assertFalse(res.ok)
        self.assertIn("move is pending", res.error)

    def test_illegal_token_choice_rejected(self):
        d = D(4)
        d.roll(RED, 6)
        res = d.try_move(RED, 9)             # out of range
        self.assertFalse(res.ok)
        d2 = D(4)
        d2.place(RED, 0, 2)
        d2.roll(RED, 6)                      # only releases are legal (2+6=8 path clear too!)
        # token 0 CAN move 2->8; use a token whose move is illegal instead:
        d3 = D(4)
        d3.place(YELLOW, 0, 26)              # blockade on cell 0
        d3.place(YELLOW, 1, 26)
        d3.roll(RED, 6)                      # no legal moves at all
        res = d3.try_move(RED, 0)
        self.assertFalse(res.ok)

    def test_wrong_seat_move_rejected(self):
        d = D(4)
        d.roll(RED, 6)
        res = d.try_move(GREEN, 0)
        self.assertFalse(res.ok)

    def test_double_leave_rejected(self):
        d = D(4)
        d.leave(GREEN)
        res = d.try_leave(GREEN)
        self.assertFalse(res.ok)

    def test_leave_nonexistent_seat_rejected(self):
        d = D(2)                              # only Red and Yellow play
        res = d.try_leave(BLUE)
        self.assertFalse(res.ok)

    def test_unknown_command_rejected(self):
        d = D(4)
        from ludo import engine
        res = engine.apply(d.s, "not-a-command")
        self.assertFalse(res.ok)

    def test_commands_after_match_over_rejected(self):
        d2 = D(2)
        for i in range(3):
            d2.place(RED, i, 56)
        d2.place(RED, 3, 53)
        d2.roll(RED, 3).move(RED, 3)         # Red finishes -> 2p match ends
        self.assertEqual(d2.s.phase.value, "MatchOver")
        self.assertFalse(d2.try_roll(YELLOW, 2).ok)
        self.assertFalse(d2.try_move(YELLOW, 0).ok)
        self.assertFalse(d2.try_leave(YELLOW).ok)

    def test_all_finished_synthetic_state_passes_turn(self):
        # A synthetic state (all tokens placed at 56 without rule-play) still
        # behaves sanely: no legal moves, turn passes, engine never crashes.
        d = D(2)
        d.place_all(RED, 56)
        d.roll(RED, 1)
        self.assertEqual(str(d.s.current_seat), "Yellow")


class TestTurnRotation(unittest.TestCase):

    def test_rotation_order(self):
        d = D(4)
        for expected_seat in (GREEN, YELLOW, BLUE, RED):
            d.roll(d.s.current_seat, 3)      # all-base: nomove -> pass
            self.assertEqual(d.s.current_seat, expected_seat)
        self.assertEqual(d.s.turn_count, 4)

    def test_finished_players_are_skipped(self):
        d = D(4)
        for i in range(3):
            d.place(RED, i, 56)
        d.place(RED, 3, 53)
        d.roll(RED, 3).move(RED, 3)          # Red finishes (place 1); match continues
        self.assertEqual(d.s.phase.value, "AwaitRoll")
        d.roll(GREEN, 3)                     # pass
        d.roll(YELLOW, 3)                    # pass
        d.roll(BLUE, 3)                      # pass -> back to Green (Red skipped)
        self.assertEqual(d.s.current_seat, GREEN)

    def test_left_player_is_skipped(self):
        d = D(4)
        d.leave(GREEN)
        d.roll(RED, 3)
        self.assertEqual(d.s.current_seat, YELLOW)   # Green skipped


class TestPlacings(unittest.TestCase):

    def test_four_player_placings_continue_to_end(self):
        d = D(4)
        # Red finishes 1st
        for i in range(3):
            d.place(RED, i, 56)
        d.place(RED, 3, 53)
        d.roll(RED, 3).move(RED, 3)
        self.assertIn("playerfinish Red 1", d.lines())
        self.assertEqual(d.s.phase.value, "AwaitRoll")   # match continues (D5)
        # Green finishes 2nd
        for i in range(3):
            d.place(GREEN, i, 56)
        d.place(GREEN, 3, 55)
        d.roll(GREEN, 1).move(GREEN, 3)
        self.assertIn("playerfinish Green 2", d.lines())
        # Yellow cannot finish; passes
        d.roll(YELLOW, 3)
        # Blue finishes 3rd -> only Yellow remains -> match ends
        for i in range(3):
            d.place(BLUE, i, 56)
        d.place(BLUE, 3, 52)
        d.roll(BLUE, 4).move(BLUE, 3)
        self.assertIn("playerfinish Blue 3", d.lines())
        self.assertEqual(d.s.phase.value, "MatchOver")
        self.assertEqual([s.line() for s in d.s.final_standings],
                         ["Red:1:4", "Green:2:4", "Blue:3:4", "Yellow:4:0"])

    def test_two_player_ends_at_first_winner(self):
        d = D(2)
        for i in range(3):
            d.place(RED, i, 56)
        d.place(RED, 3, 53)
        d.roll(RED, 3).move(RED, 3)
        self.assertEqual(d.s.phase.value, "MatchOver")   # ends immediately
        self.assertEqual([s.line() for s in d.s.final_standings],
                         ["Red:1:4", "Yellow:2:0"])

    def test_placings_off_ends_at_first_winner(self):
        d = D(4, placings=False)
        for i in range(3):
            d.place(RED, i, 56)
        d.place(RED, 3, 53)
        d.place(GREEN, 0, 56)
        d.place(GREEN, 1, 56)                # Green: 2 home
        d.place(YELLOW, 0, 56)               # Yellow: 1 home
        d.roll(RED, 3).move(RED, 3)
        self.assertEqual(d.s.phase.value, "MatchOver")
        # remaining ranked by tokens-home desc (IL-011): Green(2), Yellow(1), Blue(0)
        self.assertEqual([s.line() for s in d.s.final_standings],
                         ["Red:1:4", "Green:2:2", "Yellow:3:1", "Blue:4:0"])

    def test_leave_with_placing_ranking(self):
        d = D(4)
        d.leave(GREEN)                        # Green leaves early, 0 home... set homes after
        d.place(GREEN, 0, 56)
        d.place(GREEN, 1, 56)                 # Green: 2 home at match end
        d.place(YELLOW, 0, 56)                # Yellow: 1 home
        # Red finishes 1st
        for i in range(3):
            d.place(RED, i, 56)
        d.place(RED, 3, 53)
        d.roll(RED, 3).move(RED, 3)
        # current turn passed to next active after Red = Yellow (Green left)
        self.assertEqual(d.s.current_seat, YELLOW)
        # Yellow leaves -> only Blue remains active -> match ends
        d.leave(YELLOW)
        self.assertEqual(d.s.phase.value, "MatchOver")
        self.assertEqual([s.line() for s in d.s.final_standings],
                         ["Red:1:4", "Green:2:2", "Yellow:3:1", "Blue:4:0"])

    def test_leaving_current_seat_passes_turn(self):
        d = D(4)
        d.roll(RED, 6)                        # move pending for Red
        d.leave(RED)
        self.assertEqual(d.s.phase.value, "AwaitRoll")
        self.assertEqual(d.s.current_seat, GREEN)

    def test_last_player_leaving_ends_match(self):
        d = D(2)
        d.leave(RED)                          # Yellow alone -> ends
        self.assertEqual(d.s.phase.value, "MatchOver")
        # IL-011: on ties, the stayer (Yellow) ranks above the leaver (Red).
        self.assertEqual([s.line() for s in d.s.final_standings],
                         ["Yellow:1:0", "Red:2:0"])


class TestNoMoveOnSix(unittest.TestCase):

    def test_six_with_no_legal_move_still_grants_extra_roll(self):
        d = D(4)
        d.place(YELLOW, 0, 26)               # blockade on Red's start
        d.place(YELLOW, 1, 26)
        d.place(RED, 0, 53)                  # overshoot on 6
        d.roll(RED, 6)
        self.assertIn("nomove Red 6", d.lines())
        self.assertEqual(d.s.phase.value, "AwaitRoll")   # IL-004
        self.assertEqual(d.s.current_seat, RED)
        self.assertEqual(d.s.consec_sixes, 1)


if __name__ == "__main__":
    unittest.main()
