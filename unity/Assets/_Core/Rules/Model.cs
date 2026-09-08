using System;
using System.Collections.Generic;
using System.Text;

namespace DesiLudo3D.Rules
{
    // ------------------------------------------------------------------ seat

    public enum Seat { Red = 0, Green = 1, Yellow = 2, Blue = 3 }

    public static class Seats
    {
        public static string Name(Seat s)
        {
            switch (s)
            {
                case Seat.Red: return "Red";
                case Seat.Green: return "Green";
                case Seat.Yellow: return "Yellow";
                case Seat.Blue: return "Blue";
                default: throw new ArgumentOutOfRangeException(nameof(s));
            }
        }

        public static Seat Parse(string text)
        {
            switch (text?.Trim())
            {
                case "Red": return Seat.Red;
                case "Green": return Seat.Green;
                case "Yellow": return Seat.Yellow;
                case "Blue": return Seat.Blue;
                default: throw new FormatException("unknown seat: " + text);
            }
        }

        public static Seat[] ForPlayerCount(int players)
        {
            switch (players)
            {
                case 2: return new[] { Seat.Red, Seat.Yellow };
                case 3: return new[] { Seat.Red, Seat.Green, Seat.Yellow };
                case 4: return new[] { Seat.Red, Seat.Green, Seat.Yellow, Seat.Blue };
                default: throw new ArgumentOutOfRangeException(nameof(players), "players must be 2, 3 or 4");
            }
        }
    }

    // ------------------------------------------------------- token progress

    public static class TokenProgress
    {
        public const int Base = -1;           // in the yard
        public const int LoopMax = 50;        // 0..50 on the shared loop
        public const int ColumnFirst = 51;    // 51..55 private home column
        public const int Finished = 56;       // centre

        public static bool IsOnLoop(int p) => p >= 0 && p <= LoopMax;
    }

    // ---------------------------------------------------------- board layout

    public static class BoardGeometry
    {
        public const int LoopCells = 52;
        public const int TokensPerSeat = 4;

        static readonly int[] SafeCells = BuildSafeCells();

        static int[] BuildSafeCells()
        {
            var set = new int[8];
            for (int s = 0; s < 4; s++)
            {
                set[s] = s * 13;                       // start squares
                set[4 + s] = (s * 13 + 8) % LoopCells; // star squares
            }
            return set;
        }

        public static int StartCell(Seat s) => (int)s * 13;

        public static int GlobalCell(Seat seat, int progress)
        {
            if (progress < 0 || progress > TokenProgress.LoopMax) return -1;
            return (StartCell(seat) + progress) % LoopCells;
        }

        public static bool IsSafeCell(int cell)
        {
            foreach (int c in SafeCells) if (c == cell) return true;
            return false;
        }
    }

    // ----------------------------------------------------------- match config

    public sealed class MatchConfig
    {
        public Seat[] SeatsInPlay { get; }
        public bool Blockades { get; }         // D4 (default on)
        public bool ThreeSixes { get; }        // D2 (default on)
        public bool CaptureExtra { get; }      // D3 (default on)
        public bool Placings { get; }          // D5 (default on)
        public bool TwoPlayerEnd { get; }      // D5 corollary (default on)

        public MatchConfig(Seat[] seats,
                           bool blockades = true, bool threeSixes = true,
                           bool captureExtra = true, bool placings = true,
                           bool twoPlayerEnd = true)
        {
            if (seats == null) throw new ArgumentNullException(nameof(seats));
            if (seats.Length < 2 || seats.Length > 4)
                throw new ArgumentException("2..4 seats required");
            for (int i = 0; i < seats.Length; i++)
                for (int j = i + 1; j < seats.Length; j++)
                    if (seats[i] == seats[j])
                        throw new ArgumentException("seats must be distinct");
            SeatsInPlay = (Seat[])seats.Clone();
            Blockades = blockades;
            ThreeSixes = threeSixes;
            CaptureExtra = captureExtra;
            Placings = placings;
            TwoPlayerEnd = twoPlayerEnd;
        }

