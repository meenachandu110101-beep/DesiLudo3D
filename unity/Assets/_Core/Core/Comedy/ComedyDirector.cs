using System.Collections.Generic;

namespace DesiLudo3D.Core
{
    /// <summary>Deterministic comedy line selector — the C# mirror of the
    /// VALIDATED Python reference (ludo/comedy.py, tests/test_comedy.py).
    /// Semantics: category routing (A3 — never male lines with word swaps),
    /// personality enrichment (75% preferred-pool rule), no same line twice
    /// in a row per seat, per-(seat, context) cooldown. Greeting / Victory /
    /// Defeat are cooldown-exempt.</summary>
    public sealed class ComedyDirector
    {
        /// <summary>splitmix64 — bit-exact mirror of ludo.comedy.Rng.
        /// Golden anchors (tests/test_comedy.py::test_splitmix64_golden):
        /// seed 0 -> 0xE220A8397B1DCDAF, 0x6E789E6AA1B965F4,
        /// 0x6C45D188009454F, 0xF88BB8A8724C81EC; seed 1 ->
        /// 0x910A2DEC89025CC1, 0xBEEB8DA1658EEC67, 0xF893A2EEFB32555E,
        /// 0x71C18690EE42C90B.</summary>
        struct Rng
        {
            ulong _s;
            public Rng(ulong seed) { _s = seed; }

            public ulong Next()
            {
                _s += 0x9E3779B97F4A7C15UL;
                ulong z = _s;
                z = (z ^ (z >> 30)) * 0xBF58476D1CE4E5B9UL;
                z = (z ^ (z >> 27)) * 0x94D049BB133111EBUL;
                return z ^ (z >> 31);
            }

            public int NextInt(int n) => (int)(Next() % (ulong)n);
            public double NextFloat() => Next() / 18446744073709551616.0;
        }

        const int HistoryPerSeat = 8;   // remembered distinct ids

        readonly Rng _rng;
        readonly Dictionary<int, List<string>> _history =
            new Dictionary<int, List<string>>();
        readonly Dictionary<long, float> _lastAt = new Dictionary<long, float>();

        public ComedyDirector(ulong seed)
        {
            _rng = new Rng(seed);
        }

        static long Key(int seat, int context) =>
            ((long)seat << 32) | (uint)context;

        public static bool IsCooldownExempt(ComedyContext context) =>
            context == ComedyContext.Greeting
            || context == ComedyContext.Victory
            || context == ComedyContext.Defeat;

        /// <summary>Pick a line, or null when suppressed (cooldown) or the
        /// context is unknown. `now` is caller-supplied seconds — the
        /// director is a pure function of its inputs (testable, replayable).</summary>
        public ComedyLine Select(int seatIndex, ComedyContext context,
                                 CharacterCategory category,
                                 PersonalityStyle personality,
                                 float now, float cooldown = 6f)
        {
            int ctx = (int)context;
            if (ctx < 0 || ctx >= ComedyPools.ContextCount) return null;
            if (!IsCooldownExempt(context)
                && _lastAt.TryGetValue(Key(seatIndex, ctx), out float last)
                && now - last < cooldown)
                return null;

            ComedyLine[] cands = ComedyPools.Pools[ctx][(int)category];

            _history.TryGetValue(seatIndex, out var hist);
            string tail = (hist != null && hist.Count > 0)
                ? hist[hist.Count - 1] : null;

            var pool = new List<ComedyLine>(cands.Length);
            foreach (ComedyLine l in cands)
                if (l.Id != tail) pool.Add(l);
            if (pool.Count == 0)
                foreach (ComedyLine l in cands) pool.Add(l);

            var preferred = new List<ComedyLine>(pool.Count);
            foreach (ComedyLine l in pool)
                foreach (PersonalityStyle p in l.Personalities)
                    if (p == personality) { preferred.Add(l); break; }
            if (preferred.Count > 0 && pool.Count > 1 && _rng.NextFloat() < 0.75)
                pool = preferred;

            ComedyLine line = pool[_rng.NextInt(pool.Count)];
            if (hist == null)
            {
                hist = new List<string>();
                _history[seatIndex] = hist;
            }
            if (hist.Count == 0 || hist[hist.Count - 1] != line.Id)
            {
                hist.Add(line.Id);
                if (hist.Count > HistoryPerSeat) hist.RemoveAt(0);
            }
            _lastAt[Key(seatIndex, ctx)] = now;
            return line;
        }
    }
}
