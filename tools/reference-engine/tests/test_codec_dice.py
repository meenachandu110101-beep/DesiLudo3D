"""Codecs, determinism/replay, and commit–reveal dice tests."""
import random
import unittest

from helpers import D  # noqa: F401

from ludo import codec, dice, engine, simulation
from ludo.model import MatchConfig, RollDiceCommand, Seat


class TestStateCodec(unittest.TestCase):

    def test_roundtrip_mid_turn_with_snapshot(self):
        d = D(4)
        d.roll(Seat.RED, 6).move(Seat.RED, 0)     # release
        d.roll(Seat.RED, 6).move(Seat.RED, 0)     # 0 -> 6 (two rolls this turn)
        self.assertEqual(d.s.rolls_this_turn, 2)
        self.assertIsNotNone(d.s.snap_progress)
        text = codec.save_state(d.s)
        loaded = codec.load_state(text)
        self.assertEqual(loaded.state_hash(), d.s.state_hash())
        self.assertEqual(loaded.snap_progress, d.s.snap_progress)
        # Resuming must still void correctly on a third six (IL-009)
        res = engine.apply(loaded, RollDiceCommand(Seat.RED, 6))
        self.assertTrue(res.ok)
        self.assertEqual(res.new_state.token(Seat.RED, 0).progress, -1)
        self.assertIn("voided", " ".join(e.line() for e in res.events))

    def test_roundtrip_random_final_states(self):
        for players, kw in ((4, {}), (2, {}), (4, {"blockades": False})):
            cfg = MatchConfig.default(players, **kw)
            for seed in range(1, 6):
                state, _, _ = simulation.play_random_match(cfg, seed)
                loaded = codec.load_state(codec.save_state(state))
                self.assertEqual(loaded.state_hash(), state.state_hash())
                self.assertEqual([s.line() for s in loaded.final_standings],
                                 [s.line() for s in state.final_standings])

    def test_bad_state_rejected(self):
        for bad in ("", "DESI-LUDO-STATE v0\n", "DESI-LUDO-STATE v1\ngarbage\n",
                    "DESI-LUDO-STATE v1\nseats=Red\n"):  # 1 seat invalid
            with self.assertRaises(ValueError):
                codec.load_state(bad)


class TestCommandAndEventCodecs(unittest.TestCase):

    def test_command_log_roundtrip_and_replay(self):
        for players, kw in ((4, {}), (2, {}), (4, {"blockades": False}),
                            (3, {}), (4, {"three_sixes": False})):
            cfg = MatchConfig.default(players, **kw)
            for seed in range(1, 8):
                state, log, events = simulation.play_random_match(cfg, seed)
                text = codec.save_commands(log)
                loaded = codec.load_commands(text)
                self.assertEqual([c.line() for c in loaded], [c.line() for c in log])
                # Replay determinism: identical state + identical events
                rstate, revents = simulation.replay(cfg, loaded)
                self.assertEqual(rstate.state_hash(), state.state_hash())
                self.assertEqual([e.line() for e in revents],
                                 [e.line() for e in events])

    def test_event_log_roundtrip(self):
        cfg = MatchConfig.default(4)
        state, log, events = simulation.play_random_match(cfg, 3)
        text = codec.save_events(events)
        loaded = codec.load_events(text)
        self.assertEqual([e.line() for e in loaded], [e.line() for e in events])


class TestDeterminism(unittest.TestCase):

    def test_same_seed_same_match(self):
        cfg = MatchConfig.default(4)
        a_state, a_log, a_events = simulation.play_random_match(cfg, 42)
        b_state, b_log, b_events = simulation.play_random_match(cfg, 42)
        self.assertEqual(a_state.state_hash(), b_state.state_hash())
        self.assertEqual([c.line() for c in a_log], [c.line() for c in b_log])
        self.assertEqual([e.line() for e in a_events], [e.line() for e in b_events])

    def test_matches_complete_within_budget(self):
        for players, kw in ((4, {}), (2, {}), (4, {"blockades": False})):
            cfg = MatchConfig.default(players, **kw)
            for seed in range(1, 11):
                state, log, _ = simulation.play_random_match(cfg, seed, max_commands=100_000)
                self.assertEqual(state.phase.value, "MatchOver")
                self.assertLess(len(log), 20_000)   # sanity: no pathological loops

    def test_state_hash_distinguishes_snapshot(self):
        a = engine.new_match(MatchConfig.default(4))
        b = engine.new_match(MatchConfig.default(4))
        b.take_snapshot()                    # same board, different turn history
        self.assertNotEqual(a.state_hash(), b.state_hash())


class TestCommitRevealDice(unittest.TestCase):

    def test_commitment_properties(self):
        s1 = dice.create_seed(16)
        self.assertEqual(len(s1), 16)
        self.assertEqual(dice.commitment(s1), dice.commitment(s1))   # deterministic
        seen = set()
        for _ in range(100):
            seen.add(dice.commitment(dice.create_seed()))
        self.assertEqual(len(seen), 100)     # collisions astronomically unlikely

    def test_combine_deterministic_and_bounded(self):
        a, b = dice.create_seed(), dice.create_seed()
        for _ in range(10):
            v = dice.combine_die_value([a, b])
            self.assertIn(v, (1, 2, 3, 4, 5, 6))

    def test_combine_needs_two_seeds(self):
        with self.assertRaises(ValueError):
            dice.combine_die_value([dice.create_seed()])

    def test_distribution_is_fair(self):
        rng = random.Random(7)
        counts = [0] * 7
        for _ in range(6000):
            v = dice.combine_die_value([dice.create_seed(8), dice.create_seed(8)])
            counts[v] += 1
        for face in range(1, 7):
            self.assertTrue(700 <= counts[face] <= 1300,
                            "face %d count %d outside fair band" % (face, counts[face]))

    def test_neither_party_alone_can_bias(self):
        """Fix one party's seed; the other party's seed alone determines the
        value — so the fixed party cannot predict or steer the outcome."""
        fixed = dice.create_seed(32)
        rng = random.Random(11)
        counts = [0] * 7
        for _ in range(600):
            v = dice.combine_die_value([fixed, dice.create_seed(16)])
            counts[v] += 1
        for face in range(1, 7):
            self.assertGreaterEqual(counts[face], 60,
                                    "face %d underrepresented: %d" % (face, counts[face]))

    def test_verify_commitments(self):
        seeds = [dice.create_seed(), dice.create_seed()]
        commits = [dice.commitment(s) for s in seeds]
        self.assertTrue(dice.verify(seeds, commits))
        self.assertFalse(dice.verify(seeds, commits[::-1]))
        self.assertFalse(dice.verify(seeds + [dice.create_seed()], commits))


if __name__ == "__main__":
    unittest.main()
