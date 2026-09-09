using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>M7 hardening — device-tier performance tuner (spec §10 +
    /// §8.2). Probes the device ONCE at startup and applies the quality
    /// matrix: SAME art direction on every tier, only the levers move
    /// (mip limit / shadows / AA / fps target). Player-overridable for
    /// testing via PlayerPrefs "desiludo.tier" (0/1/2).</summary>
    public static class PerformanceTuner
    {
        public enum Tier { Low = 0, Mid = 1, High = 2 }

        /// <summary>Probe the device tier (T1/T2/T3 of spec §10).</summary>
        public static Tier Probe()
        {
            int forced = PlayerPrefs.GetInt("desiludo.tier", -1);
            if (forced >= 0 && forced <= 2) return (Tier)forced;

            // systemMemorySize = MB, approximates the RAM class
            int ram = SystemInfo.systemMemorySize;
            Tier tier;
            if (ram <= 0) tier = Tier.Mid;                  // unknown → safe
            else if (ram < 4000) tier = Tier.Low;           // ~3 GB (₹7–10k)
            else if (ram < 7000) tier = Tier.Mid;           // 4–6 GB
            else tier = Tier.High;                          // flagships

            // weak GPU downgrades one step (fill-rate driven game)
            if (SystemInfo.graphicsMemorySize > 0
                && SystemInfo.graphicsMemorySize < 1024 && tier == Tier.Mid)
                tier = Tier.Low;
            return tier;
        }

        /// <summary>Apply the §8.2 quality matrix for the probed tier.</summary>
        public static void Apply()
        {
            Tier tier = Probe();
            switch (tier)
            {
                case Tier.Low:
                    Application.targetFrameRate = 30;
                    QualitySettings.globalTextureMipmapLimit = 1;   // ½-res
                    QualitySettings.shadows = ShadowQuality.HardOnly;
                    QualitySettings.shadowCascades = 1;
                    QualitySettings.antiAliasing = 0;
                    break;
                case Tier.Mid:
                    Application.targetFrameRate = 60;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowCascades = 2;
                    QualitySettings.antiAliasing = 2;
                    break;
                default:
                    Application.targetFrameRate = 60;
                    QualitySettings.globalTextureMipmapLimit = 0;
                    QualitySettings.shadows = ShadowQuality.All;
                    QualitySettings.shadowCascades = 4;
                    QualitySettings.antiAliasing = 4;
                    break;
            }
            Debug.Log("[DesiLudo3D] PerformanceTuner: tier " + tier
                + " (ram " + SystemInfo.systemMemorySize + " MB, gpu "
                + SystemInfo.graphicsMemorySize + " MB) → "
                + Application.targetFrameRate + " fps, mipLimit "
                + QualitySettings.globalTextureMipmapLimit);
        }
    }
}