        public static MatchConfig Default(int players, bool blockades = true,
                                         bool threeSixes = true, bool captureExtra = true,
                                         bool placings = true, bool twoPlayerEnd = true) =>
            new MatchConfig(Seats.ForPlayerCount(players), blockades, threeSixes,
                            captureExtra, placings, twoPlayerEnd);

        public int SeatOrder(Seat s)
        {
            for (int i = 0; i < SeatsInPlay.Length; i++)
                if (SeatsInPlay[i] == s) return i;
            return -1;
        }
    }

    // ------------------------------------------------------------- turn phase

    public enum TurnPhase { AwaitRoll, AwaitMove, MatchOver }

    // ----------------------------------------------------------- token / state

    public sealed class TokenState
    {
        public Seat Seat { get; }
        public int Index { get; }
        public int Progress { get; set; } = TokenProgress.Base;

        public TokenState(Seat seat, int index) { Seat = seat; Index = index; }

        public TokenState Clone() => new TokenState(Seat, Index) { Progress = Progress };
    }

    public readonly struct Standing : IEquatable<Standing>
    {
        public readonly Seat Seat;
        public readonly int Place;
        public readonly int TokensHome;

        public Standing(Seat seat, int place, int tokensHome)
        { Seat = seat; Place = place; TokensHome = tokensHome; }

        public string Line() => Seats.Name(Seat) + ":" + Place + ":" + TokensHome;

        public static Standing Parse(string text)
        {
            string[] p = text.Split(':');
            if (p.Length != 3) throw new FormatException("bad standing: " + text);
            return new Standing(Seats.Parse(p[0]), int.Parse(p[1]), int.Parse(p[2]));
        }

        public bool Equals(Standing other) => Seat == other.Seat && Place == other.Place && TokensHome == other.TokensHome;
        public override bool Equals(object obj) => obj is Standing s && Equals(s);
        public override int GetHashCode() => (int)Seat * 31 + Place;
        public override string ToString() => Line();
    }

    public sealed class GameState
    {
        public MatchConfig Config { get; }
        public TokenState[] Tokens { get; }             // 16, flat seat-major (ALL seats)
        public Seat CurrentSeat { get; internal set; }
        public TurnPhase Phase { get; internal set; }
        public int LastDie { get; internal set; }
        public int ConsecutiveSixes { get; internal set; }
        public int RollsThisTurn { get; internal set; }
        public int TurnCount { get; internal set; }
        public List<Seat> FinishedOrder { get; internal set; } = new List<Seat>();
        public bool[] Left { get; internal set; } = new bool[4];
        public List<Standing> FinalStandings { get; internal set; }
        internal int[] InternalSnapshotProgress { get; set; }
        internal List<Seat> InternalSnapshotFinished { get; set; }

        public GameState(MatchConfig config)
        {
            Config = config ?? throw new ArgumentNullException(nameof(config));
            Tokens = new TokenState[16];
            foreach (Seat seat in Enum.GetValues(typeof(Seat)))
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                    Tokens[(int)seat * BoardGeometry.TokensPerSeat + i] = new TokenState(seat, i);
            CurrentSeat = config.SeatsInPlay[0];
            Phase = TurnPhase.AwaitRoll;
        }

        public TokenState Token(Seat seat, int index) =>
            Tokens[(int)seat * BoardGeometry.TokensPerSeat + index];

        public int TokensHome(Seat seat)
        {
            int n = 0;
            for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                if (Token(seat, i).Progress == TokenProgress.Finished) n++;
            return n;
        }

        public bool IsFinished(Seat s) => FinishedOrder.Contains(s);
        public bool IsLeft(Seat s) => Left[(int)s];
        public bool IsActive(Seat s) =>
            Config.SeatOrder(s) >= 0 && !IsFinished(s) && !IsLeft(s);

        public int ActiveCount()
        {
            int n = 0;
            foreach (Seat s in Config.SeatsInPlay) if (IsActive(s)) n++;
            return n;
        }

        public Seat? NextActiveSeat()
        {
            Seat[] order = Config.SeatsInPlay;
            int cur = Config.SeatOrder(CurrentSeat);
            for (int step = 1; step <= order.Length; step++)
            {
                Seat cand = order[(cur + step) % order.Length];
                if (IsActive(cand)) return cand;
            }
            return null;
        }

