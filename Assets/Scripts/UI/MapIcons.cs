using BrokenSovereign.Map;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 미니맵·전체 지도 공용 아이콘. MapIconSet(그림 에셋)이 연결되어 있으면 그 스프라이트를,
    /// 없으면 색 원 + 한 글자 임시 표기를 씁니다. 적은 항상 빨간 점.
    /// </summary>
    public static class MapIcons
    {
        public static readonly Color PLAYER_COLOR = new Color(1f, 0.92f, 0.55f);

        // 그림 에셋이 없을 때 상점 아이콘을 숨기는 기본 폭 (m)
        private const float DEFAULT_SHOP_VIEW_WIDTH = 420f;

        /// <summary>HudView가 연결 (없으면 임시 표기)</summary>
        public static MapIconSet IconSet { get; set; }

        /// <summary>지도에 보이는 가로 폭(m)에서 이 종류를 표시할지 (확대했을 때만 보이는 아이콘 처리)</summary>
        public static bool IsVisibleAt(MapMarkerKind kind, float viewWidth)
        {
            MapIconSet.Entry entry = IconSet != null ? IconSet.Find(kind) : null;
            float max = entry != null ? entry.MaxViewWidth : IsShop(kind) ? DEFAULT_SHOP_VIEW_WIDTH : 0f;
            return max <= 0f || viewWidth <= max;
        }

        public static bool IsShop(MapMarkerKind kind)
        {
            return kind == MapMarkerKind.Merchant || kind == MapMarkerKind.Smith || kind == MapMarkerKind.Healer;
        }

        public static Color ColorOf(MapMarkerKind kind)
        {
            switch (kind)
            {
                case MapMarkerKind.Merchant: return new Color(0.25f, 0.72f, 0.7f);
                case MapMarkerKind.Smith: return new Color(0.85f, 0.4f, 0.25f);
                case MapMarkerKind.Healer: return new Color(0.45f, 0.82f, 0.45f);
                case MapMarkerKind.Waypoint: return new Color(0.45f, 0.75f, 1f);
                case MapMarkerKind.Event: return new Color(1f, 0.85f, 0.3f);
                case MapMarkerKind.Trial: return new Color(1f, 0.6f, 0.2f);
                case MapMarkerKind.Boss: return new Color(0.9f, 0.2f, 0.25f);
                default: return new Color(0.95f, 0.2f, 0.2f);
            }
        }

        public static string LabelOf(MapMarkerKind kind)
        {
            switch (kind)
            {
                case MapMarkerKind.Merchant: return "잡";
                case MapMarkerKind.Smith: return "대";
                case MapMarkerKind.Healer: return "치";
                case MapMarkerKind.Waypoint: return "W";
                case MapMarkerKind.Event: return "!";
                case MapMarkerKind.Trial: return "시";
                case MapMarkerKind.Boss: return "보";
                default: return string.Empty;
            }
        }

        public static RectTransform Create(Transform parent, MapMarkerKind kind, float size)
        {
            if (kind == MapMarkerKind.Enemy)
            {
                return Dot(parent, kind, size * 0.35f);
            }

            MapIconSet.Entry entry = IconSet != null ? IconSet.Find(kind) : null;

            if (entry != null && entry.Sprite != null)
            {
                RectTransform rect = HudGraphics.CreateRect($"Icon_{kind}", parent);
                rect.sizeDelta = new Vector2(size, size);
                Image image = rect.gameObject.AddComponent<Image>();
                image.sprite = entry.Sprite;
                image.preserveAspect = true;
                image.raycastTarget = false;
                return rect;
            }

            return Fallback(parent, kind, size);
        }

        private static RectTransform Dot(Transform parent, MapMarkerKind kind, float diameter)
        {
            RectTransform rect = HudGraphics.CreateRect($"Icon_{kind}", parent);
            rect.sizeDelta = new Vector2(diameter, diameter);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudGraphics.CircleSprite;
            image.color = ColorOf(kind);
            image.raycastTarget = false;
            return rect;
        }

        // 그림 에셋이 없을 때: 테두리 원 + 색 원 + 한 글자
        private static RectTransform Fallback(Transform parent, MapMarkerKind kind, float size)
        {
            RectTransform rect = HudGraphics.CreateRect($"Icon_{kind}", parent);
            rect.sizeDelta = new Vector2(size, size);
            Image border = rect.gameObject.AddComponent<Image>();
            border.sprite = HudGraphics.CircleSprite;
            border.color = new Color(0.08f, 0.07f, 0.06f, 0.9f);
            border.raycastTarget = false;

            RectTransform fillRect = HudGraphics.CreateRect("Fill", rect);
            fillRect.anchorMin = Vector2.zero;
            fillRect.anchorMax = Vector2.one;
            fillRect.offsetMin = Vector2.one * 2f;
            fillRect.offsetMax = -Vector2.one * 2f;
            Image fill = fillRect.gameObject.AddComponent<Image>();
            fill.sprite = HudGraphics.CircleSprite;
            fill.color = ColorOf(kind);
            fill.raycastTarget = false;

            Text label = HudGraphics.CreateRect("Label", rect).gameObject.AddComponent<Text>();
            HudGraphics.Stretch(label.rectTransform);
            label.font = HudGraphics.Font;
            label.fontSize = Mathf.RoundToInt(size * 0.6f);
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = new Color(0.08f, 0.07f, 0.06f);
            label.text = LabelOf(kind);
            label.raycastTarget = false;
            return rect;
        }

        /// <summary>플레이어 화살표</summary>
        public static RectTransform CreatePlayer(Transform parent, float size)
        {
            RectTransform rect = HudGraphics.CreateRect("Player", parent);
            rect.sizeDelta = new Vector2(size, size);
            Image image = rect.gameObject.AddComponent<Image>();
            image.sprite = HudGraphics.ArrowSprite;
            image.color = PLAYER_COLOR;
            image.raycastTarget = false;
            Outline outline = rect.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.8f);
            return rect;
        }
    }
}
