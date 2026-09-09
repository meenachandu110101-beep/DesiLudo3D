using System.Collections.Generic;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Gray-box identity center (M2): per-seat cards — pick photo
    /// (system picker, never camera — A1), cycle character category
    /// (Male/Female/Child — A2/A5), clear photo. Photos apply live to token
    /// medallions. The styled M3 profile editor replaces this UI behind the
    /// same service layer.</summary>
    public class IdentityCenter : MonoBehaviour
    {
        ProfileService _profiles;
        IPhotoImporter _importer;
        BoardBuilder _board;
        MatchConfig _config;
        ControllerKind[] _kinds;

        readonly Dictionary<int, Texture2D> _medallions = new Dictionary<int, Texture2D>();
        GameObject _panel;
        GameObject _cardsRoot;
        bool _busy;

        public void Initialize(ProfileService profiles, IPhotoImporter importer,
                               BoardBuilder board, MatchConfig config, ControllerKind[] kinds)
        {
            _profiles = profiles;
            _importer = importer;
            _board = board;
            _config = config;
            _kinds = kinds ?? new ControllerKind[4];
            BuildPanel();
            ApplyPersistedMedallions();
        }

        void OnDestroy()
        {
            foreach (var kv in _medallions)
                if (kv.Value != null) Destroy(kv.Value);
        }

        // -------------------------------------------------------------- panel

        void BuildPanel()
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null)
            {
                Debug.LogWarning("[DesiLudo3D] IdentityCenter needs a canvas (HUD builds one).");
                return;
            }

            _panel = new GameObject("IdentityPanel", typeof(Image));
            _panel.transform.SetParent(canvas.transform, false);
            var rect = _panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            _panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.94f);

            var title = MakeText(_panel.transform, "Title", UiStrings.Get("profilesTitle"),
                58, FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            AnchorTop(title.rectTransform, new Vector2(0f, -100f), new Vector2(1000f, 120f));

            _cardsRoot = new GameObject("Cards");
            _cardsRoot.transform.SetParent(_panel.transform, false);
            var cardsRect = _cardsRoot.AddComponent<RectTransform>();
            cardsRect.Stretch();
            RefreshCards();

            var close = MakeButton(_panel.transform, "CloseButton",
                UiStrings.Get("close"), new Color(0.6f, 0.3f, 0.1f), 46);
            AnchorBottom(close.GetComponent<RectTransform>(), new Vector2(0f, 90f), new Vector2(380f, 120f));
            close.onClick.AddListener(Toggle);

            _panel.SetActive(false);
        }

        void BuildCard(Seat seat, int slot)
        {
            var data = _profiles.Load(slot);
            var identity = data.ToIdentity();
            bool bot = slot < _kinds.Length && _kinds[slot] == ControllerKind.Bot;

            var card = new GameObject("Card_" + seat, typeof(Image));
            card.transform.SetParent(_cardsRoot.transform, false);
            card.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
            AnchorTop(card.GetComponent<RectTransform>(),
                      new Vector2(0f, -250f - slot * 350f), new Vector2(920f, 320f));

            var seatColor = ToUiColor(BoardBuilder.ColorOf(seat));
            var label = MakeText(card.transform, "SeatLabel",
                Seats.Name(seat) + (bot ? " (" + UiStrings.Get("botTag") + ") — " : " — ")
                    + identity.Name,
                42, FontStyle.Bold, seatColor);
            CenterAt(label.rectTransform, new Vector2(0f, 105f), new Vector2(860f, 70f));

            var category = MakeButton(card.transform, "CategoryButton",
                UiStrings.F("voice", CategoryLabel(identity.Category)),
                new Color(0.25f, 0.35f, 0.55f), 34);
            CenterAt(category.GetComponent<RectTransform>(), new Vector2(-230f, 25f), new Vector2(400f, 110f));
            category.onClick.AddListener(() => CycleCategory(slot, seat));

            var personality = MakeButton(card.transform, "PersonalityButton",
                UiStrings.F("style", PersonalityLabel(identity.Personality)),
                new Color(0.45f, 0.3f, 0.55f), 32);
            CenterAt(personality.GetComponent<RectTransform>(), new Vector2(230f, 25f), new Vector2(400f, 110f));
            personality.onClick.AddListener(() => CyclePersonality(slot));

            var photo = MakeButton(card.transform, "PhotoButton",
                UiStrings.Get(string.IsNullOrEmpty(data.photoPath) ? "pickPhoto" : "replacePhoto"),
                new Color(0.2f, 0.5f, 0.3f), 34);
            CenterAt(photo.GetComponent<RectTransform>(), new Vector2(-130f, -100f), new Vector2(580f, 110f));
            photo.onClick.AddListener(() => PickPhoto(slot, seat));

            var clear = MakeButton(card.transform, "ClearButton",
                UiStrings.Get("clear"), new Color(0.6f, 0.25f, 0.25f), 30);
            CenterAt(clear.GetComponent<RectTransform>(), new Vector2(330f, -100f), new Vector2(200f, 110f));
            clear.onClick.AddListener(() => ClearPhoto(slot, seat));
        }

        // ------------------------------------------------------------- actions

        void CycleCategory(int slot, Seat seat)
        {
            var data = _profiles.Load(slot);
            var identity = data.ToIdentity();
            identity.Category = PlayerIdentity.NextCategory(identity.Category);
            _profiles.Save(slot, PlayerProfileData.From(identity, data.photoPath));
            RefreshCards();
        }

        void CyclePersonality(int slot)
        {
            var data = _profiles.Load(slot);
            var identity = data.ToIdentity();
            identity.Personality = (PersonalityStyle)(((int)identity.Personality + 1) % 5);
            _profiles.Save(slot, PlayerProfileData.From(identity, data.photoPath));
            RefreshCards();
        }

        void PickPhoto(int slot, Seat seat)
        {
            if (_busy) return;
            if (!_importer.IsAvailable)
            {
                Debug.Log("[DesiLudo3D] Photo import unavailable on this platform in M2.");
                return;
            }
            _busy = true;
            _importer.ImportPhoto(path =>
            {
                _busy = false;
                if (string.IsNullOrEmpty(path)) return;   // cancelled

                Texture2D medallion = PhotoProcessor.LoadAndMask(path);
                if (medallion == null)
                {
                    Debug.LogWarning("[DesiLudo3D] Could not load/decode the picked image.");
                    return;
                }
                string stored = PhotoProcessor.PersistMasked(medallion, "face_" + slot + ".png");
                _profiles.SetPhoto(slot, stored);
                StoreMedallion(slot, medallion);
                ApplyMedallion(slot, seat);
                RefreshCards();
            });
        }

        void ClearPhoto(int slot, Seat seat)
        {
            _profiles.ClearPhoto(slot);
            _medallions.Remove(slot);
            ApplyMedallion(slot, seat);
            RefreshCards();
        }

        // ---------------------------------------------------------- medallions

        void StoreMedallion(int slot, Texture2D medallion)
        {
            if (_medallions.TryGetValue(slot, out var old) && old != null) Destroy(old);
            _medallions[slot] = medallion;
        }

        void ApplyMedallion(int slot, Seat seat)
        {
            var token = _board.Tokens[(int)seat * BoardGeometry.TokensPerSeat];
            if (token == null) return;
            _medallions.TryGetValue(slot, out var medallion);
            token.SetFaceTexture(medallion);   // null = default token head
        }

        void ApplyPersistedMedallions()
        {
            if (_panel == null) return;
            int slot = 0;
            foreach (Seat seat in _config.SeatsInPlay)
            {
                var data = _profiles.Load(slot);
                if (!string.IsNullOrEmpty(data.photoPath))
                {
                    var medallion = PhotoProcessor.LoadPersisted(data.photoPath);
                    if (medallion != null)
                    {
                        StoreMedallion(slot, medallion);
                        ApplyMedallion(slot, seat);
                    }
                }
                slot++;
            }
        }

        // --------------------------------------------------------------- misc

        void RefreshCards()
        {
            if (_cardsRoot == null) return;
            for (int i = _cardsRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_cardsRoot.transform.GetChild(i).gameObject);
            int slot = 0;
            foreach (Seat seat in _config.SeatsInPlay)
            {
                BuildCard(seat, slot);
                slot++;
            }
        }

        public void Toggle()
        {
            if (_panel != null) _panel.SetActive(!_panel.activeSelf);
        }

        static Color ToUiColor(Color c) => new Color(c.r, c.g, c.b, 1f);

        static string PersonalityLabel(PersonalityStyle p)
        {
            switch (p)
            {
                case PersonalityStyle.Overconfident: return UiStrings.Get("styOverconfident");
                case PersonalityStyle.DramaKing: return UiStrings.Get("styDrama");
                case PersonalityStyle.SweetButDeadly: return UiStrings.Get("stySweet");
                case PersonalityStyle.Excited: return UiStrings.Get("styExcited");
                default: return UiStrings.Get("styBalanced");
            }
        }

        static string CategoryLabel(CharacterCategory c)
        {
            switch (c)
            {
                case CharacterCategory.Female: return UiStrings.Get("catFemale");
                case CharacterCategory.Child: return UiStrings.Get("catChild");
                default: return UiStrings.Get("catMale");
            }
        }

        /// <summary>Re-render after a live EN/HI language toggle.</summary>
        public void RefreshLanguage()
        {
            if (_panel == null) return;
            var title = _panel.transform.Find("Title");
            if (title != null) title.GetComponent<Text>().text = UiStrings.Get("profilesTitle");
            var close = _panel.transform.Find("CloseButton");
            if (close != null)
                close.GetComponentInChildren<Text>().text = UiStrings.Get("close");
            RefreshCards();
        }

        // -------------------------------------------------------- ui helpers

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

        static Button MakeButton(Transform parent, string name, string label, Color color, int fontSize)
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

        static void CenterAt(RectTransform rt, Vector2 offset, Vector2 size)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = offset;
            rt.sizeDelta = size;
        }
    }
}
