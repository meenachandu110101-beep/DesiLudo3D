using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Single boot object for the gray-box scene: builds camera,
    /// light, board, dice, HUD, the pass-and-play controller, the identity
    /// layer, comedy playback and save/resume — all procedurally, no scene
    /// assets (IL-021). On boot, a detected auto-save offers RESUME.</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Match setup")]
        [Range(2, 4)] public int players = 4;
        public bool redIsBot;
        public bool greenIsBot;
        public bool yellowIsBot;
        public bool blueIsBot;
        public bool blockades = true;     // D4 toggle (default ON)
        [Range(0, 2)] public int botTier = 2;   // 0=Easy 1=Medium 2=Hard
        public int seed = 12345;

        ProfileService _profiles;
        ISpeechSynthesis _speech;
        ReactionClipLibrary _clipLib;
        ClipReactionOverlay _clipOverlay;
        ClipLibraryPanel _clipPanel;
        VoicePackLibrary _voiceLib;
        VoicePackPanel _voicePanel;
        System.Action<DesiLudo3D.Core.ComedyLine, string, int> _comedyLineHook;
        SaveService _saves;
        CameraRig _rig;
        BoardBuilder _board;
        PassAndPlayController _controller;
        HudView _hud;
        DiceView _dice;
        IdentityCenter _identity;
        ComedyPlayback _comedy;
        GameFeel _feel;
        bool _started;

        void Start()
        {
            if (_started) return;
            _started = true;
            BuildAll();
        }

        void BuildAll()
        {
            // camera
            Camera cam = Camera.main;
            if (cam == null)
            {
                cam = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener))
                    .GetComponent<Camera>();
            }
            _rig = new GameObject("CameraRig").AddComponent<CameraRig>();
            _rig.Attach(cam);

            // light (reuse the scene's directional light if present)
            Light light = FindFirstObjectByType<Light>();
            if (light == null)
            {
                light = new GameObject("Directional Light", typeof(Light)).GetComponent<Light>();
            }
            light.type = LightType.Directional;
            light.intensity = 1.15f;
            light.shadows = LightShadows.Soft;
            light.transform.rotation = Quaternion.Euler(50f, -35f, 0f);

            // save/resume (M3): peek BEFORE building the board — the board
            // layout must match the SAVED seat set, not the inspector's
            _saves = new SaveService();
            MatchRunner resumed = _saves.HasSave ? _saves.LoadRunner(seed) : null;
            if (_saves.HasSave && resumed == null) _saves.Delete();   // corrupt: discard
            MatchConfig config = resumed != null ? resumed.Config : BuildSetup().BuildConfig();

            // board + dice + hud + controller
            _board = new GameObject("BoardBuilder").AddComponent<BoardBuilder>();
            _board.Build(config);

            var diceGo = new GameObject("Dice");
            diceGo.transform.position = new Vector3(9.0f, 0.35f, 0f);
            _dice = diceGo.AddComponent<DiceView>();   // builds its own pip die

            _hud = new GameObject("Hud").AddComponent<HudView>();
            _hud.Build();

            _controller = new GameObject("PassAndPlay").AddComponent<PassAndPlayController>();
            _saves.Attach(_controller);

            // premium look pass + game feel (presentation-only)
            LookDev.Apply(_board, cam);
            _feel = new GameObject("GameFeel").AddComponent<GameFeel>();
            _feel.Attach(_controller);

            if (resumed != null)
            {
                StartMatch(resumed);
            }
            else if (_saves.HasSave)
            {
                // saved match exists but not yet loaded (panel choice)
                ShowResumePanel(resume => StartMatch(resume ? LoadResumed() : null));
            }
            else
            {
                StartMatch(null);
            }
        }

        MatchRunner LoadResumed()
        {
            MatchRunner runner = _saves.LoadRunner(seed);
            if (runner == null) _saves.Delete();
            return runner;
        }

        void ShowResumePanel(System.Action<bool> onChoice)
        {
            var canvas = FindObjectOfType<Canvas>();
            if (canvas == null) { onChoice(false); return; }

            var panel = new GameObject("ResumePanel", typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rect = panel.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.08f, 0.05f, 0.02f, 0.94f);
            panel.AddComponent<CanvasGroup>().blocksRaycasts = true;

            var title = MakePanelText(panel.transform, "Title",
                UiStrings.Get("resumeTitle"), 58, FontStyle.Bold, new Color(1f, 0.85f, 0.6f));
            title.rectTransform.anchoredPosition = new Vector2(0f, 120f);
            title.rectTransform.sizeDelta = new Vector2(1000f, 120f);

            var resume = MakePanelButton(panel.transform, "ResumeButton",
                UiStrings.Get("resume"), new Color(0.2f, 0.6f, 0.3f));
            resume.GetComponent<RectTransform>().anchoredPosition = new Vector2(-220f, -80f);
            resume.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 140f);
            resume.onClick.AddListener(() => { Destroy(panel); onChoice(true); });

            var fresh = MakePanelButton(panel.transform, "NewMatchButton",
                UiStrings.Get("newMatch"), new Color(0.6f, 0.3f, 0.1f));
            fresh.GetComponent<RectTransform>().anchoredPosition = new Vector2(220f, -80f);
            fresh.GetComponent<RectTransform>().sizeDelta = new Vector2(400f, 140f);
            fresh.onClick.AddListener(() => { Destroy(panel); _saves.Delete(); onChoice(false); });
        }

        /// <summary>Start (or resume) a match: controller, identity layer,
        /// comedy layer, language wiring.</summary>
        void StartMatch(MatchRunner resumed)
        {
            // setup must mirror the actual seat set (bot kinds per seat)
            if (resumed != null) players = resumed.Config.SeatsInPlay.Length;
            MatchSetup setup = BuildSetup();

            if (resumed != null)
            {
                _controller.InitializeWithRunner(resumed, setup, _board, _dice,
                                                 _rig, _hud, seed);
            }
            else
            {
                _controller.Initialize(setup, _board, _dice, _rig, _hud, seed);
            }

            MatchConfig config = resumed != null ? resumed.Config : setup.BuildConfig();

            // identity layer (M2) + R1 prerecorded clips (M3 — picker only, A1)
            if (_profiles == null) _profiles = new ProfileService(new FileProfileStore());
            if (_clipLib == null)
            {
                _clipLib = new ReactionClipLibrary();
                var overlayGo = new GameObject("ClipOverlay");
                _clipOverlay = overlayGo.AddComponent<ClipReactionOverlay>();
                _clipOverlay.Initialize();
                var panelGo = new GameObject("ClipPanel");
                _clipPanel = panelGo.AddComponent<ClipLibraryPanel>();
                _clipPanel.Initialize(_clipLib, VideoImporters.Create(), _clipOverlay);

                _voiceLib = new VoicePackLibrary();
                var vpGo = new GameObject("VoicePackPanel");
                _voicePanel = vpGo.AddComponent<VoicePackPanel>();
                _voicePanel.Initialize(_voiceLib, AudioImporters.Create());
            }
            if (_identity == null)
            {
                _identity = new GameObject("IdentityCenter").AddComponent<IdentityCenter>();
                _identity.Initialize(new IdentityDeps
                {
                    Profiles = _profiles,
                    PhotoImporter = PhotoImporters.Create(),
                    VideoImporter = VideoImporters.Create(),
                    Clips = _clipLib,
                    ClipOverlay = _clipOverlay,
                    ClipsPanel = _clipPanel,
                    VoicePackPanel = _voicePanel,
                }, _board, config, setup.Controllers);
                _hud.ProfilesPressed += _identity.Toggle;
            }

            // R1: when the comedy layer presents a line, play that profile's
            // prerecorded clip for the emotion if one is assigned (local only)
            if (_comedyLineHook == null)
            {
                _comedyLineHook = (line, emotion, slot) =>
                    _clipOverlay.PlayForSlot(_clipLib, slot, emotion);
            }

            // comedy layer (M3): validated Core engine -> spoken lines + bubbles
            if (_comedy == null)
            {
                _comedy = new GameObject("ComedyPlayback").AddComponent<ComedyPlayback>();
                _hud.LanguageChanged += () =>
                {
                    _controller.RefreshHud();
                    _hud.RefreshStrings();
                    _identity.RefreshLanguage();
                };
            }
            _comedy.Initialize(_controller, _board, _profiles,
                               _speech ?? (_speech = SpeechSynthesis.Create()),
                               (ulong)seed, greet: resumed == null);
            _comedy.LinePresented -= _comedyLineHook;
            _comedy.LinePresented += _comedyLineHook;
            _comedy.VoicePacks = _voiceLib;   // prerecorded VO > TTS (A4)

            // first-launch tutorial (once, over the fresh board)
            if (!TutorialFlow.AlreadyDone)
                TutorialFlow.Show(transform);
        }

        void Update()
        {
            // restart loop (M3 replaces with a proper results → setup flow)
            if (_controller != null && _controller.ConsumeRestartRequest())
            {
                RebuildMatch();
            }
        }

        void RebuildMatch()
        {
            seed = seed * 1103515245 + 12345;    // new match, new dice
            _saves.Delete();
            var setup = BuildSetup();
            _controller.Initialize(setup, _board, _dice, _rig, _hud, seed);
            _comedy.Initialize(_controller, _board, _profiles,
                               _speech ?? (_speech = SpeechSynthesis.Create()),
                               (ulong)seed);
        }

        MatchSetup BuildSetup()
        {
            var setup = new MatchSetup
            {
                Players = players,
                Blockades = blockades,
                ThreeSixes = true,
                CaptureExtra = true,
                Placings = true,
                BotTier = botTier,
            };
            var kinds = new[]
            {
                redIsBot ? ControllerKind.Bot : ControllerKind.Human,
                greenIsBot ? ControllerKind.Bot : ControllerKind.Human,
                yellowIsBot ? ControllerKind.Bot : ControllerKind.Human,
                blueIsBot ? ControllerKind.Bot : ControllerKind.Human,
            };
            setup.Controllers = kinds;
            return setup;
        }

        // -------------------------------------------------------- panel helpers

        static Text MakePanelText(Transform parent, string name, string text, int size,
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

        static Button MakePanelButton(Transform parent, string name, string label, Color color)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            go.GetComponent<Image>().color = color;
            var button = go.GetComponent<Button>();
            var text = MakePanelText(go.transform, "Label", label, 48, FontStyle.Bold, Color.white);
            text.rectTransform.Stretch();
            text.raycastTarget = false;
            return button;
        }
    }
}
