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
