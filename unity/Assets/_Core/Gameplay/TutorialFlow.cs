using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>First-launch tutorial (M4): six short bilingual steps over a
    /// dimmed board. Shown once (PlayerPrefs); gray-box — M7 polish may add a
    /// settings reopen.</summary>
    public class TutorialFlow : MonoBehaviour
    {
        static readonly string[] StepKeys = { "tut1", "tut2", "tut3", "tut4", "tut5", "tut6" };

        GameObject _panel;
        Text _body;
        Button _next;
        int _step;

        public static bool AlreadyDone =>
            PlayerPrefs.GetInt("desiludo.tutorialDone", 0) == 1;

        public static TutorialFlow Show(Transform parent)
        {
            var go = new GameObject("TutorialFlow");
            go.transform.SetParent(parent, false);
            var flow = go.AddComponent<TutorialFlow>();
            flow.Build();
            return flow;
        }

        void Build()
        {
            var canvasGo = new GameObject("TutorialCanvas", typeof(Canvas),
                                          typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 800;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _panel = new GameObject("Panel", typeof(Image));
            _panel.transform.SetParent(canvasGo.transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.92f);

            var title = MakeText(_panel.transform, "Title", UiStrings.Get("tutTitle"),
                64, FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            title.rectTransform.anchorMin = title.rectTransform.anchorMax =
                new Vector2(0.5f, 1f);
            title.rectTransform.anchoredPosition = new Vector2(0f, -140f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 110f);

            _body = MakeText(_panel.transform, "Body", "", 46, FontStyle.Normal, Color.white);
            _body.rectTransform.anchorMin = _body.rectTransform.anchorMax =
                new Vector2(0.5f, 0.5f);
            _body.rectTransform.anchoredPosition = new Vector2(0f, 60f);
            _body.rectTransform.sizeDelta = new Vector2(900f, 700f);

            _next = MakeButton(_panel.transform, "Next", UiStrings.Get("next"),
                new Color(0.9f, 0.55f, 0.1f));
            var nr = _next.GetComponent<RectTransform>();
            nr.anchorMin = nr.anchorMax = new Vector2(0.5f, 0f);
            nr.anchoredPosition = new Vector2(180f, 140f);
            nr.sizeDelta = new Vector2(320f, 120f);
            _next.onClick.AddListener(Advance);

            var skip = MakeButton(_panel.transform, "Skip", UiStrings.Get("skip"),
                new Color(0.45f, 0.3f, 0.15f));
            var sr = skip.GetComponent<RectTransform>();
            sr.anchorMin = sr.anchorMax = new Vector2(0.5f, 0f);
            sr.anchoredPosition = new Vector2(-180f, 140f);
            sr.sizeDelta = new Vector2(320f, 120f);
            skip.onClick.AddListener(Finish);

            _step = 0;
            Render();
        }

        void Render()
        {
            _body.text = (_step + 1) + "/" + StepKeys.Length + "\n\n"
                       + UiStrings.Get(StepKeys[_step]);
            _next.GetComponentInChildren<Text>().text =
                _step == StepKeys.Length - 1 ? UiStrings.Get("done") : UiStrings.Get("next");
        }

        void Advance()
        {
            _step++;
            if (_step >= StepKeys.Length) { Finish(); return; }
            Render();
        }

        void Finish()
        {
            PlayerPrefs.SetInt("desiludo.tutorialDone", 1);
            PlayerPrefs.Save();
            Destroy(gameObject);
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
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            var text = MakeText(go.transform, "Label", label, 44, FontStyle.Bold, Color.white);
            text.rectTransform.Stretch();
            text.raycastTarget = false;
            return button;
        }
    }
}
