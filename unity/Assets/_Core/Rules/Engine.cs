using System;
using System.Collections.Generic;

namespace DesiLudo3D.Rules
{
    public sealed class LegalMove
    {
        public Seat Seat { get; }
        public int TokenIndex { get; }
        public int From { get; }
        public int To { get; }
        public bool IsRelease { get; }
        public bool Captures { get; }
        public (Seat Seat, int Index)[] Victims { get; }

        public LegalMove(Seat seat, int tokenIndex, int from, int to,
                         bool isRelease, bool captures, (Seat, int)[] victims)
        {
            Seat = seat; TokenIndex = tokenIndex; From = from; To = to;
            IsRelease = isRelease; Captures = captures; Victims = victims ?? new (Seat, int)[0];
        }
    }

    public sealed class ApplyResult
    {
        public readonly GameState NewState;
        public readonly GameEvent[] Events;
        public readonly string Error;
        public bool Ok => Error == null;

        ApplyResult(GameState state, GameEvent[] events, string error)
        { NewState = state; Events = events; Error = error; }

        public static ApplyResult Ok_(GameState s, List<GameEvent> ev) =>
            new ApplyResult(s, ev.ToArray(), null);

        public static ApplyResult Fail(string error) =>
            new ApplyResult(null, new GameEvent[0], error);
    }

    public static class RulesEngine
    {
        public static GameState NewMatch(MatchConfig config) => new GameState(config);

        // ------------------------------------------------------------- queries

        static Dictionary<int, Dictionary<Seat, List<int>>> LoopTokensByCell(GameState state)
        {
            var occ = new Dictionary<int, Dictionary<Seat, List<int>>>();
            foreach (Seat seat in state.Config.SeatsInPlay)
            {
                for (int idx = 0; idx < BoardGeometry.TokensPerSeat; idx++)
                {
                    int p = state.Token(seat, idx).Progress;
                    if (p >= 0 && p <= TokenProgress.LoopMax)
                    {
                        int cell = BoardGeometry.GlobalCell(seat, p);
                        if (!occ.TryGetValue(cell, out var bySeat))
                        {
                            bySeat = new Dictionary<Seat, List<int>>();
                            occ[cell] = bySeat;
                        }
                        if (!bySeat.TryGetValue(seat, out var list))
                        {
                            list = new List<int>();
                            bySeat[seat] = list;
                        }
                        list.Add(idx);
                    }
                }
            }
            return occ;
        }

        static HashSet<int> BlockadedCellsFor(GameState state, Seat mover)
        {
            var blocked = new HashSet<int>();
            foreach (var kv in LoopTokensByCell(state))
                foreach (var kv2 in kv.Value)
                    if (kv2.Key != mover && kv2.Value.Count >= 2)
                        blocked.Add(kv.Key);
            return blocked;
        }

        public static List<LegalMove> GetLegalMoves(GameState state, Seat seat, int die)
        {
            var moves = new List<LegalMove>();
            if (state == null || state.Phase == TurnPhase.MatchOver) return moves;
            if (die < 1 || die > 6) return moves;

            MatchConfig cfg = state.Config;
            HashSet<int> blockaded = cfg.Blockades ? BlockadedCellsFor(state, seat) : null;
            Dictionary<int, Dictionary<Seat, List<int>>> occ = LoopTokensByCell(state);

            for (int idx = 0; idx < BoardGeometry.TokensPerSeat; idx++)
            {
                int p = state.Token(seat, idx).Progress;
                if (p == TokenProgress.Finished) continue;

                if (p == TokenProgress.Base)
                {
                    if (die == 6)
                    {
                        int cell = BoardGeometry.StartCell(seat);
                        if (cfg.Blockades && blockaded.Contains(cell)) continue;
                        moves.Add(new LegalMove(seat, idx, TokenProgress.Base, 0, true, false, null));
                    }
                    continue;
                }

                int to = p + die;
                if (to > TokenProgress.Finished) continue; // overshoot illegal

                if (cfg.Blockades && p <= TokenProgress.LoopMax)
                {
                    int hi = Math.Min(to, TokenProgress.LoopMax);
                    bool pathBlocked = false;
                    for (int pr = p + 1; pr <= hi; pr++)
                    {
                        if (blockaded.Contains(BoardGeometry.GlobalCell(seat, pr)))
                        {
                            pathBlocked = true;
                            break;
                        }
                    }
                    if (pathBlocked) continue;
                }

                bool captures = false;
                var victims = new List<(Seat, int)>();
                if (to <= TokenProgress.LoopMax)
                {
                    int cell = BoardGeometry.GlobalCell(seat, to);
                    if (!BoardGeometry.IsSafeCell(cell) && occ.TryGetValue(cell, out var bySeat))
                    {
                        foreach (Seat s in state.Config.SeatsInPlay)   // stable order (mirror)
                        {
                            if (s == seat) continue;
                            if (bySeat.TryGetValue(s, out var idxs))
                                foreach (int i in idxs) victims.Add((s, i));
                        }
                        captures = victims.Count > 0;
                    }
                }
                moves.Add(new LegalMove(seat, idx, p, to, false, captures, victims.ToArray()));
            }
            return moves;
        }

