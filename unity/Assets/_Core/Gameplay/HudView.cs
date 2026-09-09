using System;
using DesiLudo3D.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Gray-box HUD (legacy uGUI built in code — no scene assets
    /// required). M3 replaces this with the styled UI system (truck-art
    /// bubbles, EN/HI string tables, TMP), keeping the same public surface.</summary>
    public class HudView : MonoBehaviour
    {
        public event Action RollPressed;
        public event Action RestartPressed;
        public event Action ProfilesPressed;
        public event Action LanguageChanged;
        public event Action OnlinePressed;

        Text _turnLabel;
        Text _dieLabel;
        Text _ticker;
        Button _rollButton;
        GameObject _resultsPanel;
        Text _resultsText;
        Button _restartButton;

        public void Build()
        {
            EnsureEventSystem();

            var canvasGo = new GameObject("Canvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasGo.transform.SetParent(transform, false);
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;   // adaptive portrait/landscape (D12)

            Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            _turnLabel = MakeText(canvasGo.transform, "TurnLabel", UiStrings.Get("title"),
                56, FontStyle.Bold, new Color(0.2f, 0.1f, 0.05f));
            Anchor(_turnLabel.rectTransform, TextAnchor.UpperCenter, new Vector2(0f, -40f), new Vector2(1000f, 90f));

            _dieLabel = MakeText(canvasGo.transform, "DieLabel", "", 120, FontStyle.Bold,
                new Color(0.45f, 0.2f, 0.05f));
            Anchor(_dieLabel.rectTransform, TextAnchor.UpperCenter, new Vector2(0f, -160f), new Vector2(600f, 160f));

            _ticker = MakeText(canvasGo.transform, "Ticker", "", 40, FontStyle.Normal,
                new Color(0.25f, 0.25f, 0.25f));
            Anchor(_ticker.rectTransform, TextAnchor.LowerCenter, new Vector2(0f, 260f), new Vector2(1000f, 120f));

            _rollButton = MakeButton(canvasGo.transform, "RollButton", UiStrings.Get("roll"), font,
                new Color(0.95f, 0.55f, 0.1f));
            Anchor(_rollButton.GetComponent<RectTransform>(), TextAnchor.LowerCenter,
                new Vector2(0f, 100f), new Vector2(420f, 130f));
            _rollButton.onClick.AddListener(() => RollPressed?.Invoke());

            _resultsPanel = new GameObject("ResultsPanel", typeof(Image));
            _resultsPanel.transform.SetParent(canvasGo.transform, false);
            var panelRect = _resultsPanel.GetComponent<RectTransform>();
            panelRect.anchorMin = Vector2.zero;
            panelRect.anchorMax = Vector2.one;
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
            _resultsPanel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.78f);

            _resultsText = MakeText(_resultsPanel.transform, "ResultsText", "", 64, FontStyle.Bold, Color.white);
            Anchor(_resultsText.rectTransform, TextAnchor.MiddleCenter, new Vector2(0f, 120f), new Vector2(1000f, 900f));

            _restartButton = MakeButton(_resultsPanel.transform, "RestartButton",
                UiStrings.Get("playAgain"), font, new Color(0.2f, 0.65f, 0.3f));
            Anchor(_restartButton.GetComponent<RectTransform>(), TextAnchor.LowerCenter,
                new Vector2(0f, 160f), new Vector2(460f, 140f));
            _restartButton.onClick.AddListener(() => RestartPressed?.Invoke());

            var profilesButton = MakeButton(canvasGo.transform, "ProfilesButton",
                UiStrings.Get("profiles"), font, new Color(0.5f, 0.25f, 0.55f));
            var pr = profilesButton.GetComponent<RectTransform>();
            pr.anchorMin = new Vector2(1f, 1f);
            pr.anchorMax = new Vector2(1f, 1f);
            pr.anchoredPosition = new Vector2(-230f, -80f);
            pr.sizeDelta = new Vector2(420f, 110f);
            profilesButton.onClick.AddListener(() => ProfilesPressed?.Invoke());

            // EN/HI language toggle (D8) — label shows the language you'd switch to
            var langButton = MakeButton(canvasGo.transform, "LangButton",
                UiStrings.Get("langName"), font, new Color(0.15f, 0.4f, 0.45f));
            var lr = langButton.GetComponent<RectTransform>();
            lr.anchorMin = new Vector2(0f, 1f);
            lr.anchorMax = new Vector2(0f, 1f);
            lr.anchoredPosition = new Vector2(120f, -80f);
            lr.sizeDelta = new Vector2(180f, 110f);

            var onlineButton = MakeButton(canvasGo.transform, "OnlineButton", "ONLINE",
                font, new Color(0.15f, 0.35f, 0.55f));
            var or_ = onlineButton.GetComponent<RectTransform>();
            or_.anchorMin = new Vector2(0f, 1f);
            or_.anchorMax = new Vector2(0f, 1f);
            or_.anchoredPosition = new Vector2(120f, -210f);
            or_.sizeDelta = new Vector2(180f, 110f);
            onlineButton.onClick.AddListener(() => OnlinePressed?.Invoke());
            langButton.onClick.AddListener(() =>
            {
                UiStrings.ToggleLang();
                langButton.GetComponentInChildren<Text>().text = UiStrings.Get("langName");
                LanguageChanged?.Invoke();
            });

            _resultsPanel.SetActive(false);
        }

        // ------------------------------------------------------------ surface

        public void SetTurn(string seatName, bool human, bool canRoll, bool canMove)
        {
            _turnLabel.text = UiStrings.F(human ? "turn" : "turnBot", seatName);
            _rollButton.interactable = canRoll;
            _rollButton.GetComponentInChildren<Text>().text =
                canMove ? UiStrings.Get("tapToken") : UiStrings.Get("roll");
        }

        /// <summary>Re-render every static label (language toggle).</summary>
        public void RefreshStrings()
        {
            _rollButton.GetComponentInChildren<Text>().text =
                _rollButton.interactable ? UiStrings.Get("roll") : UiStrings.Get("roll");
            _restartButton.GetComponentInChildren<Text>().text = UiStrings.Get("playAgain");
        }

        public void ShowDie(int value) => _dieLabel.text = value.ToString();

        public void Tick(string message) => _ticker.text = message;

        public void ShowResults(string lines)
        {
            _resultsText.text = lines;
            _resultsPanel.SetActive(true);
        }

        public void HideResults() => _resultsPanel.SetActive(false);

        // ------------------------------------------------------------ factory

        static void Anchor(RectTransform rt, TextAnchor anchor, Vector2 offset, Vector2 size)
        {
            switch (anchor)
            {
                case TextAnchor.UpperCenter:
                    rt.anchorMin = new Vector2(0.5f, 1f);
                    rt.anchorMax = new Vector2(0.5f, 1f);
                    break;
                case TextAnchor.LowerCenter:
                    rt.anchorMin = new Vector2(0.5f, 0f);
                    rt.anchorMax = new Vector2(0.5f, 0f);
                    break;
                case TextAnchor.MiddleCenter:
                    rt.anchorMin = new Vector2(0.5f, 0.5f);
                    rt.anchorMax = new Vector2(0.5f, 0.5f);
                    break;
            }
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
            t.horizontalOverflow = HorizontalWrapMode.Wrap;
            t.verticalOverflow = VerticalWrapMode.Overflow;
            t.raycastTarget = false;
            return t;
        }

        static Button MakeButton(Transform parent, string name, string label, Font font, Color color)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var image = go.GetComponent<Image>();
            image.color = color;
            var button = go.GetComponent<Button>();

            var text = MakeText(go.transform, "Label", label, 52, FontStyle.Bold, Color.white);
            Anchor(text.rectTransform, TextAnchor.MiddleCenter, Vector2.zero, new Vector2(0f, 0f));
            text.rectTransform.Stretch();
            return button;
        }

        static void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current != null) return;
            var es = new GameObject("EventSystem", typeof(UnityEngine.EventSystems.EventSystem));
#if ENABLE_INPUT_SYSTEM
            es.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
#else
            es.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
        }
    }

    internal static class RectTransformExtensions
    {
        public static void Stretch(this RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