        public int[] FlatProgress()
        {
            var p = new int[Tokens.Length];
            for (int i = 0; i < Tokens.Length; i++) p[i] = Tokens[i].Progress;
            return p;
        }

        // -------------------------------------------------- snapshot (D2 / IL-003)

        internal void TakeSnapshot()
        {
            InternalSnapshotProgress = FlatProgress();
            InternalSnapshotFinished = new List<Seat>(FinishedOrder);
        }

        internal void RestoreSnapshot()
        {
            if (InternalSnapshotProgress == null) throw new InvalidOperationException("no turn snapshot to restore");
            for (int i = 0; i < Tokens.Length; i++) Tokens[i].Progress = InternalSnapshotProgress[i];
            FinishedOrder = new List<Seat>(InternalSnapshotFinished);
        }

        internal void ClearSnapshot() { InternalSnapshotProgress = null; InternalSnapshotFinished = null; }

        // -------------------------------------------------- clone / hash

        public GameState Clone()
        {
            var s = new GameState(Config);
            for (int i = 0; i < Tokens.Length; i++) s.Tokens[i].Progress = Tokens[i].Progress;
            s.CurrentSeat = CurrentSeat;
            s.Phase = Phase;
            s.LastDie = LastDie;
            s.ConsecutiveSixes = ConsecutiveSixes;
            s.RollsThisTurn = RollsThisTurn;
            s.TurnCount = TurnCount;
            s.FinishedOrder = new List<Seat>(FinishedOrder);
            for (int i = 0; i < 4; i++) s.Left[i] = Left[i];
            s.FinalStandings = FinalStandings == null ? null : new List<Standing>(FinalStandings);
            s.InternalSnapshotProgress = InternalSnapshotProgress == null ? null : (int[])InternalSnapshotProgress.Clone();
            s.InternalSnapshotFinished = InternalSnapshotFinished == null ? null : new List<Seat>(InternalSnapshotFinished);
            return s;
        }

        string Canonical()
        {
            var sb = new StringBuilder();
            sb.Append("v1\n");
            sb.Append(string.Join(",", Array.ConvertAll(Config.SeatsInPlay, Seats.Name)));
            sb.Append("\n");
            sb.Append((Config.Blockades ? 1 : 0)).Append(Config.ThreeSixes ? 1 : 0)
              .Append(Config.CaptureExtra ? 1 : 0).Append(Config.Placings ? 1 : 0)
              .Append(Config.TwoPlayerEnd ? 1 : 0);
            sb.Append("\n");
            sb.Append(Seats.Name(CurrentSeat)).Append("\n");
            sb.Append(Phase.ToString()).Append("\n");
            sb.Append(LastDie).Append("\n");
            sb.Append(ConsecutiveSixes).Append("\n");
            sb.Append(RollsThisTurn).Append("\n");
            sb.Append(TurnCount).Append("\n");
            sb.Append(string.Join(",", Array.ConvertAll(FinishedOrder.ToArray(), Seats.Name))).Append("\n");
            var leftList = new List<string>();
            for (int i = 0; i < 4; i++) if (Left[i]) leftList.Add(Seats.Name((Seat)i));
            sb.Append(string.Join(",", leftList)).Append("\n");
            sb.Append(string.Join(",", FlatProgress())).Append("\n");
            if (InternalSnapshotProgress != null)
            {
                sb.Append(string.Join(",", InternalSnapshotProgress)).Append("\n");
                sb.Append(InternalSnapshotFinished != null && InternalSnapshotFinished.Count > 0
                    ? string.Join(",", Array.ConvertAll(InternalSnapshotFinished.ToArray(), Seats.Name))
                    : "-");
            }
            else
            {
                sb.Append("-\n-");
            }
            return sb.ToString();
        }

        public string StateHash()
        {
            uint h = 0x811C9DC5;
            foreach (byte b in Encoding.UTF8.GetBytes(Canonical()))
            {
                h ^= b;
                h *= 0x01000193; // unchecked by default in C# (no overflow checking)
            }
            return h.ToString("x8");
        }
    }

    // ---------------------------------------------------------------- commands

    public abstract class Command
    {
        public abstract string Line();
        public override string ToString() => Line();
    }