        // ------------------------------------------------------------- apply

        public static ApplyResult Apply(GameState state, Command command)
        {
            if (state == null) return ApplyResult.Fail("state is null");
            if (command == null) return ApplyResult.Fail("command is null");
            if (command is RollDiceCommand roll) return ApplyRoll(state, roll);
            if (command is MoveTokenCommand move) return ApplyMove(state, move);
            if (command is LeaveMatchCommand leave) return ApplyLeave(state, leave);
            return ApplyResult.Fail("unknown command type: " + command.GetType().Name);
        }

        static ApplyResult ApplyRoll(GameState state, RollDiceCommand cmd)
        {
            if (state.Phase == TurnPhase.MatchOver) return ApplyResult.Fail("match is over");
            if (state.Phase != TurnPhase.AwaitRoll) return ApplyResult.Fail("a move is pending; move before rolling");
            if (cmd.Seat != state.CurrentSeat)
                return ApplyResult.Fail("not " + Seats.Name(cmd.Seat) + "'s turn (current: " + Seats.Name(state.CurrentSeat) + ")");
            if (cmd.Die < 1 || cmd.Die > 6) return ApplyResult.Fail("die must be 1..6");

            GameState s = state.Clone();
            var ev = new List<GameEvent>();

            if (s.RollsThisTurn == 0) s.TakeSnapshot();               // IL-003
            s.RollsThisTurn += 1;
            s.ConsecutiveSixes = cmd.Die == 6 ? s.ConsecutiveSixes + 1 : 0;
            ev.Add(new DiceRolled(cmd.Seat, cmd.Die, s.RollsThisTurn, s.RollsThisTurn > 1));

            // D2: third consecutive six voids the entire turn (IL-003).
            if (cmd.Die == 6 && s.ConsecutiveSixes >= 3 && s.Config.ThreeSixes)
            {
                s.RestoreSnapshot();
                s.ConsecutiveSixes = 0;
                s.RollsThisTurn = 0;
                s.LastDie = 0;
                ev.Add(new TurnVoided(cmd.Seat, s.FlatProgress(), s.FinishedOrder.ToArray()));
                EndTurn(s, ev);
                return ApplyResult.Ok_(s, ev);
            }

            s.LastDie = cmd.Die;
            List<LegalMove> moves = GetLegalMoves(s, cmd.Seat, cmd.Die);
            if (moves.Count == 0)
            {
                ev.Add(new NoLegalMove(cmd.Seat, cmd.Die));
                if (cmd.Die == 6)
                {
                    s.Phase = TurnPhase.AwaitRoll;                    // IL-004
                    return ApplyResult.Ok_(s, ev);
                }
                EndTurn(s, ev);
                return ApplyResult.Ok_(s, ev);
            }

            s.Phase = TurnPhase.AwaitMove;
            return ApplyResult.Ok_(s, ev);
        }

        static ApplyResult ApplyMove(GameState state, MoveTokenCommand cmd)
        {
            if (state.Phase == TurnPhase.MatchOver) return ApplyResult.Fail("match is over");
            if (state.Phase != TurnPhase.AwaitMove) return ApplyResult.Fail("no move pending; roll first");
            if (cmd.Seat != state.CurrentSeat)
                return ApplyResult.Fail("not " + Seats.Name(cmd.Seat) + "'s turn (current: " + Seats.Name(state.CurrentSeat) + ")");
            if (cmd.TokenIndex < 0 || cmd.TokenIndex >= BoardGeometry.TokensPerSeat)
                return ApplyResult.Fail("token index out of range");

            List<LegalMove> moves = GetLegalMoves(state, cmd.Seat, state.LastDie);
            LegalMove sel = null;
            foreach (LegalMove m in moves) if (m.TokenIndex == cmd.TokenIndex) { sel = m; break; }
            if (sel == null)
                return ApplyResult.Fail("no legal move for token " + cmd.TokenIndex + " with die " + state.LastDie);

            GameState s = state.Clone();
            var ev = new List<GameEvent>();

            s.Token(cmd.Seat, cmd.TokenIndex).Progress = sel.To;
            ev.Add(new TokenMoved(cmd.Seat, cmd.TokenIndex, sel.From, sel.To, sel.IsRelease));

            int cell = BoardGeometry.GlobalCell(cmd.Seat, sel.To);
            foreach (var (vs, vi) in sel.Victims)
            {
                s.Token(vs, vi).Progress = TokenProgress.Base;
                ev.Add(new TokenCaptured(vs, vi, cmd.Seat, cmd.TokenIndex, cell));
            }
            bool captured = sel.Victims.Length > 0;

            if (sel.To == TokenProgress.Finished)
            {
                ev.Add(new TokenFinished(cmd.Seat, cmd.TokenIndex));
                if (s.TokensHome(cmd.Seat) == BoardGeometry.TokensPerSeat)
                {
                    int place = s.FinishedOrder.Count + 1;
                    s.FinishedOrder.Add(cmd.Seat);
                    ev.Add(new PlayerFinished(cmd.Seat, place));
                }
            }

            if (ShouldEnd(s))
            {
                EndMatch(s, ev);
                return ApplyResult.Ok_(s, ev);
            }

            bool extra = !s.IsFinished(cmd.Seat) &&
                         (state.LastDie == 6 || (captured && s.Config.CaptureExtra));
            s.LastDie = 0;
            s.Phase = TurnPhase.AwaitRoll;
            if (!extra) EndTurn(s, ev);
            return ApplyResult.Ok_(s, ev);
        }

