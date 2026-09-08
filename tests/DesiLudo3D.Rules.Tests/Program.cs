using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Rules.Tests
{
    /// <summary>Zero-dependency test harness.
    /// 1) Conformance vectors: replays every vector in tests/conformance-vectors
    ///    and requires byte-identical events, final state text, and state hash
    ///    with the Python-validated reference engine (IL-001/IL-008/IL-010).
    /// 2) Embedded unit tests mirroring the reference suite's ground truth.
    /// 3) Simulation self-consistency smoke (C#-side fuzz).
    /// Run:  dotnet run --project tests/DesiLudo3D.Rules.Tests</summary>
    public static class Program
    {
        static int _passed;
        static int _failed;
        static readonly List<string> Failures = new List<string>();

        static void Run(string name, Action a)
        {
            try
            {
                a();
                _passed++;
                Console.WriteLine("ok   " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Failures.Add(name + ": " + ex.Message);
                Console.WriteLine("FAIL " + name + ": " + ex.Message);
            }
        }

        static void Equal<T>(T expected, T actual, string what)
        {
            if (!Equals(expected, actual))
                throw new Exception(what + ": expected <" + expected + "> got <" + actual + ">");
        }

        static int Main()
        {
            Console.WriteLine("DesiLudo3D.Rules canonical test suite");
            Console.WriteLine("================================================");

            string vectorsDir = FindVectorsDirectory();
            if (vectorsDir == null)
            {
                Console.WriteLine("FAIL could not locate tests/conformance-vectors (run from repo root)");
                return 1;
            }
            RunVectorConformance(vectorsDir);
            RunEmbeddedUnitTests();
            RunSimulationSmoke();

            Console.WriteLine("================================================");
            Console.WriteLine("passed=" + _passed + " failed=" + _failed);
            foreach (string f in Failures) Console.WriteLine("  FAILURE: " + f);
            return _failed == 0 ? 0 : 1;
        }

        // ------------------------------------------------------ vector runner

        static string FindVectorsDirectory()
        {
            string dir = AppContext.BaseDirectory;
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, "tests", "conformance-vectors");
                if (Directory.Exists(candidate)) return candidate;
                candidate = Path.Combine(dir, "conformance-vectors");
                if (Directory.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir.TrimEnd(Path.DirectorySeparatorChar));
            }
            return null;
        }

        sealed class Vector
        {
            public string Name;
            public MatchConfig Config;
            public List<Command> Commands = new List<Command>();
            public List<string> EventLines = new List<string>();
            public List<string> StateLines = new List<string>();
            public string Hash;
        }

        static Vector ParseVector(string path)
        {
            var v = new Vector { Name = Path.GetFileNameWithoutExtension(path) };
            bool blockades = true, threeSixes = true, capturex = true, placings = true, twopend = true;
            int players = 4;
            bool sawHeader = false;
            foreach (string raw in File.ReadAllLines(path))
            {
                string line = raw.TrimEnd('\r');
                if (line.Length == 0) continue;
                if (line.StartsWith("#"))
                {
                    if (line != "# DESI-LUDO-VECTOR v1")
                        throw new FormatException("bad vector header: " + line);
                    sawHeader = true;
                    continue;
                }
                if (!sawHeader) throw new FormatException("missing vector header");
                int eq = line.IndexOf('=');
                if (eq < 0) throw new FormatException("bad vector line: " + line);
                string key = line.Substring(0, eq);
                string value = line.Substring(eq + 1);
                switch (key)
                {
                    case "name": v.Name = value; break;
                    case "players": players = int.Parse(value); break;
                    case "blockades": blockades = value == "1"; break;
                    case "threesixes": threeSixes = value == "1"; break;
                    case "capturex": capturex = value == "1"; break;
                    case "placings": placings = value == "1"; break;
                    case "twopend": twopend = value == "1"; break;
                    case "cmd": v.Commands.Add(CommandParser.Parse(value)); break;
                    case "event": v.EventLines.Add(value); break;
                    case "state": v.StateLines.Add(value); break;
                    case "hash": v.Hash = value; break;
                    default: throw new FormatException("unknown vector key: " + key);
                }
            }
            v.Config = new MatchConfig(Seats.ForPlayerCount(players), blockades, threeSixes, capturex, placings, twopend);
            return v;
        }

        static void RunVectorConformance(string dir)
        {
            foreach (string file in Directory.GetFiles(dir, "*.vec").OrderBy(f => f))
            {
                Run("vector:" + Path.GetFileNameWithoutExtension(file), () =>
                {
                    Vector v = ParseVector(file);
                    GameState final = Simulation.Replay(v.Config, v.Commands, out var events);

                    var actualEvents = events.Select(e => e.Line()).ToList();
                    if (actualEvents.Count != v.EventLines.Count)
                        throw new Exception("event count " + actualEvents.Count + " != " + v.EventLines.Count);
                    for (int i = 0; i < actualEvents.Count; i++)
                        if (actualEvents[i] != v.EventLines[i])
                            throw new Exception("event[" + i + "] <" + actualEvents[i] + "> != <" + v.EventLines[i] + ">");

                    List<string> actualState = MatchCodec.SaveState(final)
                        .Split(new[] { '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                    if (actualState.Count != v.StateLines.Count)
                        throw new Exception("state line count " + actualState.Count + " != " + v.StateLines.Count);
                    for (int i = 0; i < actualState.Count; i++)
                        if (actualState[i] != v.StateLines[i])
                            throw new Exception("state[" + i + "] <" + actualState[i] + "> != <" + v.StateLines[i] + ">");

                    Equal(v.Hash, final.StateHash(), "state hash");
                });
            }
        }

        // ------------------------------------------------------ embedded tests

        static void RunEmbeddedUnitTests()
        {
            Run("unit:release_grants_extra_roll", () =>
            {
                GameState s = RulesEngine.NewMatch(MatchConfig.Default(4));
                s = Step(s, new RollDiceCommand(Seat.Red, 6));
                s = Step(s, new MoveTokenCommand(Seat.Red, 0));
                Equal(0, s.Token(Seat.Red, 0).Progress, "released to start");
                Equal(TurnPhase.AwaitRoll, s.Phase, "extra roll");
                Equal(Seat.Red, s.CurrentSeat, "still Red's turn");
            });

            Run("unit:three_sixes_voids_turn", () =>
            {
                GameState s = RulesEngine.NewMatch(MatchConfig.Default(4));
                s = Step(s, new RollDiceCommand(Seat.Red, 6));
                s = Step(s, new MoveTokenCommand(Seat.Red, 0));
                s = Step(s, new RollDiceCommand(Seat.Red, 6));
                s = Step(s, new MoveTokenCommand(Seat.Red, 0));
                s = Step(s, new RollDiceCommand(Seat.Red, 6));
                Equal(TokenProgress.Base, s.Token(Seat.Red, 0).Progress, "token returned to base");
                Equal(Seat.Green, s.CurrentSeat, "turn passed");
            });

            Run("unit:capture_sends_victim_home_and_grants_extra", () =>
            {
                GameState s = RulesEngine.NewMatch(MatchConfig.Default(2));
                s.Token(Seat.Red, 0).Progress = 2;      // cell 2 (non-safe)
                s.Token(Seat.Yellow, 0).Progress = 30;  // (26+30)%52 = cell 4 (non-safe)
                s = Step(s, new RollDiceCommand(Seat.Red, 2));
                s = Step(s, new MoveTokenCommand(Seat.Red, 0));
                Equal(4, s.Token(Seat.Red, 0).Progress, "attacker on cell 4");
                Equal(TokenProgress.Base, s.Token(Seat.Yellow, 0).Progress, "victim home");
                Equal(TurnPhase.AwaitRoll, s.Phase, "D3 extra roll");
                Equal(Seat.Red, s.CurrentSeat, "still Red's turn");
            });

            Run("unit:blockade_blocks_passage", () =>
            {
                GameState s = RulesEngine.NewMatch(MatchConfig.Default(4));
                s.Token(Seat.Green, 0).Progress = 44;   // (13+44)%52 = cell 5
                s.Token(Seat.Green, 1).Progress = 44;
                s.Token(Seat.Red, 0).Progress = 1;
                Equal(0, RulesEngine.GetLegalMoves(s, Seat.Red, 4).Count, "path blocked");
            });

            Run("unit:own_stack_never_blocks_owner", () =>
            {
                GameState s = RulesEngine.NewMatch(MatchConfig.Default(4));
                s.Token(Seat.Red, 0).Progress = 5;
                s.Token(Seat.Red, 1).Progress = 5;
                s.Token(Seat.Red, 2).Progress = 1;
                Equal(3, RulesEngine.GetLegalMoves(s, Seat.Red, 4).Count, "owner passes own stack");
            });

            Run("unit:state_codec_roundtrip", () =>
            {
                GameState s = RulesEngine.NewMatch(MatchConfig.Default(2));
                s = Step(s, new RollDiceCommand(Seat.Red, 6));
                s = Step(s, new MoveTokenCommand(Seat.Red, 0));
                GameState loaded = MatchCodec.LoadState(MatchCodec.SaveState(s));
                Equal(s.StateHash(), loaded.StateHash(), "hash after roundtrip");
            });

            Run("unit:commit_reveal_deterministic", () =>
            {
                byte[] a = { 1, 2, 3, 4 };
                byte[] b = { 9, 8, 7, 6 };
                int v1 = CommitRevealDice.CombineDieValue(a, b);
                int v2 = CommitRevealDice.CombineDieValue(a, b);
                Equal(v1, v2, "deterministic");
                if (v1 < 1 || v1 > 6) throw new Exception("value out of range: " + v1);
                Equal(CommitRevealDice.Commitment(a), CommitRevealDice.Commitment(a), "commitment stable");
            });
        }

        static GameState Step(GameState s, Command cmd)
        {
            ApplyResult res = RulesEngine.Apply(s, cmd);
            if (!res.Ok) throw new Exception("command failed: " + cmd.Line() + " -> " + res.Error);
            return res.NewState;
        }

        // ------------------------------------------------------ simulation smoke

        static void RunSimulationSmoke()
        {
            var configs = new[]
            {
                MatchConfig.Default(4),
                MatchConfig.Default(2),
                MatchConfig.Default(4, blockades: false),
            };
            for (int i = 0; i < configs.Length; i++)
            {
                MatchConfig cfg = configs[i];
                int seed = 1 + i * 17;
                Run("sim:replay_self_consistency_" + cfg.SeatsInPlay.Length + "p_seed" + seed, () =>
                {
                    Simulation.PlayRandomMatch(cfg, seed, 100_000,
                        out GameState final, out var log, out var events);
                    Equal(TurnPhase.MatchOver, final.Phase, "match finished");
                    if (!Simulation.VerifyReplay(cfg, log, final, events))
                        throw new Exception("replay mismatch");
                });
            }
        }
    }
}
