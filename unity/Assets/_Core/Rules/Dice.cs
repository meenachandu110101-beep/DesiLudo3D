using System;
using System.Security.Cryptography;
using System.Text;

namespace DesiLudo3D.Rules
{
    /// <summary>Commit–reveal fair dice (spec §9.3 / IL-012).
    /// Value = SHA-256(seed0 || seed1 || ...) in seat order, first 4 bytes
    /// big-endian -> uint32 -> v % 6 + 1. Neither party can bias the outcome
    /// once the other's commitment is fixed; every participant can verify it.</summary>
    public static class CommitRevealDice
    {
        public static byte[] CreateSeed(int nbytes = 16)
        {
            if (nbytes < 8) throw new ArgumentOutOfRangeException(nameof(nbytes));
            var seed = new byte[nbytes];
            using (var rng = RandomNumberGenerator.Create())
                rng.GetBytes(seed);
            return seed;
        }

        public static string Commitment(byte[] seed)
        {
            using (SHA256 sha = SHA256.Create())
                return Hex(sha.ComputeHash(seed));
        }

        public static int CombineDieValue(params byte[][] seeds)
        {
            if (seeds == null || seeds.Length < 2)
                throw new ArgumentException("commit-reveal needs at least two seeds");
            using (SHA256 sha = SHA256.Create())
            {
                foreach (byte[] s in seeds) sha.TransformBlock(s, 0, s.Length, null, 0);
                sha.TransformFinalBlock(new byte[0], 0, 0);
                byte[] digest = sha.Hash;
                uint v = ((uint)digest[0] << 24) | ((uint)digest[1] << 16)
                       | ((uint)digest[2] << 8) | (uint)digest[3];
                return (int)(v % 6) + 1;
            }
        }

        public static bool Verify(byte[][] seeds, string[] commits)
        {
            if (seeds == null || commits == null || seeds.Length != commits.Length)
                return false;
            for (int i = 0; i < seeds.Length; i++)
                if (Commitment(seeds[i]) != commits[i]) return false;
            return true;
        }

        static string Hex(byte[] data)
        {
            var sb = new StringBuilder(data.Length * 2);
            foreach (byte b in data) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }
    }
}
