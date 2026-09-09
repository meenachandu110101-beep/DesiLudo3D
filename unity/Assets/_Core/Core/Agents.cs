using System;
using System.Collections.Generic;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    /// <summary>Bot difficulty tiers (M4).</summary>
    public enum BotTier
    {
        Easy = 0,     // uniform random
        Medium = 1,   // greedy core score, 25% noise, no danger awareness
        Hard = 2,     // full score incl. threat terms, 5% noise
    }

    /// <summary>Tiered heuristic opponent — the C# mirror of the VALIDATED
    /// Python reference (ludo/agent.py, tests/test_agents.py: preferences,
    /// danger avoidance, HARD-beats-EASY ordering, determinism). Deterministic
    /// per seed; identical scoring constants.</summary>
    public sealed class HeuristicAgent : IOpponentAgent
    {
        readonly BotTier _tier;
        readonly Random _rng;

        public string Name => "Bot-" + _tier;

        public HeuristicAgent(BotTier tier, int seed = 0)
        {
            _tier = tier;
            _rng = new Random(seed * 7919 + 17);
        }

        public LegalMove ChooseMove(GameState state, IReadOnlyList<LegalMove> moves)
        {
            if (moves == null || moves.Count == 0)
                throw new ArgumentException("no moves to choose from", nameof(moves));
            if (moves.Count == 1) return moves[0];
            if (_tier == BotTier.Easy) return moves[_rng.Next(moves.Count)];

            double eps = _tier == BotTier.Medium ? 0.25 : 0.05;
            if (_rng.NextDouble() < eps) return moves[_rng.Next(moves.Count)];

            Seat seat = state.CurrentSeat;
            int best = 0;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < moves.Count; i++)
            {
                double s = (_tier == BotTier.Medium
                        ? BaseScore(state, seat, moves[i])
                        : FullScore(state, seat, moves[i]))
                    + _rng.NextDouble() * 0.01;   // stable tiebreak
                if (s > bestScore) { bestScore = s; best = i; }
            }
            return moves[best];
        }

        /// <summary>Opponent tokens that could capture a token on `cell` with
        /// a 1..6 roll (loop proximity only — a heuristic, not an oracle).</summary>
        public static int ThreatCount(GameState state, Seat seat, int cell)
        {
            if (cell < 0) return 0;
            int count = 0;
            foreach (Seat s in state.Config.SeatsInPlay)
            {
                if (s == seat) continue;
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                {
                    int p = state.Token(s, i).Progress;
                    if (p < 0 || p > TokenProgress.LoopMax) continue;
                    int oc = BoardGeometry.GlobalCell(s, p);
                    for (int k = 1; k <= 6; k++)
                    {
                        if ((oc + k) % BoardGeometry.LoopCells == cell)
                        {
                            count++;
                            break;
                        }
                    }
                }
            }
            return count;
        }

        /// <summary>Core preferences shared by MEDIUM and HARD.</summary>
        public static double BaseScore(GameState state, Seat seat, LegalMove m)
        {
            double score = 0.0;
            if (m.Captures) score += 100.0 * m.Victims.Length;
            if (m.To == TokenProgress.Finished) score += 90.0;
            if (m.IsRelease) score += 40.0;
            if (TokenProgress.IsOnLoop(m.To) && BoardGeometry.IsSafeCell(
                    BoardGeometry.GlobalCell(seat, m.To)))
                score += 25.0;
            if (m.To > TokenProgress.LoopMax && m.To != TokenProgress.Finished)
                score += 15.0;                        // home column: immune
            score += 2.0 * m.To;                      // advance the leader
            return score;
        }

        /// <summary>BaseScore + threat awareness (HARD).</summary>
        public static double FullScore(GameState state, Seat seat, LegalMove m)
        {
            double score = BaseScore(state, seat, m);
            if (TokenProgress.IsOnLoop(m.To))
            {
                int toCell = BoardGeometry.GlobalCell(seat, m.To);
                if (!BoardGeometry.IsSafeCell(toCell))
                    score -= 20.0 * ThreatCount(state, seat, toCell);   // landing in danger
            }
            if (TokenProgress.IsOnLoop(m.From))
            {
                int frmCell = BoardGeometry.GlobalCell(seat, m.From);
                if (!BoardGeometry.IsSafeCell(frmCell))
                    score += 10.0 * ThreatCount(state, seat, frmCell);  // escaping danger
            }
            return score;
        }
    }
}
