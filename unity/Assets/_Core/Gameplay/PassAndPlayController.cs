using System.Collections;
using System.Collections.Generic;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Board-state view consumed by ComedyPlayback — implemented by
    /// the offline controller AND the online host/guest controllers so the
    /// comedy layer works identically in every mode.</summary>
    public interface IBoardStateView
    {
        Seat[] SeatsInPlay { get; }
        int TokenProgressOf(Seat seat, int tokenIndex);
        bool IsMatchOver { get; }
        /// <summary>Seat whose turn it is right now (null when over).</summary>
        Seat CurrentSeat { get; }
        event System.Action<GameEvent> EventAnimatedView;
        /// <summary>Instant-feedback stream (SFX/slow-mo), pre-animation.</summary>
        event System.Action<GameEvent> EventStartedView;
    }

    /// <summary>Drives a local match: pass-and-play humans + tiered bots,
    /// event-to-animation pipeline (via the shared EventAnimator), camera
    /// rotation, HUD state. The M5+ online controllers reuse the animator;
    /// M6 swaps local input for NetTransport commands — this controller is
    /// the offline reference implementation of the host loop.</summary>
    public class PassAndPlayController : MonoBehaviour, IBoardStateView
    {
        protected MatchRunner _runner;
        MatchSetup _setup;
        protected BoardBuilder _board;
        protected DiceView _dice;
        protected CameraRig _cameraRig;
        protected HudView _hud;
        protected EventAnimator _animator;

        readonly Dictionary<Seat, IOpponentAgent> _bots = new Dictionary<Seat, IOpponentAgent>();
        protected bool _processing;
        bool _restartRequested;

        /// <summary>The authoritative host runner (save/resume, comedy, and
        /// later NetTransport all read from here).</summary>
        public MatchRunner Runner => _runner;

        // ------------------------------------------------ IBoardStateView

        public Seat[] SeatsInPlay => _runner.Config.SeatsInPlay;
        public int TokenProgressOf(Seat seat, int tokenIndex) =>
            _runner.State.Token(seat, tokenIndex).Progress;
        public bool IsMatchOver => _runner.IsMatchOver;
        public Seat CurrentSeat => _runner.IsMatchOver ? (Seat)(-1) : _runner.CurrentSeat;

        public event System.Action<GameEvent> EventAnimatedView;
        public event System.Action<GameEvent> EventStartedView;

        /// <summary>Raised after each event's animation completes — the
        /// comedy layer, R1 clip overlay and auto-save subscribe here.</summary>
        public event System.Action<GameEvent> EventAnimated;

        /// <summary>Raised the moment each event arrives (before animation) —
        /// instant feedback layer (SFX, slow-mo, screen shake).</summary>
        public event System.Action<GameEvent> EventStarted;

        /// <summary>Raised once per applied command, after its whole event
        /// batch has animated — the auto-save point.</summary>
        public event System.Action BatchCompleted;

        // ------------------------------------------------------------ init

        public void Initialize(MatchSetup setup, BoardBuilder board, DiceView dice,
                               CameraRig rig, HudView hud, int seed)
        {
            InitializeWithRunner(
                new MatchRunner(setup.BuildConfig(), new SeededRandomDiceProvider(seed)),
                setup, board, dice, rig, hud, seed);
        }

        /// <summary>Resume path (M3): adopt a restored MatchRunner (from
        /// MatchRunner.Load — hash-verified replay) with the CURRENT setup's
        /// controller kinds; the saved rules state is authoritative.</summary>
        public void InitializeWithRunner(MatchRunner runner, MatchSetup setup,
                                         BoardBuilder board, DiceView dice,
                                         CameraRig rig, HudView hud, int seed)
        {
            _setup = setup;
            _board = board;
            _dice = dice;
            _cameraRig = rig;
            _hud = hud;

            if (_runner != null) _runner.CommandApplied -= OnCommandApplied;
            if (_animator != null)
            {
                _animator.EventStarted -= OnEventStarted;
                _animator.EventAnimated -= OnEventAnimated;
            }
            _runner = runner ?? throw new System.ArgumentNullException(nameof(runner));
            _runner.CommandApplied += OnCommandApplied;

            _animator = EnsureAnimator();
            _animator.Wire(_board, _dice, _cameraRig, _hud);
            _animator.EventStarted += OnEventStarted;
            _animator.EventAnimated += OnEventAnimated;

            _bots.Clear();
            var tier = (BotTier)Mathf.Clamp((int)setup.BotTier, 0, 2);
            foreach (Seat seat in _runner.Config.SeatsInPlay)
            {
                if (setup.KindFor(seat) == ControllerKind.Bot)
                    _bots[seat] = new HeuristicAgent(tier, seed * 31 + (int)seat);
            }

            _hud.RollPressed -= OnRollPressed;
            _hud.RollPressed += OnRollPressed;
            _hud.RestartPressed -= OnRestartPressed;
            _hud.RestartPressed += OnRestartPressed;
            _hud.HideResults();
            _hud.Tick("");

            _animator.SyncAll(_runner.State);
            _cameraRig.SetSeat(_runner.CurrentSeat, instant: true);
            RefreshHud();
        }

        EventAnimator EnsureAnimator()
        {
            if (_animator != null) return _animator;
            var go = new GameObject("EventAnimator");
            go.transform.SetParent(transform, false);
            return go.AddComponent<EventAnimator>();
        }

        protected virtual void OnDestroy()
        {
            if (_runner != null) _runner.CommandApplied -= OnCommandApplied;
            if (_animator != null)
            {
                _animator.EventStarted -= OnEventStarted;
                _animator.EventAnimated -= OnEventAnimated;
            }
        }

        // ------------------------------------------------------------ input

        void Update()
        {
            if (_runner == null || _processing || _runner.IsMatchOver) return;
            if (_setup.KindFor(_runner.CurrentSeat) != ControllerKind.Human) return;

            if (_runner.IsAwaitingRoll) return;   // roll via HUD button

            if (_runner.IsAwaitingMove && GrayBoxInput.TapDown())
            {
                TokenView tapped = RaycastToken();
                if (tapped != null && tapped.Seat == _runner.CurrentSeat)
                {
                    foreach (LegalMove m in _runner.PendingMoves)
                    {
                        if (m.TokenIndex == tapped.Index)
                        {
                            _runner.TryMoveToken(tapped.Index);
                            break;
                        }
                    }
                }
            }
        }

        void OnRollPressed()
        {
            if (_processing || _runner == null || _runner.IsMatchOver) return;
            if (_runner.IsAwaitingRoll && _setup.KindFor(_runner.CurrentSeat) == ControllerKind.Human)
                _runner.TryRollDie();
        }

        void OnRestartPressed()
        {
            _restartRequested = true;
        }

        public bool ConsumeRestartRequest()
        {
            bool r = _restartRequested;
            _restartRequested = false;
            return r;
        }

        TokenView RaycastToken()
        {
            Ray ray = _cameraRig.Camera.ScreenPointToRay(GrayBoxInput.PointerPosition());
            var hits = Physics.RaycastAll(ray, 200f);
            TokenView best = null;
            float bestDist = float.MaxValue;
            foreach (var hit in hits)
            {
                var token = hit.collider.GetComponentInParent<TokenView>();
                if (token != null && hit.distance < bestDist)
                {
                    best = token;
                    bestDist = hit.distance;
                }
            }
            return best;
        }

        // ------------------------------------------------------------ events

        void OnCommandApplied(Command command, IReadOnlyList<GameEvent> events, string error)
        {
            if (error != null)
            {
                _hud.Tick(UiStrings.F("illegal", error));
                return;
            }
            _processing = true;
            _animator.AnimateBatch(events, () =>
            {
                _processing = false;
                RefreshHud();
                BatchCompleted?.Invoke();
                MaybeStartBotTurn();
            });
        }

        void OnEventStarted(GameEvent e)
        {
            EventStarted?.Invoke(e);
            EventStartedView?.Invoke(e);
        }
        void OnEventAnimated(GameEvent e)
        {
            EventAnimated?.Invoke(e);
            EventAnimatedView?.Invoke(e);
        }

        // ------------------------------------------------------------ bots & hud

        void MaybeStartBotTurn()
        {
            if (_runner.IsMatchOver || _processing) return;
            if (_setup.KindFor(_runner.CurrentSeat) != ControllerKind.Bot) return;
            StartCoroutine(BotTurn());
        }

        IEnumerator BotTurn()
        {
            yield return new WaitForSeconds(0.9f);
            if (_runner.IsMatchOver || _processing) yield break;
            Seat seat = _runner.CurrentSeat;
            if (_setup.KindFor(seat) != ControllerKind.Bot) yield break;

            if (_runner.IsAwaitingRoll)
            {
                _runner.TryRollDie();
            }
            else if (_runner.IsAwaitingMove)
            {
                var moves = _runner.PendingMoves;
                var agent = _bots.TryGetValue(seat, out var a) ? a : null;
                int index = agent != null
                    ? agent.ChooseMove(_runner.State, moves).TokenIndex
                    : moves[0].TokenIndex;
                _runner.TryMoveToken(index);
            }
        }

        public void RefreshHud()
        {
            if (_runner.IsMatchOver) return;
            bool human = _setup.KindFor(_runner.CurrentSeat) == ControllerKind.Human;
            _hud.SetTurn(Seats.Name(_runner.CurrentSeat), human,
                         human && _runner.IsAwaitingRoll,
                         human && _runner.IsAwaitingMove);
        }
    }
}
