using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace DesiLudo3D.Gameplay
{
    /// <summary>A speech bubble that follows a token on screen (screen-space
    /// overlay — always crisp at any camera angle/zoom, portrait or
    /// landscape). Speech is the PRIMARY comedy channel (A4); bubbles are the
    /// readable secondary surface and always accompany the spoken line.</summary>
    public class SpeechBubbleView : MonoBehaviour
    {
        static Sprite _bubbleSprite;

        Transform _anchor;
        Vector3 _anchorOffset;
        Text _text;
        RectTransform _rect;
        CanvasGroup _group;
        float _life;
        float _age;

        public static SpeechBubbleView Show(Transform anchor, string text, float duration,
                                            Color accent)
        {
            var parent = EnsureOverlayCanvas();
            var go = new GameObject("SpeechBubble", typeof(RectTransform),
                                    typeof(CanvasGroup), typeof(Image));
            go.transform.SetParent(parent, false);
            var bubble = go.AddComponent<SpeechBubbleView>();
            bubble._anchor = anchor;
            bubble._anchorOffset = Vector3.up * 0.55f;
            bubble._life = duration;

            bubble._rect = (RectTransform)go.transform;
            bubble._rect.sizeDelta = new Vector2(640f, 200f);

            bubble._group = go.GetComponent<CanvasGroup>();
            var img = go.GetComponent<Image>();
            img.sprite = BubbleSprite();
            img.type = Image.Type.Sliced;
            img.color = new Color(1f, 0.97f, 0.9f, 0.96f);

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(Text));
            txtGo.transform.SetParent(go.transform, false);
            bubble._text = txtGo.GetComponent<Text>();
            bubble._text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            bubble._text.fontSize = 44;
            bubble._text.fontStyle = FontStyle.Bold;
            bubble._text.color = new Color(0.18f, 0.09f, 0.04f);
            bubble._text.alignment = TextAnchor.MiddleCenter;
            bubble._text.horizontalOverflow = HorizontalWrapMode.Wrap;
            bubble._text.verticalOverflow = VerticalWrapMode.Overflow;
            bubble._text.text = text;
            var tr = (RectTransform)txtGo.transform;
            tr.Stretch();
            tr.offsetMin = new Vector2(28f, 18f);
            tr.offsetMax = new Vector2(-28f, -18f);

            // accent bar (seat colour) so four players are distinguishable
            var bar = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            bar.transform.SetParent(go.transform, false);
            var barRect = (RectTransform)bar.transform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = Vector2.zero;
            barRect.sizeDelta = new Vector2(0f, 14f);
            bar.GetComponent<Image>().color = accent;

            bubble.Position();
            return bubble;
        }

        static Canvas EnsureOverlayCanvas()
        {
            var existing = GameObject.Find("ComedyOverlayCanvas");
            if (existing != null) return existing.GetComponent<Canvas>();
            var go = new GameObject("ComedyOverlayCanvas", typeof(Canvas),
                                    typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;   // above HUD, below nothing critical
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        static Sprite BubbleSprite()
        {
            if (_bubbleSprite != null) return _bubbleSprite;
            // 9-sliced rounded rectangle, generated at runtime (no assets).
            const int size = 64, radius = 22;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.wrapMode = TextureWrapMode.Clamp;
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    byte a = 255;
                    // corner distance test (keep borders opaque for slicing)
                    int cx = Mathf.Clamp(x, radius, size - 1 - radius);
                    int cy = Mathf.Clamp(y, radius, size - 1 - radius);
                    float d = Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
                    if (d > radius) a = 0;
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _bubbleSprite = Sprite.Create(tex, new Rect(0, 0, size, size),
                new Vector2(0.5f, 0.5f), 32f, 0,
                SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
            return _bubbleSprite;
        }

        void Position()
        {
            if (_anchor == null) return;
            Camera cam = Camera.main;
            if (cam == null) return;
            Vector3 world = _anchor.position + _anchorOffset;
            Vector3 screen = cam.WorldToScreenPoint(world);
            var canvas = GetComponentInParent<Canvas>().rootCanvas;
            RectTransform canvasRect = (RectTransform)canvas.transform;
            Vector2 local;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                canvasRect, screen, canvas.renderMode == RenderMode.ScreenSpaceOverlay
                    ? null : cam, out local);
            // clamp inside screen
            float halfW = canvasRect.rect.width * 0.5f - 340f;
            float halfH = canvasRect.rect.height * 0.5f - 120f;
            local.x = Mathf.Clamp(local.x, -Mathf.Max(halfW, 0f), Mathf.Max(halfW, 0f));
            local.y = Mathf.Clamp(local.y, -Mathf.Max(halfH, 0f), Mathf.Max(halfH, 0f));
            _rect.anchoredPosition = local;
        }

        void Update()
        {
            _age += Time.deltaTime;
            Position();
            const float fade = 0.35f;
            float alpha = _age > _life - fade ? Mathf.Max(0f, (_life - _age) / fade) : 1f;
            if (_group != null) _group.alpha = alpha;
            if (_age >= _life) Destroy(gameObject);
        }
    }
}
