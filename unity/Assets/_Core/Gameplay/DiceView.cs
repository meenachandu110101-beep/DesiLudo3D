using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Pip-textured die (M3, spec §4.2): six procedurally drawn
    /// faces on a 6-quad die; the tumble is choreographed to LAND ON THE
    /// PREDETERMINED FACE (dice values come from the rules engine — never
    /// from physics, so fairness is preserved). Ivory + deep-red pips.</summary>
    public class DiceView : MonoBehaviour
    {
        static readonly Dictionary<int, Texture2D> PipTextures = new Dictionary<int, Texture2D>();

        static readonly Vector3[] FaceDirs =
        {
            Vector3.forward, Vector3.back, Vector3.up, Vector3.down, Vector3.right, Vector3.left,
        };
        static readonly int[] FaceValues = { 1, 6, 2, 5, 3, 4 };   // opposites sum to 7

        Vector3 _restPosition;

        void Awake()
        {
            _restPosition = transform.position;
            transform.localScale = Vector3.one * 0.62f;   // match the M1 die footprint
            BuildDie();
        }

        void BuildDie()
        {
            // ivory body (fills the edges between face quads)
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Body";
            body.transform.SetParent(transform, false);
            body.transform.localScale = new Vector3(0.96f, 0.96f, 0.96f);
            var col = body.GetComponent<Collider>();
            if (col != null) Destroy(col);
            var bodyMat = BoardBuilder.LitMaterial(new Color(0.97f, 0.93f, 0.85f));
            LookDev.SetTextureSafe(bodyMat, PipTexture(0));   // plain ivory
            body.GetComponent<Renderer>().sharedMaterial = bodyMat;

            for (int i = 0; i < FaceDirs.Length; i++)
            {
                int value = FaceValues[i];
                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Face_" + value;
                quad.transform.SetParent(transform, false);
                quad.transform.localPosition = FaceDirs[i] * 0.501f;
                quad.transform.localRotation = Quaternion.LookRotation(FaceDirs[i], Vector3.up);
                var qcol = quad.GetComponent<Collider>();
                if (qcol != null) Destroy(qcol);
                var mat = BoardBuilder.LitMaterial(Color.white);
                LookDev.SetTextureSafe(mat, PipTexture(value));
                quad.GetComponent<Renderer>().sharedMaterial = mat;
            }
        }

        /// <summary>Rotation that puts the given value's face UP.</summary>
        static Quaternion FaceUp(int value, float yawDeg)
        {
            Vector3 dir = Vector3.forward;
            for (int i = 0; i < FaceValues.Length; i++)
                if (FaceValues[i] == value) { dir = FaceDirs[i]; break; }
            return Quaternion.Euler(0f, yawDeg, 0f)
                 * Quaternion.FromToRotation(dir, Vector3.up);
        }

        public IEnumerator AnimateRoll(int value)
        {
            const float tumbleTime = 0.62f;
            Vector3 axis = Random.onUnitSphere;
            float speed = Random.Range(560f, 880f);
            float t = 0f;
            while (t < tumbleTime)
            {
                t += Time.deltaTime;
                transform.Rotate(axis, speed * Time.deltaTime, Space.World);
                transform.position = _restPosition
                    + Vector3.up * (0.3f * Mathf.Sin(Mathf.PI * t / tumbleTime));
                yield return null;
            }

            // settle onto the predetermined face (random yaw for life)
            const float settleTime = 0.26f;
            Quaternion start = transform.rotation;
            Quaternion end = FaceUp(value, Random.Range(0f, 360f));
            t = 0f;
            while (t < settleTime)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / settleTime);
                float kEase = 1f - (1f - k) * (1f - k);
                transform.rotation = Quaternion.Slerp(start, end, kEase);
                // tiny bounce as it lands
                float bounce = Mathf.Abs(Mathf.Sin(Mathf.PI * k)) * 0.05f * (1f - k);
                transform.position = _restPosition + Vector3.up * bounce;
                yield return null;
            }
            transform.position = _restPosition;
            transform.rotation = end;
        }

        // ------------------------------------------------------------ pip art

        /// <summary>Value 0 = plain ivory (die body).</summary>
        static Texture2D PipTexture(int value)
        {
            if (PipTextures.TryGetValue(value, out var cached)) return cached;

            const int size = 128;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Bilinear;

            // pip layout in grid space [-1, 1]
            var layout = new List<(float x, float y)>();
            switch (value)
            {
                case 1: layout.Add((0f, 0f)); break;
                case 2: layout.Add((-1f, -1f)); layout.Add((1f, 1f)); break;
                case 3: layout.Add((-1f, -1f)); layout.Add((0f, 0f)); layout.Add((1f, 1f)); break;
                case 4: layout.Add((-1f, -1f)); layout.Add((1f, -1f)); layout.Add((-1f, 1f)); layout.Add((1f, 1f)); break;
                case 5:
                    layout.Add((-1f, -1f)); layout.Add((1f, -1f)); layout.Add((0f, 0f));
                    layout.Add((-1f, 1f)); layout.Add((1f, 1f)); break;
                case 6:
                    layout.Add((-1f, -1f)); layout.Add((-1f, 0f)); layout.Add((-1f, 1f));
                    layout.Add((1f, -1f)); layout.Add((1f, 0f)); layout.Add((1f, 1f)); break;
            }

            const float pipRadius = 15f;
            var ivory = new Color(0.97f, 0.93f, 0.85f);
            var pip = new Color(0.55f, 0.12f, 0.10f);
            var px = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    Color c = ivory;
                    // faint ivory grain
                    float g = Mathf.PerlinNoise(x * 0.11f, y * 0.11f) * 0.06f - 0.03f;
                    c = new Color(c.r + g, c.g + g, c.b + g, 1f);
                    // rounded edge shading
                    int d = Mathf.Min(Mathf.Min(x, y), Mathf.Min(size - 1 - x, size - 1 - y));
                    if (d < 8) c *= 0.92f + 0.01f * d;

                    if (value > 0)
                    {
                        foreach (var (gx, gy) in layout)
                        {
                            float px_ = size / 2f + gx * size * 0.27f;
                            float py_ = size / 2f + gy * size * 0.27f;
                            float dist = Mathf.Sqrt((x - px_) * (x - px_) + (y - py_) * (y - py_));
                            if (dist < pipRadius) c = pip;
                            else if (dist < pipRadius + 2f)
                                c = Color.Lerp(pip, c, (dist - pipRadius) / 2f);
                        }
                    }
                    px[y * size + x] = c;
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            PipTextures[value] = tex;
            return tex;
        }
    }
}
