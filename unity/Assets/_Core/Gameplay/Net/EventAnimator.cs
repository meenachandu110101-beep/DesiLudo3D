using System.Collections;
using System.Collections.Generic;
using DesiLudo3D.Rules;
using UnityEngine;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Shared event→animation pipeline (extracted from
    /// PassAndPlayController in M5 so the ONLINE host/guest controllers
    /// animate through the exact same code path — one feel everywhere).
    /// Serializes batches so animations never overlap.</summary>
    public class EventAnimator : MonoBehaviour
    {
        BoardBuilder _board;
        DiceView _dice;
        CameraRig _rig;
        HudView _hud;
        bool _busy;

        /// <summary>Fired the moment each event arrives in a batch (instant
        /// feedback: SFX/slow-mo) and after its animation completes (comedy,
        /// clip overlay, autosave).</summary>
        public event System.Action<GameEvent> EventStarted;
        public event System.Action<GameEvent> EventAnimated;

        public bool Busy => _busy;

        public void Wire(BoardBuilder board, DiceView dice, CameraRig rig, HudView hud)
        {
            _board = board;
            _dice = dice;
            _rig = rig;
            _hud = hud;
        }

        /// <summary>Animate a batch of events, then invoke `done` (turn flow,
        /// autosave, bot scheduling attach there).</summary>
        public void AnimateBatch(IReadOnlyList<GameEvent> events, System.Action done)
        {
            if (_busy) StartCoroutine(QueueBatch(events, done));
            else StartCoroutine(RunBatch(events, done));
        }

        IEnumerator QueueBatch(IReadOnlyList<GameEvent> events, System.Action done)
        {
            while (_busy) yield return null;
            yield return RunBatch(events, done);
        }

        IEnumerator RunBatch(IReadOnlyList<GameEvent> events, System.Action done)
        {
            _busy = true;
            foreach (GameEvent e in events)
            {
                EventStarted?.Invoke(e);
                yield return HandleEvent(e);
                EventAnimated?.Invoke(e);
            }
            _busy = false;
            done?.Invoke();
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
                    _hud.Tick(UiStrings.F("noMove", Seats.Name(n.Seat), n.Die));
                    yield return new WaitForSeconds(0.7f);
                    break;

                case TokenMoved m:
                    yield return AnimateTokenMove(m);
                    break;

                case TokenCaptured c:
                {
                    var victim = _board.Tokens[(int)c.VictimSeat * BoardGeometry.TokensPerSeat + c.VictimIndex];
                    _hud.Tick(UiStrings.F("cut", Seats.Name(c.AttackerSeat), Seats.Name(c.VictimSeat)));
                    yield return victim.AnimateCaptureHome();
                    break;
                }

                case TokenFinished f:
                    _hud.Tick(UiStrings.F("tokenHome", Seats.Name(f.Seat)));
                    yield return new WaitForSeconds(0.35f);
                    break;

                case PlayerFinished p:
                    _hud.Tick(UiStrings.F("playerFinish", Seats.Name(p.Seat), p.Place));
                    yield return new WaitForSeconds(1.0f);
                    break;

                case TurnVoided v:
                    _hud.Tick(UiStrings.F("voided", Seats.Name(v.Seat)));
                    yield return new WaitForSeconds(1.0f);
                    break;

                case TurnEnded t:
                    _rig.SetSeat(t.NextSeat);
                    yield return new WaitForSeconds(0.35f);
                    break;

                case PlayerLeft l:
                    _hud.Tick(UiStrings.F("left", Seats.Name(l.Seat)));
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

        public void ShowResults(MatchEnded end)
        {
            var lines = new System.Text.StringBuilder(UiStrings.Get("matchOver") + "\n\n");
            foreach (Standing s in end.Standings)
                lines.Append("#").Append(s.Place).Append("  ").Append(Seats.Name(s.Seat))
                     .Append("  ").Append(UiStrings.F("homeCount", s.TokensHome)).Append("\n");
            _hud.ShowResults(lines.ToString());
        }

        /// <summary>Snap every token to the given rules state (boot/resume).</summary>
        public void SyncAll(GameState state)
        {
            foreach (Seat seat in state.Config.SeatsInPlay)
            {
                for (int i = 0; i < BoardGeometry.TokensPerSeat; i++)
                {
                    var token = _board.Tokens[(int)seat * BoardGeometry.TokensPerSeat + i];
                    int progress = state.Token(seat, i).Progress;
                    if (progress == TokenProgress.Base) token.SnapToBase();
                    else token.SnapToProgress(progress);
                }
            }
        }
    }
}
