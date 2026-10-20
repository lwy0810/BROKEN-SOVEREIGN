using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 코드로 만드는 창(UI)용 공용 함수: 글자·패널·막대 생성, 배치, 마우스 판정 (10/21 보스·스킬·캐릭터 창 공용).
    /// 다른 화면과 같이 EventSystem 없이 마우스 위치를 직접 판정합니다.
    /// </summary>
    public static class UiKit
    {
        public static readonly Color GOLD = new Color(0.95f, 0.8f, 0.45f);
        public static readonly Color SUBTLE = new Color(0.85f, 0.82f, 0.75f);
        public static readonly Color DIM_TEXT = new Color(0.6f, 0.58f, 0.55f);
        public static readonly Color PANEL = new Color(0.07f, 0.065f, 0.09f, 0.94f);
        public static readonly Color ROW = new Color(1f, 1f, 1f, 0.04f);
        public static readonly Color ROW_HOVER = new Color(0.95f, 0.8f, 0.45f, 0.16f);

        public static Text Label(string name, Transform parent, int size, Color color, TextAnchor anchor,
            FontStyle style = FontStyle.Normal)
        {
            Text text = HudGraphics.CreateRect(name, parent).gameObject.AddComponent<Text>();
            text.font = HudGraphics.Font;
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.fontStyle = style;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        public static Image Box(string name, Transform parent, Color color)
        {
            Image image = HudGraphics.CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = HudGraphics.WhiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>금테 패널</summary>
        public static Image Panel(string name, Transform parent)
        {
            Image panel = Box(name, parent, PANEL);
            Outline outline = panel.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.85f);
            outline.effectDistance = new Vector2(2f, -2f);
            return panel;
        }

        /// <summary>가로로 채워지는 막대 (배경 위)</summary>
        public static Image Fill(string name, Transform parent, Color color)
        {
            Image fill = Box(name, parent, color);
            HudGraphics.Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            return fill;
        }

        /// <summary>부모 기준 anchor 지점에 pivot을 맞춰 배치</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>부모 왼쪽 위 기준 배치 (목록 행 배치용)</summary>
        public static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(position.x, -position.y), size);
        }

        public static Vector2 Pointer
        {
            get
            {
                Mouse mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            }
        }

        public static bool Clicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public static bool IsHover(RectTransform rect)
        {
            return rect != null && rect.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(rect, Pointer, null);
        }
    }
}
