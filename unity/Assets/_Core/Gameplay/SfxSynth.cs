using System;
using System.Collections.Generic;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Runtime-synthesized sound effects — $0, zero audio assets,
    /// zero licenses (IL-021 friendly). Every clip is generated from a pure
    /// function of time on first use and cached. Gray-box M3 stand-ins for
    /// the M4 designed-sound pass; the API (Play(name)) stays.</summary>
    public static class Sfx
    {
        const int Rate = 22050;
        static readonly Dictionary<string, AudioClip> Cache =
            new Dictionary<string, AudioClip>();
        static AudioSource _src;

        public static void Play(string name, float volume = 0.4f)
        {
            AudioClip clip = Get(name);
            if (clip == null) return;
            EnsureHost();
            if (_src != null) _src.PlayOneShot(clip, volume);
        }

        static void EnsureHost()
        {
            if (_src != null) return;
            var go = new GameObject("SfxSynth");
            _src = go.AddComponent<AudioSource>();
            _src.playOnAwake = false;
            _src.spatialBlend = 0f;
            UnityEngine.Object.DontDestroyOnLoad(go);
        }

        static AudioClip Get(string name)
        {
            if (Cache.TryGetValue(name, out var clip)) return clip;
            clip = Synthesize(name);
            Cache[name] = clip;
            return clip;
        }

        static System.Random _noise = new System.Random(20260909);

        static float Noise() => (float)(_noise.NextDouble() * 2.0 - 1.0);

        static AudioClip Synthesize(string name)
        {
            switch (name)
            {
                case "roll":    return Synth(name, 0.5f, t => Rattle(t));
                case "hop":     return Synth(name, 0.09f, t =>
                                    (float)Math.Sin(2 * Math.PI * (500 + 2500 * t) * t)
                                    * (float)Math.Exp(-t * 45));
                case "release": return Synth(name, 0.2f, t =>
                                    (float)Math.Sin(2 * Math.PI * (280 + 1800 * t) * t)
                                    * (float)Math.Exp(-t * 16));
                case "capture": return Synth(name, 0.55f, t =>
                                    Descend(t) + (t > 0.32 ? Noise() * (float)Math.Exp(-(t - 0.32) * 22) * 0.8f : 0f));
                case "captured": return Synth(name, 0.45f, t =>
                                    Noise() * (float)Math.Sin(Math.PI * t / 0.45)
                                    * (0.35f + 0.4f * t));
                case "finish":  return Synth(name, 0.8f, t => Arp(t, 0.18f, 523.25f, 659.26f, 783.99f, 1046.5f));
                case "victory": return Synth(name, 1.6f, t => Arp(t, 0.21f,
                                    523.25f, 659.26f, 783.99f, 1046.5f, 1318.5f, 1046.5f, 1568f)
                                    * (float)Math.Exp(-t * 0.6));
                case "void":    return Synth(name, 0.85f, t =>
                                    (float)Math.Sin(2 * Math.PI * (160 - 105 * t / 0.85) * t)
                                    * (0.6f + 0.4f * Math.Sin(2 * Math.PI * 6 * t))
                                    * (float)Math.Exp(-t * 2.2));
                case "nomove":  return Synth(name, 0.28f, t =>
                                    (float)Math.Sin(2 * Math.PI * 110 * t) * (float)Math.Exp(-t * 18)
                                    + Noise() * (float)Math.Exp(-t * 40) * 0.3f);
                case "six":     return Synth(name, 0.55f, t =>
                                    Sparkle(t) * (float)Math.Exp(-t * 3.5));
                default:        return null;
            }
        }

        static float Rattle(float t)
        {
            float amp = 0f;
            for (int i = 0; i < 7; i++)
            {
                float start = i * 0.062f;
                if (t >= start)
                    amp += (float)Math.Exp(-(t - start) * 26f) * (1f - i * 0.1f);
            }
            return (Noise() * 0.7f + (float)Math.Sin(2 * Math.PI * 320 * t) * 0.3f) * amp;
        }

        static float Descend(float t)
        {
            float f = 600 - 420 * (t / 0.55f);
            float sq = (float)Math.Sin(2 * Math.PI * f * t) >= 0 ? 1f : -1f;
            return sq * 0.25f * (float)Math.Exp(-t * 3.5f);
        }

        static float Arp(float t, float step, params float[] freqs)
        {
            int i = (int)(t / step);
            if (i < 0) i = 0;
            if (i >= freqs.Length)
            {
                // let the last note ring
                i = freqs.Length - 1;
                t -= step * (freqs.Length - 1);
                step = 0.6f;
                return (float)Math.Sin(2 * Math.PI * freqs[i] * t) * (float)Math.Exp(-t * 3);
            }
            float lt = t - i * step;
            return (float)Math.Sin(2 * Math.PI * freqs[i] * lt) * (float)Math.Exp(-lt * 7);
        }

        static float Sparkle(float t)
        {
            float a = (float)Math.Sin(2 * Math.PI * (600 + 1400 * t) * t);
            float b = (float)Math.Sin(2 * Math.PI * (900 + 1800 * t) * (t - 0.12f))
                      * (t > 0.12f ? 1f : 0f);
            return (a * 0.6f + b * 0.4f) * 0.8f;
        }

        static AudioClip Synth(string name, float seconds, Func<float, float> f)
        {
            int count = Mathf.CeilToInt(seconds * Rate);
            var data = new float[count];
            for (int i = 0; i < count; i++)
            {
                float t = i / (float)Rate;
                data[i] = Mathf.Clamp(f(t), -1f, 1f) * 0.9f;
            }
            var clip = AudioClip.Create("sfx_" + name, count, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
