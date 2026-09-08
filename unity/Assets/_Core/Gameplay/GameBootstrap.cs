using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Single boot object for the M1 gray-box scene: builds camera,
    /// light, board, dice, HUD and the pass-and-play controller procedurally.
    /// No prefabs or scene assets required — the whole game assembles from
    /// code, which keeps the authored repo buildable without binary assets.
    /// (M3 introduces authored scene content on top of this.)</summary>
    public class GameBootstrap : MonoBehaviour
    {
        [Header("Match setup")]
        [Range(2, 4)] public int players = 4;
        public bool redIsBot;
        public bool greenIsBot;
        public bool yellowIsBot;
        public bool blueIsBot;
        public bool blockades = true;     // D4 toggle (default ON)
        public int seed = 12345;

        CameraRig _rig;
        BoardBuilder _board;
        PassAndPlayController _controller;
        HudView _hud;
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

            // board + dice + hud + controller
            _board = new GameObject("BoardBuilder").AddComponent<BoardBuilder>();
            _board.Build(BuildSetup().BuildConfig());

            var diceGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diceGo.name = "Dice";
            diceGo.transform.position = new Vector3(9.0f, 0.35f, 0f);
            diceGo.transform.localScale = new Vector3(0.55f, 0.55f, 0.55f);
            diceGo.GetComponent<Renderer>().sharedMaterial =
                BoardBuilder.LitMaterial(new Color(0.97f, 0.95f, 0.9f));
            var dice = diceGo.AddComponent<DiceView>();

            _hud = new GameObject("Hud").AddComponent<HudView>();
            _hud.Build();

            _controller = new GameObject("PassAndPlay").AddComponent<PassAndPlayController>();
            _controller.Initialize(BuildSetup(), _board, dice, _rig, _hud, seed);
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
            _controller.Initialize(BuildSetup(), _board, _rig, _hud, seed);
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
    }
}
