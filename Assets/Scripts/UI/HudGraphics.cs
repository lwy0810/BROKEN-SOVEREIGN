using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 코드로 만든 HUD가 쓰는 공용 그래픽 (흰 스프라이트, 기본 폰트).
    /// 아트 에셋 도입 시 프리팹·스프라이트로 교체합니다.
    /// </summary>
    public static class HudGraphics
    {
        private static Sprite _whiteSprite;
        private static Font _font;

        /// <summary>Image의 Filled(재사용 대기 원형 막) 기능은 스프라이트가 있어야 동작</summary>
        public static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    Texture2D texture = Texture2D.whiteTexture;
                    _whiteSprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f));
                    _whiteSprite.name = "HudWhite";
                }

                return _whiteSprite;
            }
        }

        /// <summary>내장 동적 폰트. 숫자·영문 표시용 (한글 폰트는 툴팁 도입 시 준비)</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return _font;
            }
        }

        private static Sprite _circleSprite;
        private static Sprite _ringSprite;
        private static Sprite _arrowSprite;

        /// <summary>안티앨리어싱 원 (미니맵 마스크·아이콘)</summary>
        public static Sprite CircleSprite => _circleSprite != null ? _circleSprite : _circleSprite = MakeSprite("HudCircle", 128, (x, y) => Disc(x, y, 0.5f));

        /// <summary>원 테두리 (미니맵 외곽)</summary>
        public static Sprite RingSprite => _ringSprite != null ? _ringSprite : _ringSprite = MakeSprite("HudRing", 256, (x, y) => Disc(x, y, 0.5f) * (1f - Disc(x, y, 0.46f)));

        /// <summary>위를 가리키는 화살촉 (플레이어 위치)</summary>
        public static Sprite ArrowSprite => _arrowSprite != null ? _arrowSprite : _arrowSprite = MakeSprite("HudArrow", 64, Arrow);

        // 중심 (0.5, 0.5) 기준 반지름 radius 원 안쪽 = 1, 경계 1픽셀 부드럽게
        private static float Disc(float x, float y, float radius)
        {
            float d = Mathf.Sqrt((x - 0.5f) * (x - 0.5f) + (y - 0.5f) * (y - 0.5f));
            return Mathf.Clamp01((radius - d) * 128f + 0.5f);
        }

        // 아래가 오목한 화살촉: 꼭짓점 (0.5, 0.95), 양 날개 (0.12, 0.1)·(0.88, 0.1), 오목점 (0.5, 0.32)
        private static float Arrow(float x, float y)
        {
            bool Inside(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
            {
                float d1 = (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
                float d2 = (p.x - c.x) * (b.y - c.y) - (b.x - c.x) * (p.y - c.y);
                float d3 = (p.x - a.x) * (c.y - a.y) - (c.x - a.x) * (p.y - a.y);
                bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
                bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
                return !(neg && pos);
            }

            var point = new Vector2(x, y);
            var tip = new Vector2(0.5f, 0.95f);
            var notch = new Vector2(0.5f, 0.32f);
            bool left = Inside(point, tip, new Vector2(0.12f, 0.1f), notch);
            bool right = Inside(point, tip, notch, new Vector2(0.88f, 0.1f));
            return left || right ? 1f : 0f;
        }

        private static Sprite MakeSprite(string name, int size, System.Func<float, float, float> alpha)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp };
            var pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float a = alpha((x + 0.5f) / size, (y + 0.5f) / size);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply();
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            sprite.name = name;
            return sprite;
        }

        public static RectTransform CreateRect(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.layer = parent.gameObject.layer;
            RectTransform rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
