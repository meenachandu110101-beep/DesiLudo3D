using System.Collections;
using System.Collections.Generic;
using DesiLudo3D.Core;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Drives a local match: pass-and-play humans + placeholder bots,
    /// event-to-animation pipeline, camera rotation, HUD state. M3 layers the
    /// ReactionDirector (voice/bubble comedy) onto the same event stream; M5/M6
    /// swap the local input sources for NetTransport commands — this controller
    /// is the offline reference implementation of the MatchRunner host loop.</summary>
    public class PassAndPlayController : MonoBehaviour
    {
        MatchRunner _runner;
        MatchSetup _setup;
        BoardBuilder _board;
        DiceView _dice;
        CameraRig _cameraRig;
        HudView _hud;

        readonly Dictionary<Seat, IOpponentAgent> _bots = new Dictionary<Seat, IOpponentAgent>();
        bool _processing;
        bool _restartRequested;

        public void Initialize(MatchSetup setup, BoardBuilder board, DiceView dice,
                               CameraRig rig, HudView hud, int seed)
        {
            _setup = setup;
            _board = board;
            _dice = dice;
            _cameraRig = rig;
            _hud = hud;

            _runner = new MatchRunner(setup.BuildConfig(), new SeededRandomDiceProvider(seed));
            _runner.CommandApplied += OnCommandApplied;

            _bots.Clear();
            foreach (Seat seat in _runner.Config.SeatsInPlay)
            {
                if (setup.KindFor(seat) == ControllerKind.Bot)
                    _bots[seat] = new RandomAgent(seed * 31 + (int)seat);
            }

            _hud.RollPressed -= OnRollPressed;
            _hud.RollPressed += OnRollPressed;
            _hud.RestartPressed -= OnRestartPressed;
            _hud.RestartPressed += OnRestartPressed;
            _hud.HideResults();
            _hud.Tick("");

            SyncAllTokens();
            _cameraRig.SetSeat(_runner.CurrentSeat, instant: true);
            RefreshHud();
        }

        void OnDestroy()
        {
            if (_runner != null) _runner.CommandApplied -= OnCommandApplied;
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
                _hud.Tick("Illegal: " + error);
                return;
            }
            StartCoroutine(ProcessBatch(events));
        }

        IEnumerator ProcessBatch(IReadOnlyList<GameEvent> events)
        {
            _processing = true;
            foreach (GameEvent e in events)
                yield return HandleEvent(e);
            _processing = false;
            RefreshHud();
            MaybeStartBotTurn();
        }

        IEnumerator HandleEvent(GameEvent e)
        {
            switch (e)
            {
                case DiceRolled d:
                    _hud.Tick("");
                    yield return _dice.AnimateRoll(d.Die);
                    _hud.ShowDie(d.Die);
                    break;

                case NoLegalMove n:
                    _hud.Tick(Seats.Name(n.Seat) + " has no legal move (" + n.Die + ")");
                    yield return new WaitForSeconds(0.7f);
                    break;

                case TokenMoved m:
                    yield return AnimateTokenMove(m);
                    break;

                case TokenCaptured c:
                    {
                        var victim = _board.Tokens[(int)c.VictimSeat * BoardGeometry.TokensPerSeat + c.VictimIndex];
                        _hud.Tick(Seats.Name(c.AttackerSeat) + " cut " + Seats.Name(c.VictimSeat) + "'s token!");
                        yield return victim.AnimateCaptureHome();
                        break;
                    }

                case TokenFinished f:
                    _hud.Tick(Seats.Name(f.Seat) + "'s token reached home!");
                    yield return new WaitForSeconds(0.35f);
                    break;

                case PlayerFinished p:
                    _hud.Tick(Seats.Name(p.Seat) + " FINISHED — place " + p.Place + "!");
                    yield return new WaitForSeconds(1.0f);
                    break;

                case TurnVoided v:
                    _hud.Tick("Three sixes! " + Seats.Name(v.Seat) + "'s turn is VOID!");
                    yield return new WaitForSeconds(1.0f);
                    break;

                case TurnEnded t:
                    _cameraRig.SetSeat(t.NextSeat);
                    yield return new WaitForSeconds(0.35f);
                    break;

                case PlayerLeft l:
                    _hud.Tick(Seats.Name(l.Seat) + " left the match");
                    break;

                case MatchEnded end:
                    ShowResults(end);
                    break;
            }
        }

        IEnumerator AnimateTokenMove(TokenMoved m)
        {
            TokenView token = _board.Tokens[(int)m.Seat * BoardGeometry.TokensPerSeat + m.TokenIndex];
            var waypoints = new List<Vector3>();

            if (m.IsRelease)
            {
                waypoints.Add(TokenView.ProgressToWorld(m.Seat, 0));
            }
            else
            {
                for (int p = m.From + 1; p <= m.To; p++)
                    waypoints.Add(TokenView.ProgressToWorld(m.Seat, p));
            }

            yield return token.AnimateMove(waypoints, m.IsRelease, m.To == TokenProgress.Finished);
        }

        void ShowResults(MatchEnded end)
        {
            var lines = new System.Text.StringBuilder("MATCH OVER!\n\n");
            foreach (Standing s in end.Standings)
                lines.Append("#").Append(s.Place).Append("  ").Append(Seats.Name(s.Seat))
                     .Append("  (").Append(s.TokensHome).Append(" home)\n");
            _hud.ShowResults(lines.ToString());
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

        void RefreshHud()
        {
            if (_runner.IsMatchOver) return;
            bool human = _setup.KindFor(_runner.CurrentSeat) == ControllerKind.Human;
            _hud.SetTurn(Seats.Name(_runner.CurrentSeat), human,
                         human && _runner.IsAwaitingRoll,
                         human && _runner.IsAwaitingMove);
        }

        void SyncAllTokens()
        {
            foreach (Seat seat in _runner.Config.SeatsInPlay)
            {
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                {
                    var token = _board.Tokens[(int)seat * BoardGeometry.TokensPerSeat + i];
                    int progress = _runner.State.Token(seat, i).Progress;
                    if (progress == TokenProgress.Base) token.SnapToBase();
                    else token.SnapToProgress(progress);
                }
            }
        }
    }
}
