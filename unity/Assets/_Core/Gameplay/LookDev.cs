using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Procedural premium look pass (M3, spec §8/D7): hand-painted
    /// cell textures, wood frame, gold accents, festival gradient sky and a
    /// URP volume (bloom/vignette/colour grading) — ALL generated at runtime,
    /// zero authored assets (IL-021), $0. Falls back gracefully under the
    /// built-in pipeline (IL-019).</summary>
    public static class LookDev
    {
        static readonly Dictionary<string, Texture2D> TextureCache =
            new Dictionary<string, Texture2D>();

        public static void Apply(BoardBuilder board, Camera camera)
        {
            if (board != null && board.BoardRoot != null) ApplyBoard(board.BoardRoot);
            if (camera != null)
            {
                ApplySky(camera);
                ApplyVolume(camera);
            }
        }

        // ------------------------------------------------------------ board

        static void ApplyBoard(Transform root)
        {
            foreach (Renderer r in root.GetComponentsInChildren<Renderer>())
            {
                // never restyle tokens/medallions (their materials are identity)
                if (r.GetComponentInParent<TokenView>() != null) continue;

                Material mat = r.sharedMaterial;
                if (mat == null) continue;
                Color c = mat.HasProperty("_BaseColor") ? mat.GetColor("_BaseColor")
                        : mat.HasProperty("_Color") ? mat.GetColor("_Color")
                        : Color.white;

                string name = r.gameObject.name;
                bool wood = name == "BoardFrame";
                bool gold = name.StartsWith("Star_") || name.StartsWith("FinishPad_")
                            || name == "Centre";

                var styled = new Material(mat);
                if (wood) styled.SetTextureSafe(WoodTexture());
                else if (gold) styled.SetTextureSafe(GoldTexture());
                else styled.SetTextureSafe(PaintedTexture(c));

                if (styled.HasProperty("_Smoothness"))
                    styled.SetFloat("_Smoothness", wood ? 0.35f : gold ? 0.8f : 0.18f);
                if (styled.HasProperty("_Glossiness"))
                    styled.SetFloat("_Glossiness", wood ? 0.35f : gold ? 0.8f : 0.18f);

                r.sharedMaterial = styled;
            }
        }

        public static void SetTextureSafe(this Material m, Texture2D tex)
        {
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            else if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
        }

        // deterministic value noise (no Random — stable between runs)
        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                int h = x * 374761393 + y * 668265263 + seed * 1442695041;
                h = (h ^ (h >> 13)) * 1274126177;
                return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
            }
        }

        static float SmoothNoise(float x, float y, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float xf = x - xi, yf = y - yi;
            float a = Hash(xi, yi, seed), b = Hash(xi + 1, yi, seed);
            float c = Hash(xi, yi + 1, seed), d = Hash(xi + 1, yi + 1, seed);
            float u = xf * xf * (3 - 2 * xf), v = yf * yf * (3 - 2 * yf);
            return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
        }

        static Texture2D PaintedTexture(Color baseColor)
        {
            string key = "paint_" + ColorUtility.ToHtmlStringRGB(baseColor);
            if (TextureCache.TryGetValue(key, out var t)) return t;

            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // subtle hand-painted speckle
                    float n = SmoothNoise(x * 0.35f, y * 0.35f, 7) - 0.5f;
                    float shade = 1f + n * 0.12f;
                    // darker rim border (painted edge)
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    if (d < 5) shade *= 0.86f + 0.03f * d;
                    // gentle centre lift
                    float cx = x / (float)size - 0.5f, cy = y / (float)size - 0.5f;
                    shade += Mathf.Max(0f, 0.06f - Mathf.Sqrt(cx * cx + cy * cy)) * 0.5f;
                    px[y * size + x] = new Color(
                        Mathf.Clamp01(baseColor.r * shade),
                        Mathf.Clamp01(baseColor.g * shade),
                        Mathf.Clamp01(baseColor.b * shade), 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            TextureCache[key] = tex;
            return tex;
        }

        static Texture2D WoodTexture()
        {
            const string key = "wood";
            if (TextureCache.TryGetValue(key, out var t)) return t;

            const int size = 256;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var baseC = new Color(0.36f, 0.21f, 0.12f);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float warp = SmoothNoise(x * 0.05f, y * 0.02f, 21) * 18f;
                    float grain = Mathf.Sin((x + warp) * 0.35f) * 0.5f + 0.5f;
                    grain = grain * 0.7f + SmoothNoise(x * 0.5f, y * 0.1f, 22) * 0.3f;
                    float shade = 0.78f + grain * 0.4f;
                    px[y * size + x] = new Color(
                        baseC.r * shade, baseC.g * shade, baseC.b * shade, 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            TextureCache[key] = tex;
            return tex;
        }

        static Texture2D GoldTexture()
        {
            const string key = "gold";
            if (TextureCache.TryGetValue(key, out var t)) return t;

            const int size = 96;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float warp = SmoothNoise(x * 0.08f, y * 0.08f, 31) * 6f;
                    float sheen = Mathf.Sin((x + y + warp) * 0.22f) * 0.5f + 0.5f;
                    float n = SmoothNoise(x * 0.4f, y * 0.4f, 32) - 0.5f;
                    float k = 0.75f + sheen * 0.35f + n * 0.08f;
                    px[y * size + x] = new Color(
                        Mathf.Clamp01(0.95f * k),
                        Mathf.Clamp01(0.76f * k),
                        Mathf.Clamp01(0.26f * k), 1f);
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            TextureCache[key] = tex;
            return tex;
        }

        // ------------------------------------------------------------ sky & volume

        static void ApplySky(Camera camera)
        {
            // festival sky: deep indigo top, warm marigold horizon
            Shader shader = Shader.Find("Universal Render Pipeline/Skybox/Gradient");
            if (shader != null)
            {
                var mat = new Material(shader);
                if (mat.HasProperty("_Top")) mat.SetColor("_Top", new Color(0.25f, 0.30f, 0.52f));
                if (mat.HasProperty("_Middle")) mat.SetColor("_Middle", new Color(0.93f, 0.72f, 0.42f));
                if (mat.HasProperty("_Bottom")) mat.SetColor("_Bottom", new Color(0.52f, 0.32f, 0.26f));
                if (mat.HasProperty("_Diffusion")) mat.SetFloat("_Diffusion", 1.4f);
                if (mat.HasProperty("_Intensity1")) mat.SetFloat("_Intensity1", 1.0f);
                if (mat.HasProperty("_Intensity2")) mat.SetFloat("_Intensity2", 1.0f);
                RenderSettings.skybox = mat;
                return;
            }
            // built-in fallback: solid deep indigo
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.16f, 0.17f, 0.28f);
        }

        static void ApplyVolume(Camera camera)
        {
            try
            {
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();

                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.Override(0.55f);
                bloom.threshold.Override(0.92f);

                var vignette = profile.Add<Vignette>(true);
                vignette.intensity.Override(0.26f);
                vignette.smoothness.Override(0.45f);

                var grade = profile.Add<ColorAdjustments>(true);
                grade.saturation.Override(8f);
                grade.contrast.Override(4f);

                var volume = camera.gameObject.AddComponent<Volume>();
                volume.isGlobal = true;
                volume.priority = 1;
                volume.profile = profile;
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[DesiLudo3D] volume pass skipped: " + e.Message);
            }
        }
    }
}