    public sealed class RollDiceCommand : Command
    {
        public readonly Seat Seat;
        public readonly int Die;
        public RollDiceCommand(Seat seat, int die) { Seat = seat; Die = die; }
        public override string Line() => "roll " + Seats.Name(Seat) + " " + Die;
    }

    public sealed class MoveTokenCommand : Command
    {
        public readonly Seat Seat;
        public readonly int TokenIndex;
        public MoveTokenCommand(Seat seat, int tokenIndex) { Seat = seat; TokenIndex = tokenIndex; }
        public override string Line() => "move " + Seats.Name(Seat) + " " + TokenIndex;
    }

    public sealed class LeaveMatchCommand : Command
    {
        public readonly Seat Seat;
        public LeaveMatchCommand(Seat seat) { Seat = seat; }
        public override string Line() => "leave " + Seats.Name(Seat);
    }

    public static class CommandParser
    {
        public static Command Parse(string line)
        {
            string[] p = (line ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (p.Length >= 3 && p[0] == "roll")
                return new RollDiceCommand(Seats.Parse(p[1]), int.Parse(p[2]));
            if (p.Length >= 3 && p[0] == "move")
                return new MoveTokenCommand(Seats.Parse(p[1]), int.Parse(p[2]));
            if (p.Length >= 2 && p[0] == "leave")
                return new LeaveMatchCommand(Seats.Parse(p[1]));
            throw new FormatException("bad command line: " + line);
        }
    }

    // ------------------------------------------------------------------ events

    public abstract class GameEvent
    {
        public abstract string Line();
        public override string ToString() => Line();
        public override bool Equals(object obj) =>
            obj != null && obj.GetType() == GetType() && obj.ToString() == Line();
        public override int GetHashCode() => (GetType().Name + Line()).GetHashCode();
    }

    public sealed class DiceRolled : GameEvent
    {
        public readonly Seat Seat; public readonly int Die;
        public readonly int RollIndex; public readonly bool WasExtra;
        public DiceRolled(Seat seat, int die, int rollIndex, bool wasExtra)
        { Seat = seat; Die = die; RollIndex = rollIndex; WasExtra = wasExtra; }
        public override string Line() =>
            "dice " + Seats.Name(Seat) + " " + Die + " " + RollIndex + " " + (WasExtra ? 1 : 0);
    }

    public sealed class NoLegalMove : GameEvent
    {
        public readonly Seat Seat; public readonly int Die;
        public NoLegalMove(Seat seat, int die) { Seat = seat; Die = die; }
        public override string Line() => "nomove " + Seats.Name(Seat) + " " + Die;
    }

    public sealed class TokenMoved : GameEvent
    {
        public readonly Seat Seat; public readonly int TokenIndex;
        public readonly int From; public readonly int To; public readonly bool IsRelease;
        public TokenMoved(Seat seat, int tokenIndex, int from, int to, bool isRelease)
        { Seat = seat; TokenIndex = tokenIndex; From = from; To = to; IsRelease = isRelease; }
        public override string Line() =>
            "move " + Seats.Name(Seat) + ":" + TokenIndex + " " + From + " " + To + " " + (IsRelease ? 1 : 0);
    }

    public sealed class TokenCaptured : GameEvent
    {
        public readonly Seat VictimSeat; public readonly int VictimIndex;
        public readonly Seat AttackerSeat; public readonly int AttackerIndex;
        public readonly int Cell;
        public TokenCaptured(Seat victimSeat, int victimIndex, Seat attackerSeat, int attackerIndex, int cell)
        { VictimSeat = victimSeat; VictimIndex = victimIndex; AttackerSeat = attackerSeat; AttackerIndex = attackerIndex; Cell = cell; }
        public override string Line() =>
            "capture " + Seats.Name(VictimSeat) + ":" + VictimIndex + " "
            + Seats.Name(AttackerSeat) + ":" + AttackerIndex + " " + Cell;
    }

    public sealed class TokenFinished : GameEvent
    {
        public readonly Seat Seat; public readonly int TokenIndex;
        public TokenFinished(Seat seat, int tokenIndex) { Seat = seat; TokenIndex = tokenIndex; }
        public override string Line() => "finish " + Seats.Name(Seat) + ":" + TokenIndex;
    }

