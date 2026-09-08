using System.IO;
using UnityEngine;
using Object = UnityEngine.Object;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Photo → oval-masked medallion texture (P3: the player's real
    /// face on their token). Pure CPU pixel work: center-crop, bilinear
    /// resample to 256×256, soft ellipse alpha mask, PNG persistence under
    /// Application.persistentDataPath. Device-only storage (spec §12).</summary>
    public static class PhotoProcessor
    {
        public const int MedallionSize = 256;

        /// <summary>Load an image file and produce the oval-masked medallion
        /// texture, or null if the file can't be read/decoded.</summary>
        public static Texture2D LoadAndMask(string sourcePath, int size = MedallionSize)
        {
            if (string.IsNullOrEmpty(sourcePath) || !File.Exists(sourcePath)) return null;

            byte[] bytes;
            try { bytes = File.ReadAllBytes(sourcePath); }
            catch (System.Exception) { return null; }

            var src = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!src.LoadImage(bytes))
            {
                Object.Destroy(src);
                return null;
            }

            try
            {
                // center-crop square from the source
                int side = Mathf.Min(src.width, src.height);
                float offX = (src.width - side) * 0.5f;
                float offY = (src.height - side) * 0.5f;

                var dst = new Texture2D(size, size, TextureFormat.RGBA32, false);
                var pixels = new Color[size * size];

                for (int y = 0; y < size; y++)
                {
                    float v = (y + 0.5f) / size;
                    for (int x = 0; x < size; x++)
                    {
                        float u = (x + 0.5f) / size;

                        // map into the cropped square (GetPixelBilinear is [0,1] over the full source)
                        float su = (offX + u * side) / src.width;
                        float sv = (offY + v * side) / src.height;
                        Color c = src.GetPixelBilinear(su, sv);

                        // soft ellipse alpha (medallion)
                        float nx = (x + 0.5f) / size * 2f - 1f;
                        float ny = (y + 0.5f) / size * 2f - 1f;
                        float r = Mathf.Sqrt(nx * nx + ny * ny);
                        c.a = Mathf.Clamp01((1.02f - r) * 10f);

                        pixels[y * size + x] = c;
                    }
                }

                dst.SetPixels(pixels);
                dst.Apply();
                return dst;
            }
            finally
            {
                Object.Destroy(src);
            }
        }

        /// <summary>Persist a medallion texture as PNG under
        /// persistentDataPath/identity; returns the stored path.</summary>
        public static string PersistMasked(Texture2D medallion, string fileName)
        {
            string dir = Path.Combine(Application.persistentDataPath, "identity");
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            File.WriteAllBytes(path, medallion.EncodeToPNG());
            return path;
        }

        /// <summary>Load a previously persisted medallion, or null.</summary>
        public static Texture2D LoadPersisted(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            byte[] bytes;
            try { bytes = File.ReadAllBytes(path); }
            catch (System.Exception) { return null; }
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            return tex.LoadImage(bytes) ? tex : null;
        }
    }
}
