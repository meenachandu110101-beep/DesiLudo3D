using System;
using System.Collections.Generic;
using DesiLudo3D.Rules;

namespace DesiLudo3D.Core
{
    /// <summary>Local fair dice: seeded System.Random (deterministic per
    /// seed — matches the reference engine's determinism requirement for
    /// offline play; the seed is recorded in the match log).</summary>
    public sealed class SeededRandomDiceProvider : IDiceProvider
    {
        readonly Random _rng;
        public int LastValue { get; private set; }

        public SeededRandomDiceProvider(int seed)
        {
            _rng = new Random(seed);
        }

        public int RollDie()
        {
            LastValue = _rng.Next(1, 7);
            return LastValue;
        }
    }

    /// <summary>Fixed sequence dice for tests and scripted scenarios;
    /// cycles (or throws) when exhausted.</summary>
    public sealed class FixedSequenceDiceProvider : IDiceProvider
    {
        readonly int[] _values;
        int _index;

        public FixedSequenceDiceProvider(params int[] values)
        {
            if (values == null || values.Length == 0)
                throw new ArgumentException("at least one value required", nameof(values));
            _values = values;
        }

        public int RollDie()
        {
            int v = _values[_index];
            _index = (_index + 1) % _values.Length;
            return v;
        }
    }

    /// <summary>M1 placeholder bot (M4 replaces with Easy/Medium/Hard tiers
    /// behind the same IOpponentAgent seam). Prefers, in order: captures,
    /// releases, finishing a token, then the leading token.</summary>
    public sealed class RandomAgent : IOpponentAgent
    {
        readonly Random _rng;
        public string Name => "RandomBot (M1 placeholder)";

        public RandomAgent(int seed = 0) { _rng = new Random(seed); }

        public LegalMove ChooseMove(GameState state, IReadOnlyList<LegalMove> moves)
        {
            if (moves == null || moves.Count == 0) throw new ArgumentException("no moves to choose from");
            foreach (LegalMove m in moves) if (m.Captures) return m;
            foreach (LegalMove m in moves) if (m.IsRelease) return m;
            foreach (LegalMove m in moves) if (m.To == TokenProgress.Finished) return m;
            LegalMove best = moves[0];
            foreach (LegalMove m in moves) if (m.To > best.To) best = m;
            if (_rng.Next(4) == 0 && moves.Count > 1) return moves[_rng.Next(moves.Count)]; // some spice
            return best;
        }
    }
}