    public sealed class PlayerFinished : GameEvent
    {
        public readonly Seat Seat; public readonly int Place;
        public PlayerFinished(Seat seat, int place) { Seat = seat; Place = place; }
        public override string Line() => "playerfinish " + Seats.Name(Seat) + " " + Place;
    }

    public sealed class TurnVoided : GameEvent
    {
        public readonly Seat Seat;
        public readonly int[] RestoredProgress;
        public readonly Seat[] RestoredFinished;
        public TurnVoided(Seat seat, int[] restoredProgress, Seat[] restoredFinished)
        { Seat = seat; RestoredProgress = restoredProgress; RestoredFinished = restoredFinished; }
        public override string Line()
        {
            string fin = RestoredFinished != null && RestoredFinished.Length > 0
                ? string.Join(",", Array.ConvertAll(RestoredFinished, Seats.Name))
                : "-";
            return "voided " + Seats.Name(Seat) + " " + string.Join(",", RestoredProgress) + " " + fin;
        }
    }

    public sealed class TurnEnded : GameEvent
    {
        public readonly Seat NextSeat; public readonly int TurnNumber;
        public TurnEnded(Seat nextSeat, int turnNumber) { NextSeat = nextSeat; TurnNumber = turnNumber; }
        public override string Line() => "turnend " + Seats.Name(NextSeat) + " " + TurnNumber;
    }

    public sealed class PlayerLeft : GameEvent
    {
        public readonly Seat Seat;
        public PlayerLeft(Seat seat) { Seat = seat; }
        public override string Line() => "left " + Seats.Name(Seat);
    }

    public sealed class MatchEnded : GameEvent
    {
        public readonly Standing[] Standings;
        public MatchEnded(Standing[] standings) { Standings = standings; }
        public override string Line()
        {
            var parts = new string[Standings.Length];
            for (int i = 0; i < Standings.Length; i++) parts[i] = Standings[i].Line();
            return "end " + string.Join(",", parts);
        }
    }

    public static class EventParser
    {
        public static GameEvent Parse(string line)
        {
            string[] p = (line ?? "").Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            switch (p[0])
            {
                case "dice":
                    return new DiceRolled(Seats.Parse(p[1]), int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]) == 1);
                case "nomove":
                    return new NoLegalMove(Seats.Parse(p[1]), int.Parse(p[2]));
                case "move":
                {
                    string[] t = p[1].Split(':');
                    return new TokenMoved(Seats.Parse(t[0]), int.Parse(t[1]),
                                          int.Parse(p[2]), int.Parse(p[3]), int.Parse(p[4]) == 1);
                }
                case "capture":
                {
                    string[] v = p[1].Split(':');
                    string[] a = p[2].Split(':');
                    return new TokenCaptured(Seats.Parse(v[0]), int.Parse(v[1]),
                                             Seats.Parse(a[0]), int.Parse(a[1]), int.Parse(p[3]));
                }
                case "finish":
                {
                    string[] t = p[1].Split(':');
                    return new TokenFinished(Seats.Parse(t[0]), int.Parse(t[1]));
                }
                case "playerfinish":
                    return new PlayerFinished(Seats.Parse(p[1]), int.Parse(p[2]));
                case "voided":
                {
                    string[] prog = p[2].Split(',');
                    var pr = new int[prog.Length];
                    for (int i = 0; i < prog.Length; i++) pr[i] = int.Parse(prog[i]);
                    Seat[] fin = p.Length > 3 && p[3] != "-"
                        ? Array.ConvertAll(p[3].Split(','), Seats.Parse)
                        : new Seat[0];
                    return new TurnVoided(Seats.Parse(p[1]), pr, fin);
                }
                case "turnend":
                    return new TurnEnded(Seats.Parse(p[1]), int.Parse(p[2]));
                case "left":
                    return new PlayerLeft(Seats.Parse(p[1]));
                case "end":
                    return new MatchEnded(Array.ConvertAll(p[1].Split(','), Standing.Parse));
                default:
                    throw new FormatException("bad event line: " + line);
            }
        }
    }
}
