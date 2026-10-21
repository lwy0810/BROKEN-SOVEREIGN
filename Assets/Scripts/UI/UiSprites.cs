using System;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 테마 색으로 코드에서 만드는 UI 스프라이트 (10/21 B안). 아트 에셋 없이 금속판·기술 칸·막대·인장·팔각 테두리를 그립니다.
    /// 판·칸·막대는 9-slice(Image.Type.Sliced)로 늘려 씁니다. UiTheme가 바뀌면 다음 접근 때 다시 만듭니다.
    /// </summary>
    public static class UiSprites
    {
        private static int _version = -1;
        private static Sprite _plate;
        private static Sprite _tile;
        private static Sprite _barFrame;
        private static Sprite _pill;
        private static Sprite _round;
        private static Sprite _button;
        private static Sprite _buttonEquip;
        private static Sprite _seal;
        private static Sprite _octagonFill;
        private static Sprite _octagonFrame;
        private static Sprite _rune;
        private static Sprite _glow;
        private static Sprite _pin;
        private static Sprite _mouseWheel;
        private static Sprite _mouseRight;

        /// <summary>금속판 (패널·기술 바·대상 게이지 바탕). 테두리 16px</summary>
        public static Sprite Plate { get { Ensure(); return _plate; } }

        /// <summary>기술 칸 (안쪽 움푹 영역 포함)</summary>
        public static Sprite Tile { get { Ensure(); return _tile; } }

        /// <summary>막대 바탕 (움푹 + 테두리, 알약형)</summary>
        public static Sprite BarFrame { get { Ensure(); return _barFrame; } }

        /// <summary>흰 알약형 (막대 채움·색 입혀 사용)</summary>
        public static Sprite Pill { get { Ensure(); return _pill; } }

        /// <summary>흰 둥근 사각형 (목록 행·강조, 색 입혀 사용)</summary>
        public static Sprite Round { get { Ensure(); return _round; } }

        /// <summary>일반 버튼 (알약형 판)</summary>
        public static Sprite Button { get { Ensure(); return _button; } }

        /// <summary>장착·선택된 버튼 (금빛)</summary>
        public static Sprite ButtonEquip { get { Ensure(); return _buttonEquip; } }

        /// <summary>격 인장 (원형 판 + 안쪽 고리)</summary>
        public static Sprite Seal { get { Ensure(); return _seal; } }

        /// <summary>흰 팔각형 (미니맵 마스크)</summary>
        public static Sprite OctagonFill { get { Ensure(); return _octagonFill; } }

        /// <summary>팔각 테두리 (미니맵)</summary>
        public static Sprite OctagonFrame { get { Ensure(); return _octagonFrame; } }

        /// <summary>기술 칸 기본 문양 (기술 아이콘 도입 전)</summary>
        public static Sprite Rune { get { Ensure(); return _rune; } }

        /// <summary>흰 부드러운 빛 (번쩍임·강조)</summary>
        public static Sprite Glow { get { Ensure(); return _glow; } }

        /// <summary>지도 핀 (물방울 모양, 아래 끝이 위치)</summary>
        public static Sprite Pin { get { Ensure(); return _pin; } }

        /// <summary>마우스 아이콘: 휠 버튼 강조 (지도 안내)</summary>
        public static Sprite MouseWheel { get { Ensure(); return _mouseWheel; } }

        /// <summary>마우스 아이콘: 오른쪽 버튼 강조 (지도 안내)</summary>
        public static Sprite MouseRight { get { Ensure(); return _mouseRight; } }

        private static void Ensure()
        {
            if (_version == UiTheme.Version && _plate != null)
            {
                return;
            }

            _version = UiTheme.Version;
            UiPalette p = UiTheme.P;

            _plate = Make("UiPlate", 64, 64, 16, (x, y, w, h) =>
            {
                float d = RoundRect(x, y, w, h, 10f);
                Color c = Color.Lerp(p.PlateBottom, p.PlateTop, y / h);
                c = Stroke(c, p.PlateStroke, d, 2f);
                c = Line(c, p.InnerLine, d + 5f, 0.6f);
                return Cover(c, d);
            });

            _tile = Make("UiTile", 64, 64, 18, (x, y, w, h) =>
            {
                float d = RoundRect(x, y, w, h, 7f);
                Color c = Color.Lerp(p.TileBottom, p.TileTop, y / h);
                float inner = RoundRect(x, y, w, h, 4f, 7f);
                if (inner < 0.5f)
                {
                    c = Color.Lerp(c, p.Inset, Mathf.Clamp01(0.5f - inner));
                    c = Line(c, p.InsetStroke, inner + 0.5f, 0.6f);
                }

                c = Stroke(c, p.PlateStroke, d, 2f);
                return Cover(c, d);
            });

            _barFrame = Make("UiBarFrame", 48, 24, 11, (x, y, w, h) =>
            {
                float d = RoundRect(x, y, w, h, 12f);
                Color c = Stroke(p.Inset, p.PlateStroke, d, 1.5f);
                return Cover(c, d);
            });

            _pill = Make("UiPill", 48, 24, 11, (x, y, w, h) => Cover(Color.white, RoundRect(x, y, w, h, 12f)));
            _round = Make("UiRound", 32, 32, 10, (x, y, w, h) => Cover(Color.white, RoundRect(x, y, w, h, 6f)));

            _button = Make("UiButton", 64, 40, 19, (x, y, w, h) =>
            {
                float d = RoundRect(x, y, w, h, 20f);
                Color c = Color.Lerp(p.PlateBottom, p.PlateTop, y / h);
                return Cover(Stroke(c, p.PlateStroke, d, 1.5f), d);
            });

            _buttonEquip = Make("UiButtonEquip", 64, 40, 19, (x, y, w, h) =>
            {
                float d = RoundRect(x, y, w, h, 20f);
                Color c = Color.Lerp(p.EquipBottom, p.EquipTop, y / h);
                return Cover(Stroke(c, p.Title, d, 2f), d);
            });

            _seal = Make("UiSeal", 128, 128, 0, (x, y, w, h) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.5f));
                float d = r - (w * 0.5f - 1f);
                Color c = Color.Lerp(p.PlateTop, p.PlateBottom, Mathf.Clamp01(r / (w * 0.5f)));
                c = Line(c, p.PlateStroke, r - (w * 0.5f - 12f), 0.75f);
                c = Stroke(c, p.PlateStroke, d, 3f);
                return Cover(c, d);
            });

            _octagonFill = Make("UiOctagonFill", 128, 128, 0, (x, y, w, h) => Cover(Color.white, Octagon(x, y, w, 1f)));
            _octagonFrame = Make("UiOctagonFrame", 128, 128, 0, (x, y, w, h) =>
            {
                float d = Octagon(x, y, w, 1f);
                Color c = UiTheme.WithAlpha(p.Inset, 0f);
                c = Stroke(c, p.PlateStroke, d, 3f);
                c = Line(c, UiTheme.WithAlpha(p.PlateStroke, 0.5f), Octagon(x, y, w, 9f), 0.6f);
                return Cover(c, d);
            });

            // 4갈래 별 + 가운데 점 (기술 아이콘 자리)
            _rune = MakeSuper("UiRune", 64, (u, v) =>
            {
                float ax = Mathf.Abs(u - 0.5f), ay = Mathf.Abs(v - 0.5f);
                bool star = ax + ay * 3.2f < 0.46f || ay + ax * 3.2f < 0.46f;
                bool dot = (u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f) < 0.012f;
                if (dot) return p.Accent;
                return star ? p.Icon : Color.clear;
            });

            // 핀: 위쪽 원 + 아래로 뾰족한 꼬리, 진한 테두리, 가운데 점
            _pin = MakeSuper("UiPin", 64, (u, v) =>
            {
                float outer = PinShape(u, v, 0f);
                if (outer > 0f) return Color.clear;
                if ((u - 0.5f) * (u - 0.5f) + (v - 0.66f) * (v - 0.66f) < 0.012f) return new Color(0.1f, 0.1f, 0.12f);
                return PinShape(u, v, 0.06f) <= 0f ? p.Accent : new Color(0.1f, 0.1f, 0.12f);
            });

            _mouseWheel = MakeSuper("UiMouseWheel", 64, (u, v) => Mouse(u, v, p, true));
            _mouseRight = MakeSuper("UiMouseRight", 64, (u, v) => Mouse(u, v, p, false));

            _glow = Make("UiGlow", 64, 64, 0, (x, y, w, h) =>
            {
                float r = Vector2.Distance(new Vector2(x, y), new Vector2(w * 0.5f, h * 0.5f)) / (w * 0.5f);
                float a = Mathf.Clamp01(1f - r);
                return new Color(1f, 1f, 1f, a * a);
            });
        }

        // 핀 모양 거리 근사 (안 <= 0). shrink만큼 안쪽
        private static float PinShape(float u, float v, float shrink)
        {
            float circle = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.66f) * (v - 0.66f)) - (0.3f - shrink);

            // 꼬리: 원 아래 (0.5, 0.04)까지 좁아지는 삼각형
            float t = Mathf.InverseLerp(0.04f + shrink * 1.6f, 0.62f, v);
            float halfWidth = Mathf.Lerp(0f, 0.27f - shrink, t);
            float tail = v < 0.04f + shrink * 1.6f || v > 0.66f ? 1f : Mathf.Abs(u - 0.5f) - halfWidth;
            return Mathf.Min(circle, tail);
        }

        // 마우스 아이콘 (테두리 + 가운데 선, wheel이면 휠 강조, 아니면 오른쪽 버튼 강조)
        private static Color Mouse(float u, float v, UiPalette p, bool wheel)
        {
            float d = RoundRectUv(u, v, 0.5f, 0.5f, 0.24f, 0.42f, 0.22f);

            if (d > 0f)
            {
                return Color.clear;
            }

            Color line = p.Text;
            bool border = d > -0.05f;
            bool split = v > 0.58f && Mathf.Abs(u - 0.5f) < 0.022f;
            bool divider = Mathf.Abs(v - 0.58f) < 0.022f;

            if (border || divider || (split && !wheel))
            {
                return line;
            }

            if (wheel)
            {
                bool wheelArea = RoundRectUv(u, v, 0.5f, 0.74f, 0.06f, 0.11f, 0.05f) <= 0f;
                return wheelArea ? p.Accent : (split ? line : UiTheme.WithAlpha(p.Inset, 0.85f));
            }

            bool rightButton = u > 0.5f && v > 0.58f;
            return rightButton ? p.Accent : UiTheme.WithAlpha(p.Inset, 0.85f);
        }

        // uv 공간 둥근 사각형 (가운데 cx·cy, 반폭 hx·hy, 반지름 r)
        private static float RoundRectUv(float u, float v, float cx, float cy, float hx, float hy, float r)
        {
            float px = Mathf.Abs(u - cx) - (hx - r);
            float py = Mathf.Abs(v - cy) - (hy - r);
            return new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - r;
        }

        // 둥근 사각형 부호 거리 (안 < 0). inset만큼 안쪽 사각형
        internal static float RoundRect(float x, float y, float w, float h, float radius, float inset = 0f)
        {
            float hx = w * 0.5f - inset, hy = h * 0.5f - inset;
            float px = Mathf.Abs(x - w * 0.5f) - (hx - radius);
            float py = Mathf.Abs(y - h * 0.5f) - (hy - radius);
            float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude;
            return outside + Mathf.Min(Mathf.Max(px, py), 0f) - radius;
        }

        private static float Octagon(float x, float y, float size, float inset)
        {
            float ax = Mathf.Abs(x - size * 0.5f), ay = Mathf.Abs(y - size * 0.5f);
            float r = size * 0.5f - inset;
            return Mathf.Max(Mathf.Max(ax, ay), (ax + ay) * 0.7071f) - r;
        }

        // 가장자리에서 width 안쪽까지 테두리 색
        internal static Color Stroke(Color fill, Color stroke, float d, float width)
        {
            float t = Mathf.Clamp01(d + width + 0.5f);
            return Color.Lerp(fill, stroke, t * stroke.a + (1f - stroke.a) * 0f);
        }

        // 거리 0 근처에 가는 선
        private static Color Line(Color fill, Color line, float d, float halfWidth)
        {
            float t = Mathf.Clamp01(halfWidth + 0.5f - Mathf.Abs(d)) * line.a;
            Color c = Color.Lerp(fill, line, t);
            c.a = Mathf.Max(fill.a, t);
            return c;
        }

        internal static Color Cover(Color c, float d)
        {
            c.a *= Mathf.Clamp01(0.5f - d);
            return c;
        }

        internal static Sprite Make(string name, int w, int h, int border, Func<float, float, float, float, Color> pixel)
        {
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var pixels = new Color[w * h];

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    pixels[y * w + x] = pixel(x + 0.5f, y + 0.5f, w, h);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            var sprite = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, new Vector4(border, border, border, border));
            sprite.name = name;
            return sprite;
        }

        // 4x4 표본으로 경계를 부드럽게 (도형 판정 함수용)
        internal static Sprite MakeSuper(string name, int size, Func<float, float, Color> sample)
        {
            return Make(name, size, size, 0, (x, y, w, h) =>
            {
                Color sum = Color.clear;

                for (int sy = 0; sy < 4; sy++)
                {
                    for (int sx = 0; sx < 4; sx++)
                    {
                        Color c = sample((x - 0.5f + (sx + 0.5f) / 4f) / w, (y - 0.5f + (sy + 0.5f) / 4f) / h);
                        sum += new Color(c.r * c.a, c.g * c.a, c.b * c.a, c.a);
                    }
                }

                sum /= 16f;
                return sum.a > 0f ? new Color(sum.r / sum.a, sum.g / sum.a, sum.b / sum.a, sum.a) : Color.clear;
            });
        }
    }
}
