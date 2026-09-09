using System.Collections;
using DesiLudo3D.Rules;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>Game-feel choreography on the controller's event stream:
    /// instant SFX, capture slow-motion, camera shake, event flashes.
    /// Presentation-only — it never touches rules state.</summary>
    public class GameFeel : MonoBehaviour
    {
        IBoardStateView _view;
        Image _flash;
        Coroutine _slowmo;

        /// <summary>Attach to any game mode (offline/online host/online
        /// guest) — the feel layer is view-agnostic.</summary>
        public void AttachView(IBoardStateView view)
        {
            if (_view != null) _view.EventStartedView -= OnEvent;
            _view = view;
            _view.EventStartedView += OnEvent;
        }

        /// <summary>Offline convenience (kept for the M3 call sites).</summary>
        public void Attach(PassAndPlayController controller) => AttachView(controller);

        void OnDestroy()
        {
            if (_view != null) _view.EventStartedView -= OnEvent;
        }

        void OnEvent(GameEvent e)
        {
            switch (e)
            {
                case DiceRolled d:
                    Sfx.Play("roll", 0.5f);
                    if (d.Die == 6) Sfx.Play("six", 0.4f);
                    break;

                case NoLegalMove _:
                    Sfx.Play("nomove", 0.4f);
                    break;

                case TokenMoved m:
                    if (m.IsRelease) Sfx.Play("release", 0.45f);
                    break;

                case TokenCaptured _:
                    Sfx.Play("capture", 0.55f);
                    StartCoroutine(Delayed(0.25f, () => Sfx.Play("captured", 0.4f)));
                    if (_slowmo != null) StopCoroutine(_slowmo);
                    _slowmo = StartCoroutine(Slowmo(0.5f, 0.42f));
                    StartCoroutine(Shake(0.55f, 0.16f));
                    break;

                case TokenFinished _:
                    Sfx.Play("finish", 0.5f);
                    break;

                case PlayerFinished _:
                    Sfx.Play("finish", 0.65f);
                    Flash(new Color(1f, 0.9f, 0.55f), 0.22f);
                    break;

                case TurnVoided _:
                    Sfx.Play("void", 0.6f);
                    Flash(new Color(0.9f, 0.15f, 0.1f), 0.3f);
                    StartCoroutine(Shake(0.4f, 0.1f));
                    break;

                case MatchEnded _:
                    Sfx.Play("victory", 0.6f);
                    Flash(new Color(1f, 0.85f, 0.4f), 0.35f);
                    break;
            }
        }

        IEnumerator Delayed(float seconds, System.Action action)
        {
            yield return new WaitForSeconds(seconds);
            action();
        }

        /// <summary>Brief world slow-motion (real-time restored after).</summary>
        IEnumerator Slowmo(float seconds, float scale)
        {
            Time.timeScale = scale;
            yield return new WaitForSecondsRealtime(seconds);
            Time.timeScale = 1f;
            _slowmo = null;
        }

        IEnumerator Shake(float seconds, float strength)
        {
            Camera cam = _view != null ? Camera.main : null;
            if (cam == null) yield break;
            Transform t = cam.transform;
            Vector3 original = t.localPosition;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                float k = 1f - elapsed / seconds;   // decays to zero
                t.localPosition = original + new Vector3(
                    (Random.value * 2f - 1f) * strength * k,
                    (Random.value * 2f - 1f) * strength * k,
                    0f);
                yield return null;
            }
            t.localPosition = original;
        }

        void Flash(Color color, float seconds)
        {
            EnsureFlash();
            if (_flash == null) return;
            _flash.color = new Color(color.r, color.g, color.b, 0.55f);
            StartCoroutine(FadeFlash(seconds));
        }

        IEnumerator FadeFlash(float seconds)
        {
            float a = _flash.color.a;
            float elapsed = 0f;
            while (elapsed < seconds)
            {
                elapsed += Time.unscaledDeltaTime;
                _flash.color = new Color(_flash.color.r, _flash.color.g, _flash.color.b,
                    Mathf.Lerp(a, 0f, elapsed / seconds));
                yield return null;
            }
            _flash.color = new Color(_flash.color.r, _flash.color.g, _flash.color.b, 0f);
        }

        void EnsureFlash()
        {
            if (_flash != null) return;
            var canvasGo = new GameObject("FeelFlashCanvas", typeof(Canvas),
                                           typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;   // above everything, non-interactive
            var imgGo = new GameObject("Flash", typeof(Image));
            imgGo.transform.SetParent(canvasGo.transform, false);
            _flash = imgGo.GetComponent<Image>();
            _flash.color = new Color(0f, 0f, 0f, 0f);
            _flash.raycastTarget = false;
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }
    }
}
