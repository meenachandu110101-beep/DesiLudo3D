using System;
using System.Collections.Generic;
using System.Text;

namespace DesiLudo3D.Rules
{
    /// <summary>Deterministic text codecs — the cross-implementation contract
    /// (docs/RULES-FORMAT.md). Must stay byte-identical with the reference engine.</summary>
    public static class MatchCodec
    {
        public const string StateHeader = "DESI-LUDO-STATE v1";
        public const string CommandLogHeader = "DESI-LUDO-CMDLOG v1";
        public const string EventLogHeader = "DESI-LUDO-EVENTLOG v1";

        // ------------------------------------------------------------ state

        public static string SaveState(GameState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine(StateHeader);
            sb.Append("seats=").AppendLine(string.Join(",", Array.ConvertAll(state.Config.SeatsInPlay, Seats.Name)));
            sb.Append("blockades=").AppendLine(state.Config.Blockades ? "1" : "0");
            sb.Append("threesixes=").AppendLine(state.Config.ThreeSixes ? "1" : "0");
            sb.Append("capturex=").AppendLine(state.Config.CaptureExtra ? "1" : "0");
            sb.Append("placings=").AppendLine(state.Config.Placings ? "1" : "0");
            sb.Append("twopend=").AppendLine(state.Config.TwoPlayerEnd ? "1" : "0");
            sb.Append("current=").AppendLine(Seats.Name(state.CurrentSeat));
            sb.Append("phase=").AppendLine(state.Phase.ToString());
            sb.Append("lastdie=").AppendLine(state.LastDie.ToString());
            sb.Append("consec=").AppendLine(state.ConsecutiveSixes.ToString());
            sb.Append("rolls=").AppendLine(state.RollsThisTurn.ToString());
            sb.Append("turns=").AppendLine(state.TurnCount.ToString());
            sb.Append("finished=").AppendLine(string.Join(",", Array.ConvertAll(state.FinishedOrder.ToArray(), Seats.Name)));
            var left = new List<string>();
            for (int i = 0; i < 4; i++) if (state.Left[i]) left.Add(Seats.Name((Seat)i));
            sb.Append("left=").AppendLine(string.Join(",", left));

            var progParts = new List<string>();
            foreach (Seat seat in Enum.GetValues(typeof(Seat)))
            {
                var vals = new int[BoardGeometry.TokensPerSeat];
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                    vals[i] = state.Token(seat, i).Progress;
                progParts.Add(Seats.Name(seat) + ":" + string.Join(",", vals));
            }
            sb.Append("progress=").AppendLine(string.Join(";", progParts));

            if (state.InternalSnapshotProgress != null)
            {
                sb.Append("snap=").AppendLine(string.Join(",", state.InternalSnapshotProgress));
                sb.Append("snapfin=").AppendLine(state.InternalSnapshotFinished != null && state.InternalSnapshotFinished.Count > 0
                    ? string.Join(",", Array.ConvertAll(state.InternalSnapshotFinished.ToArray(), Seats.Name))
                    : "");
            }
            if (state.FinalStandings != null)
            {
                var st = new string[state.FinalStandings.Count];
                for (int i = 0; i < state.FinalStandings.Count; i++) st[i] = state.FinalStandings[i].Line();
                sb.Append("standings=").Append(string.Join(",", st)).Append("\n");
            }
            return sb.ToString();
        }

