using System.Collections;
using System.Collections.Generic;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Host-device online controller: drives the authoritative
    /// NetHostSession (local input for the host seat, net lines for guests,
    /// tiered bots for vacant seats) and animates through the shared
    /// EventAnimator — identical feel to offline play.</summary>
    public class OnlineHostController : MonoBehaviour, IBoardStateView
    {
        NetHostSession _session;
        LanTransport.HostSide _transport;
        BoardBuilder _board;
        DiceView _dice;
        CameraRig _cameraRig;
        HudView _hud;
        EventAnimator _animator;
        ProfileService _profiles;

        readonly Dictionary<Seat, IOpponentAgent> _bots = new Dictionary<Seat, IOpponentAgent>();
        readonly Dictionary<Seat, string> _seatKind = new Dictionary<Seat, string>(); // "host"/"guest"/"bot"
        int _botTier;
        int _seed;
        bool _processing;
        int _seed;

        public NetHostSession Session => _session;

        // ------------------------------------------------ IBoardStateView

        public Seat[] SeatsInPlay => _session.Seats;
        public int TokenProgressOf(Seat seat, int tokenIndex) =>
            _session.Runner.State.Token(seat, tokenIndex).Progress;
        public bool IsMatchOver => _session.Runner.IsMatchOver;
        public Seat CurrentSeat => _session.Runner.IsMatchOver ? (Seat)(-1)
                                    : _session.Runner.CurrentSeat;

        public event System.Action<GameEvent> EventAnimatedView;
        public event System.Action<GameEvent> EventStarted;
        public event System.Action<GameEvent> EventStartedView;
        public event System.Action BatchCompleted;

        public void Initialize(NetHostSession session, LanTransport.HostSide transport,
                               BoardBuilder board, DiceView dice, CameraRig rig,
                               HudView hud, ProfileService profiles, int seed,
                               Dictionary<Seat, string> seatKinds, int botTier)
        {
            _session = session;
            _transport = transport;
            _board = board;
            _dice = dice;
            _cameraRig = rig;
            _hud = hud;
            _profiles = profiles;
            _seed = seed;
            _botTier = botTier;
            _seatKind.Clear();
            foreach (var kv in seatKinds) _seatKind[kv.Key] = kv.Value;

            _bots.Clear();
            foreach (Seat seat in session.Seats)
            {
                if (_seatKind.TryGetValue(seat, out var kind) && kind == "bot")
                    _bots[seat] = new HeuristicAgent((BotTier)Mathf.Clamp(botTier, 0, 2),
                        seed * 31 + (int)seat);
            }

            // M6: a guest's connection dropping mid-match converts their
            // seat to a bot immediately (match never stalls); if they rejoin
            // with proof, the seat flips back automatically.
            _transport.GuestDisconnected += OnGuestDisconnected;

            var animatorGo = new GameObject("HostAnimator");
            animatorGo.transform.SetParent(transform, false);
            _animator = animatorGo.AddComponent<EventAnimator>();
            _animator.Wire(_board, _dice, _cameraRig, _hud);
            _animator.EventStarted += e =>
            {
                EventStarted?.Invoke(e);
                EventStartedView?.Invoke(e);
            };
            _animator.EventAnimated += e =>
            {
                EventAnimatedView?.Invoke(e);
                if (e is MatchEnded) BatchCompleted?.Invoke();
            };

            _hud.RollPressed -= OnRollPressed;
            _hud.RollPressed += OnRollPressed;
            _hud.HideResults();

            _session.CommandAppliedLocal += OnCommandAppliedLocal;
        }

        void OnDestroy()
        {
            if (_session != null) _session.CommandAppliedLocal -= OnCommandAppliedLocal;
            if (_transport != null) _transport.Close();
        }

        void Update()
        {
            if (_transport != null)
                _transport.Pump((id, line) => _session.OnLine(id, line));
            if (_session != null && _session.MatchStarted)
                PollRejoinedGuests();

            if (_session.Runner == null || _processing || _session.Runner.IsMatchOver) return;
            MaybeRunBot();
        }

        void OnRollPressed()
        {
            if (_processing || _session.Runner == null || _session.Runner.IsMatchOver) return;
            if (_session.Runner.IsAwaitingRoll && IsLocalSeat(_session.Runner.CurrentSeat))
                _session.HostRoll();
        }

        bool IsLocalSeat(Seat seat) =>
            _seatKind.TryGetValue(seat, out var k) && (k == "host" || k == "bot");

        void OnGuestDisconnected(int guestId)
        {
            if (_session == null || !_session.MatchStarted) return;
            Seat? owned = _session.SeatOfGuest(guestId);
            if (owned == null || !_session.Seats.Contains(owned.Value)) return;
            _session.TakeOverSeat(owned.Value);
            _seatKind[owned.Value] = "bot";
            _bots[owned.Value] = new HeuristicAgent(
                (BotTier)Mathf.Clamp(_botTier, 0, 2), _seed * 31 + (int)owned.Value);
            _hud.Tick("Player left — seat taken over by a bot (they can rejoin anytime)");
            MaybeRunBot();
        }

        void PollRejoinedGuests()
        {
            // a rebind (proof rejoin) flips a bot seat back to "guest"
            var owners = _session.SeatOwners;
            foreach (var kv in _seatKind)
            {
                if (kv.Value != "bot") continue;
                if (owners.TryGetValue(kv.Key, out string owner)
                    && owner == "guest")
                {
                    _seatKind[kv.Key] = "guest";
                    _bots.Remove(kv.Key);
                    _hud.Tick("Player rejoined — seat restored");
                }
            }
        }

        void MaybeRunBot()
        {
            Seat seat = _session.Runner.CurrentSeat;
            if (_seatKind.TryGetValue(seat, out var kind) && kind != "bot") return;
            if (!_bots.TryGetValue(seat, out var agent)) return;
            StartCoroutine(BotTurn(seat, agent));
        }

        IEnumerator BotTurn(Seat seat, IOpponentAgent agent)
        {
            _processing = true;   // guard re-entry while waiting
            yield return new WaitForSeconds(0.9f);
            _processing = false;
            if (_session.Runner == null || _session.Runner.IsMatchOver) yield break;
            if (_session.Runner.CurrentSeat != seat) yield break;
            if (_session.Runner.IsAwaitingRoll)
            {
                _session.RollFor(seat);
            }
            else if (_session.Runner.IsAwaitingMove)
            {
                var moves = _session.Runner.PendingMoves;
                if (moves.Count > 0)
                    _session.MoveFor(seat, agent.ChooseMove(_session.Runner.State, moves).TokenIndex);
            }
        }

        void OnCommandAppliedLocal(Command command, IReadOnlyList<GameEvent> events)
        {
            _processing = true;
            _animator.AnimateBatch(events, () =>
            {
                _processing = false;
                RefreshHud();
                if (_session.Runner.IsMatchOver) BatchCompleted?.Invoke();
            });
        }

        public void RefreshHud()
        {
            if (_session.Runner == null || _session.Runner.IsMatchOver) return;
            Seat seat = _session.Runner.CurrentSeat;
            _seatKind.TryGetValue(seat, out var kind);
            bool local = kind == "host";
            _hud.SetTurn(Seats.Name(seat), local,
                         local && _session.Runner.IsAwaitingRoll,
                         local && _session.Runner.IsAwaitingMove);
        }

        /// <summary>Local tap on one of this device's tokens (host seat).</summary>
        public void TryLocalMove(int tokenIndex)
        {
            if (_processing || _session.Runner == null) return;
            if (_session.Runner.IsAwaitingMove && IsLocalSeat(_session.Runner.CurrentSeat))
                _session.HostMove(tokenIndex);
        }
    }

    /// <summary>Guest-device controller: sends intents, shadow-replays the
    /// host's verified commands, animates the same events. No local rules
    /// authority — the guest cannot diverge (every command is hash-checked
    /// against the host's broadcast state hash).</summary>
    public class OnlineGuestController : MonoBehaviour, IBoardStateView
    {
        NetGuestSession _session;
        LanTransport.GuestSide _transport;
        BoardBuilder _board;
        DiceView _dice;
        CameraRig _cameraRig;
        HudView _hud;
        EventAnimator _animator;
        bool _processing;

        public NetGuestSession Session => _session;

        // ------------------------------------------------ IBoardStateView

        public Seat[] SeatsInPlay => _session.Config.SeatsInPlay;
        public int TokenProgressOf(Seat seat, int tokenIndex) =>
            _session.State.Token(seat, tokenIndex).Progress;
        public bool IsMatchOver => _session.State.Phase == TurnPhase.MatchOver;
        public Seat CurrentSeat => IsMatchOver ? (Seat)(-1) : _session.State.CurrentSeat;

        public event System.Action<GameEvent> EventAnimatedView;
        public event System.Action<GameEvent> EventStarted;
        public event System.Action<GameEvent> EventStartedView;

        public void Initialize(NetGuestSession session, LanTransport.GuestSide transport,
                               BoardBuilder board, DiceView dice, CameraRig rig, HudView hud)
        {
            _session = session;
            _transport = transport;
            _board = board;
            _dice = dice;
            _cameraRig = rig;
            _hud = hud;

            var animatorGo = new GameObject("GuestAnimator");
            animatorGo.transform.SetParent(transform, false);
            _animator = animatorGo.AddComponent<EventAnimator>();
            _animator.Wire(_board, _dice, _cameraRig, _hud);
            _animator.EventStarted += e =>
            {
                EventStarted?.Invoke(e);
                EventStartedView?.Invoke(e);
            };
            _animator.EventAnimated += e => EventAnimatedView?.Invoke(e);

            _hud.RollPressed -= OnRollPressed;
            _hud.RollPressed += OnRollPressed;
            _hud.HideResults();
        }

        bool _linkLostShown;
        bool _clearedPersist;

        void Update()
        {
            if (_transport != null)
                _transport.Pump(line => _session.OnLine(line));

            // M6: connection lost → tell the player how to get back
            // (the host auto-converts the seat to a bot meanwhile)
            if (!_linkLostShown && _transport != null && !_transport.Connected
                && _session.State != null)
            {
                _linkLostShown = true;
                _hud.Tick("Connection lost — tap ONLINE to REJOIN the match");
            }

            // match over → the persisted rejoin key is stale, clear it
            if (_session.State != null && _session.State.Phase == TurnPhase.MatchOver
                && !_clearedPersist)
            {
                _clearedPersist = true;
                PlayerPrefs.DeleteKey(OnlineLobby.PersistKey);
                PlayerPrefs.Save();
            }

            // guest input: tap own token to move (roll via HUD button)
            if (_session.State == null || _processing) return;
            if (!_session.MyTurn) return;
            if (_session.State.Phase == TurnPhase.AwaitMove && GrayBoxInput.TapDown())
            {
                TokenView tapped = RaycastToken();
                if (tapped != null && (int)tapped.Seat == _session.SeatIndex)
                {
                    var moves = RulesEngine.GetLegalMoves(
                        _session.State, _session.State.CurrentSeat, _session.State.LastDie);
                    foreach (LegalMove m in moves)
                    {
                        if (m.TokenIndex == tapped.Index)
                        {
                            _session.RequestMove(tapped.Index);
                            break;
                        }
                    }
                }
            }
        }

        void OnRollPressed()
        {
            if (_processing || _session.State == null) return;
            if (_session.MyTurn && _session.State.Phase == TurnPhase.AwaitRoll)
                _session.RequestRoll();
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

        /// <summary>Wire shadow-applied commands to the animator. During a
        /// replay (rejoin/resync) commands are applied to the shadow state
        /// but NOT animated — the board snaps via SyncAll on `resynced`
        /// (M6: rejoining a running match is instant, not a slow re-watch).</summary>
        public void BindShadowToAnimator()
        {
            _session.CommandAppliedShadow += (command, events) =>
            {
                if (_session.Replaying) return;
                _processing = true;
                _animator.AnimateBatch(events, () => { _processing = false; RefreshHud(); });
            };
            _session.Resynced += () =>
            {
                if (_session.State == null) return;
                _animator.SyncAll(_session.State);
                RefreshHud();
            };
        }

        /// <summary>Called when the seeds land and the shadow state begins:
        /// snap tokens, aim the camera at the guest's seat, first HUD paint.</summary>
        public void OnShadowReady()
        {
            if (_session.State == null || _animator == null) return;
            _animator.SyncAll(_session.State);
            Seat my = (Seat)_session.SeatIndex;
            _cameraRig.SetSeat(my, instant: true);
            RefreshHud();
        }

        public void RefreshHud()
        {
            if (_session.State == null) return;
            if (_session.State.Phase == TurnPhase.MatchOver) return;
            bool mine = _session.MyTurn;
            _hud.SetTurn(Seats.Name(_session.State.CurrentSeat), mine,
                         mine && _session.State.Phase == TurnPhase.AwaitRoll,
                         mine && _session.State.Phase == TurnPhase.AwaitMove);
        }
    }
}
