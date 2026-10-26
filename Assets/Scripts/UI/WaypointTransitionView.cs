using System;
using System.Collections;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 웨이포인트 이동 연출 (10/26, Figma 15-A). 인트로 로딩(13-A)과 구분되는 짧은 화면 전환.
    /// - 차가운 푸른빛 어둠 + 회전하는 인장 고리 3겹 + 빛나는 마름모 (10/26: 흰 구름 장막 제거, 덮기·걷힘은 페이드)
    /// - "웨이포인트 이동" / 목적지 이름(Serif 64) / 출발 → 도착 / 가운데 아래 가는 진행선
    /// 흐름: 덮기 0.35초 → (가려진 동안 순간 이동) → 유지 0.9초 → 걷힘 0.5초. 연출 중 전투 입력 차단.
    /// 같은 씬 안 순간 이동이라 실제 로딩은 없습니다. WaypointTraveler.TravelTo가 호출합니다.
    /// </summary>
    public class WaypointTransitionView : MonoBehaviour
    {
        private const float WIDTH = 1920f;
        private const float HEIGHT = 1080f;
        private const float COVER = 0.35f;
        private const float HOLD = 0.9f;
        private const float REVEAL = 0.5f;
        private static readonly Color RING = new Color(0.75f, 0.83f, 1f);

        private CanvasGroup _group;
        private RectTransform[] _rings;
        private float[] _ringSpeed;
        private Image _glow;
        private RectTransform _progress;
        private float _elapsed;

        public static bool IsPlaying { get; private set; }

        /// <summary>연출 시작. warp = 화면이 가려진 순간 실행 (순간 이동), done = 끝난 뒤</summary>
        public static void Play(string from, string to, Action warp, Action done = null)
        {
            var go = new GameObject("WaypointTransition");
            DontDestroyOnLoad(go);
            WaypointTransitionView view = go.AddComponent<WaypointTransitionView>();
            view.Build(from, to);
            view.StartCoroutine(view.Run(warp, done));
        }

        private IEnumerator Run(Action warp, Action done)
        {
            IsPlaying = true;
            bool wasBlocked = GameplayInput.IsBlocked;
            GameplayInput.IsBlocked = true;

            // 덮기: 화면이 어두워짐
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / COVER)
            {
                SetCover(Ease(t));
                yield return null;
            }

            SetCover(1f);

            try
            {
                warp?.Invoke();
            }
            catch (Exception e)
            {
                Debug.LogException(e);
            }

            // 유지: 진행선이 차오름 (고리 회전·빛 맥동은 Update)
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / HOLD)
            {
                _progress.sizeDelta = new Vector2(520f * Ease(t), 3f);
                yield return null;
            }

            _progress.sizeDelta = new Vector2(520f, 3f);

            // 걷힘
            for (float t = 0f; t < 1f; t += Time.unscaledDeltaTime / REVEAL)
            {
                SetCover(1f - Ease(t));
                yield return null;
            }

            GameplayInput.IsBlocked = wasBlocked;
            IsPlaying = false;
            done?.Invoke();
            Destroy(gameObject);
        }

        private void Update()
        {
            _elapsed += Time.unscaledDeltaTime;

            for (int i = 0; i < _rings.Length; i++)
            {
                _rings[i].Rotate(0f, 0f, _ringSpeed[i] * Time.unscaledDeltaTime);
            }

            float pulse = 0.85f + 0.15f * Mathf.Sin(_elapsed * 6f);
            _glow.rectTransform.localScale = new Vector3(pulse, pulse, 1f);
        }

        // 0 = 걷힘(투명), 1 = 덮음
        private void SetCover(float k)
        {
            _group.alpha = k;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        // ------------------------------------------------------------------
        // 화면 구성 (Figma 15-A)
        // ------------------------------------------------------------------

        private void Build(string from, string to)
        {
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 900;
            CanvasScaler scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(WIDTH, HEIGHT);
            scaler.matchWidthOrHeight = 0.5f;
            _group = gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.alpha = 0f;
            RectTransform root = (RectTransform)transform;

            // 배경: 가운데가 조금 밝은 푸른 어둠
            Image back = HudGraphics.CreateRect("Back", root).gameObject.AddComponent<Image>();
            back.sprite = RadialSprite(new Color(0.11f, 0.14f, 0.2f), new Color(0.02f, 0.024f, 0.04f));
            back.raycastTarget = false;
            HudGraphics.Stretch(back.rectTransform);

            // 인장 고리 3겹 + 빛 + 마름모 (중심 960, 위에서 430)
            Vector2 center = new Vector2(0f, HEIGHT * 0.5f - 430f);
            _rings = new RectTransform[3];
            _ringSpeed = new[] { 12f, -70f, 45f };
            (float radius, float thickness, float alpha, float span)[] rings = { (170f, 1.5f, 0.35f, 360f), (140f, 3f, 0.85f, 288f), (116f, 1.5f, 0.5f, 306f) };

            for (int i = 0; i < rings.Length; i++)
            {
                Image ring = HudGraphics.CreateRect("Ring" + i, root).gameObject.AddComponent<Image>();
                ring.sprite = ArcSprite(rings[i].thickness / rings[i].radius, rings[i].span);
                ring.color = new Color(RING.r, RING.g, RING.b, rings[i].alpha);
                ring.raycastTarget = false;
                UiKit.Place(ring.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, Vector2.one * rings[i].radius * 2f);
                _rings[i] = ring.rectTransform;
            }

            _glow = HudGraphics.CreateRect("Glow", root).gameObject.AddComponent<Image>();
            _glow.sprite = SoftSprite();
            _glow.color = new Color(0.85f, 0.9f, 1f, 0.9f);
            _glow.raycastTarget = false;
            UiKit.Place(_glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(170f, 170f));

            Image diamond = UiKit.Box("Diamond", root, Color.white);
            UiKit.Place(diamond.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), center, new Vector2(30f, 30f));
            diamond.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);

            // 글자 (위에서 650부터)
            Text label = UiKit.Label("Label", root, 20, RING, TextAnchor.MiddleCenter, FontStyle.Bold);
            label.text = "웨 이 포 인 트   이 동";
            UiKit.Place(label.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -650f), new Vector2(800f, 30f));

            Text name = UiKit.Title("Destination", root, 64, new Color(0.95f, 0.96f, 1f), TextAnchor.MiddleCenter);
            name.text = to;
            UiKit.Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -688f), new Vector2(1400f, 84f));

            Image line = UiKit.Box("Line", root, new Color(RING.r, RING.g, RING.b, 0.6f));
            UiKit.Place(line.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -782f), new Vector2(360f, 1.5f));

            Text route = UiKit.Label("Route", root, 20, new Color(0.68f, 0.71f, 0.78f), TextAnchor.MiddleCenter);
            route.text = string.IsNullOrEmpty(from) ? to : $"{from}  →  {to}";
            UiKit.Place(route.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -794f), new Vector2(1200f, 30f));   // 선과 간격 12 (보조 글 여백 규칙)

            // 진행선 (가운데 아래)
            Image track = UiKit.Box("Track", root, new Color(1f, 1f, 1f, 0.15f));
            UiKit.Place(track.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0.5f), new Vector2(0f, 120f), new Vector2(520f, 3f));
            Image fill = UiKit.Box("Fill", track.rectTransform, Color.white);
            UiKit.Place(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 3f));
            _progress = fill.rectTransform;

            SetCover(0f);
        }

        // ------------------------------------------------------------------
        // 그림 (코드 생성)
        // ------------------------------------------------------------------

        private static Sprite _soft;

        private static Sprite SoftSprite()
        {
            if (_soft != null)
            {
                return _soft;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TransitionSoft", wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy) * 2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }

            texture.Apply();
            _soft = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return _soft;
        }

        private static Sprite RadialSprite(Color inner, Color outer)
        {
            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TransitionBack", wrapMode = TextureWrapMode.Clamp };

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float d = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / 0.707f);
                    texture.SetPixel(x, y, Color.Lerp(inner, outer, Mathf.SmoothStep(0f, 1f, d)));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }

        // 원호: 두께 비율(반지름 대비), 각도(도)
        private static Sprite ArcSprite(float thickness, float span)
        {
            const int size = 256;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "TransitionArc", wrapMode = TextureWrapMode.Clamp };
            float outer = 0.5f;
            float inner = outer * (1f - Mathf.Max(thickness, 0.012f));
            float edge = 1.2f / size;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size - 0.5f;
                    float dy = (y + 0.5f) / size - 0.5f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01((outer - r) / edge) * Mathf.Clamp01((r - inner) / edge);
                    float angle = (Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f) % 360f;
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, angle <= span ? a : 0f));
                }
            }

            texture.Apply();
            return Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
        }
    }
}
