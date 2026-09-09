using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Gray-box R1 clip manager: per profile slot, one clip per
    /// emotion — pick (system video picker — NEVER camera, A1), remove,
    /// in/out trim (±0.5s), preview. Backed by ReactionClipLibrary.</summary>
    public class ClipLibraryPanel : MonoBehaviour
    {
        ReactionClipLibrary _lib;
        IVideoImporter _importer;
        ClipReactionOverlay _overlay;
        GameObject _panel;
        GameObject _rows;
        int _slot;
        string _seatName;
        bool _busy;

        public void Initialize(ReactionClipLibrary library, IVideoImporter importer,
                               ClipReactionOverlay overlay)
        {
            _lib = library;
            _importer = importer;
            _overlay = overlay;
        }

        public void Open(int slot, string seatName)
        {
            _slot = slot;
            _seatName = seatName;
            BuildPanel();
            _panel.SetActive(true);
        }

        void Close() => _panel.SetActive(false);

        void BuildPanel()
        {
            if (_panel != null)
            {
                RebuildRows();
                return;
            }

            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) return;

            _panel = new GameObject("ClipPanel", typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.96f);

            var title = MakeText(_panel.transform, "Title",
                UiStrings.Get("clipsTitle"), 44, FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            AnchorTop(title.rectTransform, new Vector2(0f, -90f), new Vector2(1040f, 90f));

            _rows = new GameObject("Rows");
            _rows.transform.SetParent(_panel.transform, false);
            var rowsRect = _rows.AddComponent<RectTransform>();
            rowsRect.Stretch();
            RebuildRows();

            var done = MakeButton(_panel.transform, "DoneButton",
                UiStrings.Get("done"), new Color(0.2f, 0.5f, 0.3f), 44);
            AnchorBottom(done.GetComponent<RectTransform>(), new Vector2(0f, 70f), new Vector2(360f, 110f));
            done.onClick.AddListener(Close);

            _panel.SetActive(false);
        }

        void RebuildRows()
        {
            if (_rows == null) return;
            for (int i = _rows.transform.childCount - 1; i >= 0; i--)
                Destroy(_rows.transform.GetChild(i).gameObject);

            var subtitle = MakeText(_rows.transform, "Sub",
                _seatName + " — " + UiStrings.Get("clips"), 36, FontStyle.Bold, Color.white);
            AnchorTop(subtitle.rectTransform, new Vector2(0f, -170f), new Vector2(1040f, 70f));

            for (int i = 0; i < ReactionClipLibrary.Emotions.Length; i++)
                BuildEmotionRow(ReactionClipLibrary.Emotions[i], i);
        }

        void BuildEmotionRow(string emotion, int index)
        {
            string rowName = UiStrings.Get("emo" + char.ToUpper(emotion[0]) + emotion.Substring(1));
            var row = new GameObject("Row_" + emotion, typeof(RectTransform));
            row.transform.SetParent(_rows.transform, false);
            row.GetComponent<RectTransform>().Stretch();

            var label = MakeText(row.transform, "Label", rowName, 30, FontStyle.Bold,
                new Color(1f, 0.9f, 0.7f));
            Place(label.rectTransform, -370f, 0f, 180f, 70f);

            var entry = _lib.Get(_slot, emotion);

            var pick = MakeButton(row.transform, "Pick",
                UiStrings.Get(entry != null ? "replace" : "pick"),
                new Color(0.2f, 0.5f, 0.3f), 26);
            Place(pick.GetComponent<RectTransform>(), -160f, 0f, 200f, 70f);
            string emo = emotion;
            pick.onClick.AddListener(() => Pick(emo));

            if (entry != null)
            {
                var trim = MakeText(row.transform, "Trim",
                    entry.startSec.ToString("0.0") + "s–" +
                    (entry.endSec > 0f ? entry.endSec.ToString("0.0") + "s" : "end"),
                    26, FontStyle.Normal, Color.white);
                Place(trim.rectTransform, 70f, 0f, 170f, 70f);

                MakeTrimButton(row, "S-", -160f, () => _lib.AdjustTrim(_slot, emo, -0.5f, 0f));
                MakeTrimButton(row, "S+", -60f, () => _lib.AdjustTrim(_slot, emo, 0.5f, 0f));
                MakeTrimButton(row, "E-", 40f, () => _lib.AdjustTrim(_slot, emo, 0f, -0.5f));
                MakeTrimButton(row, "E+", 140f, () => _lib.AdjustTrim(_slot, emo, 0f, 0.5f));

                var play = MakeButton(row.transform, "Play", "▶", new Color(0.25f, 0.35f, 0.55f), 30);
                Place(play.GetComponent<RectTransform>(), 260f, 0f, 90f, 70f);
                play.onClick.AddListener(() => _overlay.PlayForSlot(_lib, _slot, emo));

                var rem = MakeButton(row.transform, "Remove", UiStrings.Get("remove"),
                    new Color(0.6f, 0.25f, 0.25f), 24);
                Place(rem.GetComponent<RectTransform>(), 380f, 0f, 150f, 70f);
                rem.onClick.AddListener(() => { _lib.Remove(_slot, emo); RebuildRows(); });
            }

            // vertical placement
            float y = -260f - index * 90f;
            foreach (RectTransform child in row.transform)
                child.anchoredPosition += new Vector2(0f, y);
            // label/pick placed relative then shifted; simpler: shift row root
        }

        void MakeTrimButton(GameObject row, string text, float x, System.Action action)
        {
            var b = MakeButton(row.transform, text, text, new Color(0.35f, 0.3f, 0.5f), 26);
            Place(b.GetComponent<RectTransform>(), x, 0f, 90f, 70f);
            b.onClick.AddListener(() => { action(); RebuildRows(); });
        }

        void Pick(string emotion)
        {
            if (_busy) return;
            if (!_importer.IsAvailable)
            {
                Debug.Log("[DesiLudo3D] Video import unavailable on this platform.");
                return;
            }
            _busy = true;
            _importer.ImportVideo(path =>
            {
                _busy = false;
                if (string.IsNullOrEmpty(path)) return;   // cancelled
                if (_lib.Set(_slot, emotion, path) != null)
                    RebuildRows();
            });
        }

        // ------------------------------------------------------------ helpers

        static void Place(RectTransform rt, float x, float y, float w, float h)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(x, y);
            rt.sizeDelta = new Vector2(w, h);
        }

        static void AnchorTop(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        static void AnchorBottom(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }

        static Text MakeText(Transform parent, string name, string text, int size,
                             FontStyle style, Color color)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<Text>();
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.alignment = TextAnchor.MiddleCenter;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label,
                                 Color color, int fontSize)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            var text = MakeText(go.transform, "Label", label, fontSize, FontStyle.Bold, Color.white);
            text.rectTransform.Stretch();
            text.raycastTarget = false;
            return button;
        }
    }
}
