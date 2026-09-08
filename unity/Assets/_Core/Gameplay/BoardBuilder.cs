using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Procedural gray-box board builder (M1). The polished festival
    /// look (spec §8, D7) replaces these primitives in M3 — the structure
    /// (cells + slots + tokens from BoardGridMap) stays identical.</summary>
    public class BoardBuilder : MonoBehaviour
    {
        public Transform BoardRoot { get; private set; }
        public TokenView[] Tokens { get; private set; } = new TokenView[16];

        static readonly Color[] SeatColors =
        {
            new Color(0.82f, 0.16f, 0.16f),   // Red
            new Color(0.18f, 0.68f, 0.26f),   // Green
            new Color(0.95f, 0.78f, 0.10f),   // Yellow
            new Color(0.15f, 0.40f, 0.90f),   // Blue
        };

        MatchConfig _config;

        public static Color ColorOf(Seat seat) => SeatColors[(int)seat];

        public void Build(MatchConfig config)
        {
            _config = config;
            BoardRoot = new GameObject("Board").transform;
            BoardRoot.SetParent(transform, false);

            MakeFrame();
            MakeLoopCells();
            MakeRunways();
            MakeBases();
            MakeCentre();
            MakeTokens();
        }

        // ------------------------------------------------------------ pieces

        void MakeFrame()
        {
            // warm wooden frame slab under everything — premium silhouette seed
            var frame = GameObject.CreatePrimitive(PrimitiveType.Cube);
            frame.name = "BoardFrame";
            frame.transform.SetParent(BoardRoot, false);
            frame.transform.localScale = new Vector3(16.6f, 0.28f, 16.6f);
            frame.transform.localPosition = new Vector3(0f, -0.16f, 0f);
            SetMaterial(frame, new Color(0.34f, 0.20f, 0.11f));
            DisableCollider(frame);
        }

        void MakeLoopCells()
        {
            var parent = NewChild("LoopCells");
            for (int i = 0; i < BoardGeometry.LoopCells; i++)
            {
                (int x, int y) = DesiLudo3D.Core.BoardGridMap.Loop[i];
                var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                cell.name = "Cell_" + i;
                cell.transform.SetParent(parent, false);
                var (wx, wz) = DesiLudo3D.Core.BoardGridMap.GridToWorld(x, y);
                cell.transform.localPosition = new Vector3(wx, 0.05f, wz);
                cell.transform.localScale = new Vector3(0.96f, 0.12f, 0.96f);

                Color c = new Color(0.96f, 0.92f, 0.84f);          // cream
                bool isStart = false;
                foreach (Seat s in AllSeats())
                {
                    if (BoardGeometry.StartCell(s) == i)
                    {
                        c = ColorOf(s).Tint(0.55f);
                        isStart = true;
                        break;
                    }
                }
                if (!isStart && BoardGeometry.IsSafeCell(i))
                    c = new Color(0.98f, 0.86f, 0.45f);            // star gold
                SetMaterial(cell, c);
                DisableCollider(cell);

                if (!isStart && BoardGeometry.IsSafeCell(i))
                {
                    var star = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    star.name = "Star_" + i;
                    star.transform.SetParent(parent, false);
                    star.transform.localPosition = new Vector3(wx, 0.13f, wz);
                    star.transform.localScale = new Vector3(0.4f, 0.02f, 0.4f);
                    SetMaterial(star, new Color(0.85f, 0.62f, 0.08f));
                    DisableCollider(star);
                }
            }
        }

        void MakeRunways()
        {
            var parent = NewChild("Runways");
            foreach (Seat seat in _config.SeatsInPlay)
            {
                var column = DesiLudo3D.Core.BoardGridMap.HomeColumns[(int)seat];
                for (int i = 0; i < column.Length; i++)
                {
                    var cell = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    cell.name = "Runway_" + seat + "_" + i;
                    cell.transform.SetParent(parent, false);
                    var (wx, wz) = DesiLudo3D.Core.BoardGridMap.GridToWorld(column[i].X, column[i].Y);
                    cell.transform.localPosition = new Vector3(wx, 0.05f, wz);
                    cell.transform.localScale = new Vector3(0.96f, 0.12f, 0.96f);
                    SetMaterial(cell, ColorOf(seat).Tint(0.6f));
                    DisableCollider(cell);
                }
            }
        }

        void MakeBases()
        {
            var parent = NewChild("Bases");
            foreach (Seat seat in _config.SeatsInPlay)
            {
                var (cx, cy) = BaseCentre(seat);
                var slab = GameObject.CreatePrimitive(PrimitiveType.Cube);
                slab.name = "Base_" + seat;
                slab.transform.SetParent(parent, false);
                slab.transform.localPosition = new Vector3(cx, 0.02f, cy);
                slab.transform.localScale = new Vector3(5.6f, 0.08f, 5.6f);
                SetMaterial(slab, ColorOf(seat).Tint(0.35f));
                DisableCollider(slab);

                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                {
                    var slot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    slot.name = "Slot_" + seat + "_" + i;
                    slot.transform.SetParent(parent, false);
                    var (sx, sy) = DesiLudo3D.Core.BoardGridMap.BaseSlots[(int)seat][i];
                    var (wx, wz) = DesiLudo3D.Core.BoardGridMap.GridToWorld(sx, sy);
                    slot.transform.localPosition = new Vector3(wx, 0.08f, wz);
                    slot.transform.localScale = new Vector3(0.8f, 0.03f, 0.8f);
                    SetMaterial(slot, ColorOf(seat).Tint(0.75f));
                    DisableCollider(slot);
                }
            }
        }

        void MakeCentre()
        {
            var centre = GameObject.CreatePrimitive(PrimitiveType.Cube);
            centre.name = "Centre";
            centre.transform.SetParent(BoardRoot, false);
            centre.transform.localPosition = new Vector3(0f, 0.04f, 0f);
            centre.transform.localScale = new Vector3(2.9f, 0.1f, 2.9f);
            SetMaterial(centre, new Color(0.93f, 0.88f, 0.78f));
            DisableCollider(centre);

            // four seat-coloured finish pads in the centre quarters (M3: diya-lit)
            var parent = NewChild("CentrePads");
            foreach (Seat seat in AllSeats())
            {
                var pad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                pad.name = "FinishPad_" + seat;
                pad.transform.SetParent(parent, false);
                var (px, pz) = CentrePadOffset(seat);
                pad.transform.localPosition = new Vector3(px, 0.1f, pz);
                pad.transform.localScale = new Vector3(0.9f, 0.06f, 0.9f);
                SetMaterial(pad, ColorOf(seat).Tint(0.7f));
                DisableCollider(pad);
            }
        }

        void MakeTokens()
        {
            foreach (Seat seat in _config.SeatsInPlay)
            {
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.name = "Token_" + seat + "_" + i;
                    var token = go.AddComponent<TokenView>();
                    token.Initialize(seat, i, BoardRoot);
                    Tokens[(int)seat * BoardGeometry.TokensPerSeat + i] = token;
                }
            }
        }

        // ------------------------------------------------------------ helpers

        static Seat[] AllSeats() => new[] { Seat.Red, Seat.Green, Seat.Yellow, Seat.Blue };

        static (float, float) BaseCentre(Seat seat)
        {
            switch (seat)
            {
                case Seat.Red: return (-4.5f, -4.5f);
                case Seat.Green: return (4.5f, -4.5f);
                case Seat.Yellow: return (4.5f, 4.5f);
                case Seat.Blue: return (-4.5f, 4.5f);
                default: return (0f, 0f);
            }
        }

        static (float, float) CentrePadOffset(Seat seat)
        {
            switch (seat)
            {
                case Seat.Red: return (-0.7f, -0.7f);
                case Seat.Green: return (0.7f, -0.7f);
                case Seat.Yellow: return (0.7f, 0.7f);
                case Seat.Blue: return (-0.7f, 0.7f);
                default: return (0f, 0f);
            }
        }

        Transform NewChild(string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(BoardRoot, false);
            return t;
        }

        static void DisableCollider(GameObject go)
        {
            var col = go.GetComponent<Collider>();
            if (col != null) Destroy(col);
        }

        public static void SetMaterial(GameObject go, Color color)
        {
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = LitMaterial(color);
        }

        static Material _lit;

        /// <summary>URP Lit when a URP pipeline is active, built-in Standard
        /// otherwise — so the gray-box renders correctly before the URP asset
        /// is assigned (see docs/UNITY-VALIDATION.md).</summary>
        public static Material LitMaterial(Color color)
        {
            if (_lit == null)
            {
                bool urp = UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline != null;
                Shader shader = Shader.Find(urp ? "Universal Render Pipeline/Lit" : "Standard");
                if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                _lit = new Material(shader);
            }
            var m = new Material(_lit);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", color);
            if (m.HasProperty("_Color")) m.SetColor("_Color", color);
            return m;
        }
    }

    internal static class ColorExtensions
    {
        /// <summary>Mix toward white (t=1 -> white).</summary>
        public static Color Tint(this Color c, float t) => Color.Lerp(c, Color.white, t);
    }
}