        public static GameState LoadState(string text)
        {
            string[] raw = (text ?? "").Split('\n');
            var lines = new List<string>();
            foreach (string l in raw)
            {
                string t = l.TrimEnd('\r');
                if (t.Length > 0) lines.Add(t);
            }
            if (lines.Count == 0 || lines[0] != StateHeader)
                throw new FormatException("bad state header");

            var kv = new Dictionary<string, string>();
            for (int i = 1; i < lines.Count; i++)
            {
                int eq = lines[i].IndexOf('=');
                if (eq < 0) throw new FormatException("bad state line: " + lines[i]);
                string k = lines[i].Substring(0, eq);
                if (kv.ContainsKey(k)) throw new FormatException("duplicate state key: " + k);
                kv[k] = lines[i].Substring(eq + 1);
            }

            string Need(string key)
            {
                if (!kv.ContainsKey(key)) throw new FormatException("missing state key: " + key);
                return kv[key];
            }
            Seat[] SeatList(string text) => text.Length == 0
                ? new Seat[0]
                : Array.ConvertAll(text.Split(','), Seats.Parse);
            int[] IntList(string text)
            {
                if (text.Length == 0) return new int[0];
                string[] p = text.Split(',');
                var v = new int[p.Length];
                for (int i = 0; i < p.Length; i++) v[i] = int.Parse(p[i]);
                return v;
            }

            var cfg = new MatchConfig(
                SeatList(Need("seats")),
                blockades: Need("blockades") == "1",
                threeSixes: Need("threesixes") == "1",
                captureExtra: Need("capturex") == "1",
                placings: Need("placings") == "1",
                twoPlayerEnd: Need("twopend") == "1");

            var state = new GameState(cfg);
            state.CurrentSeat = Seats.Parse(Need("current"));
            state.Phase = (TurnPhase)Enum.Parse(typeof(TurnPhase), Need("phase"));
            state.LastDie = int.Parse(Need("lastdie"));
            state.ConsecutiveSixes = int.Parse(Need("consec"));
            state.RollsThisTurn = int.Parse(Need("rolls"));
            state.TurnCount = int.Parse(Need("turns"));
            state.FinishedOrder = new List<Seat>(SeatList(Need("finished")));
            Seat[] leftSeats = SeatList(Need("left"));
            foreach (Seat s in leftSeats) state.Left[(int)s] = true;

            foreach (string part in Need("progress").Split(';'))
            {
                int colon = part.IndexOf(':');
                Seat seat = Seats.Parse(part.Substring(0, colon));
                int[] vals = IntList(part.Substring(colon + 1));
                if (vals.Length != BoardGeometry.TokensPerSeat)
                    throw new FormatException("progress must have 4 values per seat");
                for (int i = 0; i < vals.Length; i++) state.Token(seat, i).Progress = vals[i];
            }

            if (kv.ContainsKey("snap"))
            {
                int[] snap = IntList(kv["snap"]);
                if (snap.Length != 16) throw new FormatException("snap must have 16 values");
                state.InternalSnapshotProgress = snap;
                state.InternalSnapshotFinished = new List<Seat>(SeatList(kv.ContainsKey("snapfin") ? kv["snapfin"] : ""));
            }
            if (kv.ContainsKey("standings") && kv["standings"].Length > 0)
            {
                var standings = new List<Standing>();
                foreach (string t in kv["standings"].Split(',')) standings.Add(Standing.Parse(t));
                state.FinalStandings = standings;
            }
            return state;
        }

        // ------------------------------------------------------------ logs

        public static string SaveCommands(IEnumerable<Command> commands)
        {
            var sb = new StringBuilder();
            sb.AppendLine(CommandLogHeader);
            foreach (Command c in commands) sb.AppendLine(c.Line());
            return sb.ToString();
        }

        public static List<Command> LoadCommands(string text)
        {
            var lines = SplitBody(text, CommandLogHeader);
            var result = new List<Command>();
            foreach (string l in lines) result.Add(CommandParser.Parse(l));
            return result;
        }

        public static string SaveEvents(IEnumerable<GameEvent> events)
        {
            var sb = new StringBuilder();
            sb.AppendLine(EventLogHeader);
            foreach (GameEvent e in events) sb.AppendLine(e.Line());
            return sb.ToString();
        }

        public static List<GameEvent> LoadEvents(string text)
        {
            var lines = SplitBody(text, EventLogHeader);
            var result = new List<GameEvent>();
            foreach (string l in lines) result.Add(EventParser.Parse(l));
            return result;
        }

        static List<string> SplitBody(string text, string header)
        {
            string[] raw = (text ?? "").Split('\n');
            var lines = new List<string>();
            foreach (string l in raw)
            {
                string t = l.TrimEnd('\r');
                if (t.Length > 0) lines.Add(t);
            }
            if (lines.Count == 0 || lines[0] != header)
                throw new FormatException("bad log header");
            return lines.GetRange(1, lines.Count - 1);
        }
    }
}