        static ApplyResult ApplyLeave(GameState state, LeaveMatchCommand cmd)
        {
            if (state.Phase == TurnPhase.MatchOver) return ApplyResult.Fail("match is over");
            if (state.Config.SeatOrder(cmd.Seat) < 0)
                return ApplyResult.Fail("seat " + Seats.Name(cmd.Seat) + " is not in this match");
            if (state.IsLeft(cmd.Seat))
                return ApplyResult.Fail(Seats.Name(cmd.Seat) + " has already left");

            GameState s = state.Clone();
            var ev = new List<GameEvent>();
            s.Left[(int)cmd.Seat] = true;
            ev.Add(new PlayerLeft(cmd.Seat));

            if (ShouldEnd(s))
            {
                EndMatch(s, ev);
                return ApplyResult.Ok_(s, ev);
            }
            if (cmd.Seat == s.CurrentSeat) EndTurn(s, ev);
            return ApplyResult.Ok_(s, ev);
        }

        // ------------------------------------------------------------- internals

        static bool ShouldEnd(GameState state)
        {
            if (state.Phase == TurnPhase.MatchOver) return true;
            if (state.FinishedOrder.Count > 0)
            {
                if (!state.Config.Placings) return true;
                if (state.Config.SeatsInPlay.Length == 2 && state.Config.TwoPlayerEnd) return true;
            }
            return state.ActiveCount() <= 1;
        }

        static void EndTurn(GameState state, List<GameEvent> ev)
        {
            Seat? nxt = state.NextActiveSeat();
            if (nxt == null)
            {
                EndMatch(state, ev);
                return;
            }
            state.TurnCount += 1;
            state.CurrentSeat = nxt.Value;
            state.Phase = TurnPhase.AwaitRoll;
            state.LastDie = 0;
            state.ConsecutiveSixes = 0;
            state.RollsThisTurn = 0;
            state.ClearSnapshot();
            ev.Add(new TurnEnded(nxt.Value, state.TurnCount));
        }

        static void EndMatch(GameState state, List<GameEvent> ev)
        {
            var standings = new List<Standing>();
            int place = 1;
            foreach (Seat seat in state.FinishedOrder)
            {
                standings.Add(new Standing(seat, place, BoardGeometry.TokensPerSeat));
                place++;
            }
            var remaining = new List<Seat>();
            foreach (Seat s in state.Config.SeatsInPlay)
                if (!state.FinishedOrder.Contains(s)) remaining.Add(s);
            // IL-011: tokens-home desc, then non-leavers above leavers, then seat order.
            remaining.Sort((a, b) =>
            {
                int c = -state.TokensHome(a).CompareTo(state.TokensHome(b));
                if (c != 0) return c;
                c = state.IsLeft(a).CompareTo(state.IsLeft(b));
                if (c != 0) return c;
                return state.Config.SeatOrder(a).CompareTo(state.Config.SeatOrder(b));
            });
            foreach (Seat s in remaining)
            {
                standings.Add(new Standing(s, place, state.TokensHome(s)));
                place++;
            }
            state.FinalStandings = standings;
            state.Phase = TurnPhase.MatchOver;
            state.LastDie = 0;
            state.ConsecutiveSixes = 0;
            state.RollsThisTurn = 0;
            state.ClearSnapshot();
            ev.Add(new MatchEnded(standings.ToArray()));
        }
    }
}
