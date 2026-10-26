using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Item;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 아이템 툴팁 (10/21): 이름(희귀도 색)·희귀도·부위, 능력치(착용 장비와 비교 ▲▼), 설명, 조작 안내.
    /// 마우스 오른쪽 아래에 붙고 화면 밖으로 나가지 않게 옮깁니다. 창들보다 위에 그려지도록 HUD 루트 맨 위에 둡니다.
    /// </summary>
    public sealed class ItemTooltipView
    {
        private const float WIDTH = 300f;
        private const float PAD = 16f;
        private const float ROW = 24f;
        private const int MAX_ROWS = 8;

        private readonly RectTransform _root;
        private readonly RectTransform _panel;
        private readonly Image _band;
        private readonly Image _border;
        private readonly Text _name;
        private readonly Text _sub;
        private readonly Image _line;
        private readonly Text _description;
        private readonly Text _footer;
        private readonly List<Text[]> _rows = new List<Text[]>();
        private readonly HashSet<StatType> _stats = new HashSet<StatType>();

        public ItemTooltipView(RectTransform root)
        {
            _root = root;
            UiPalette p = UiTheme.P;
            _panel = UiKit.Plate("ItemTooltip", root).rectTransform;
            _panel.pivot = new Vector2(0f, 1f);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);

            _band = UiKit.Box("Band", _panel, UiTheme.WithAlpha(p.Accent, 0.18f));
            UiKit.PlaceTopLeft(_band.rectTransform, new Vector2(3f, 3f), new Vector2(WIDTH - 6f, 54f));
            _border = UiKit.Sliced("Border", _panel, ItemVisuals.Frame);
            HudGraphics.Stretch(_border.rectTransform);

            _name = UiKit.Title("Name", _panel, 19, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_name.rectTransform, new Vector2(PAD, 6f), new Vector2(WIDTH - PAD * 2f, 28f));
            _sub = UiKit.Label("Sub", _panel, 13, p.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_sub.rectTransform, new Vector2(PAD, 38f), new Vector2(WIDTH - PAD * 2f, 18f));   // 10/26: 이름과 간격 +4 (띠 안)
            _line = UiKit.Box("Line", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.5f));

            for (int i = 0; i < MAX_ROWS; i++)
            {
                Text name = UiKit.Label("StatName", _panel, 14, p.TextDim, TextAnchor.MiddleLeft);
                Text value = UiKit.Label("StatValue", _panel, 14, p.Text, TextAnchor.MiddleRight, FontStyle.Bold);
                Text diff = UiKit.Label("StatDiff", _panel, 12, p.Text, TextAnchor.MiddleRight, FontStyle.Bold);
                _rows.Add(new[] { name, value, diff });
            }

            _description = UiKit.Label("Description", _panel, 13, new Color(0.79f, 0.66f, 1f), TextAnchor.UpperLeft);
            _description.horizontalOverflow = HorizontalWrapMode.Wrap;
            _footer = UiKit.Label("Footer", _panel, 11, UiTheme.WithAlpha(p.TextDim, 0.8f), TextAnchor.MiddleLeft);
            Hide();
        }

        public void Hide()
        {
            _panel.gameObject.SetActive(false);
        }

        /// <summary>compare = 같은 부위에 착용 중인 장비 (없거나 자기 자신이면 비교 안 함)</summary>
        public void Show(ItemDefinition item, ItemDefinition compare, string footer)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            UiPalette p = UiTheme.P;
            Color rarity = ItemVisuals.RarityColor(item.Rarity);
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            _name.text = item.DisplayName;
            _name.color = item.Rarity >= ItemRarity.Rare ? Color.Lerp(rarity, Color.white, 0.25f) : p.Text;
            _sub.text = $"{ItemVisuals.RarityName(item.Rarity)}  ·  {ItemVisuals.KindName(item)}";
            _sub.color = rarity;
            _band.color = UiTheme.WithAlpha(rarity, 0.2f);
            _border.color = UiTheme.WithAlpha(rarity, 0.9f);

            float y = 64f;
            UiKit.PlaceTopLeft(_line.rectTransform, new Vector2(PAD, y), new Vector2(WIDTH - PAD * 2f, 1f));
            y += 10f;

            bool compareOn = compare != null && compare != item;
            _stats.Clear();

            foreach (StatModifier modifier in item.Stats)
            {
                _stats.Add(modifier.Stat);
            }

            if (compareOn)
            {
                foreach (StatModifier modifier in compare.Stats)
                {
                    _stats.Add(modifier.Stat);
                }
            }

            int row = 0;

            foreach (StatType stat in _stats)
            {
                if (row >= MAX_ROWS)
                {
                    break;
                }

                float value = item.GetStat(stat);
                Text[] texts = _rows[row++];
                SetRow(texts, true, y);
                texts[0].text = StatText.Name(stat);
                texts[1].text = Mathf.Abs(value) > 0.0001f ? StatText.Signed(stat, value) : "-";

                float diff = compareOn ? value - compare.GetStat(stat) : 0f;
                bool show = compareOn && Mathf.Abs(diff) > 0.0001f;
                texts[2].text = show ? (diff > 0f ? "▲ " : "▼ ") + StatText.Value(stat, Mathf.Abs(diff)) : string.Empty;
                texts[2].color = diff > 0f ? new Color(0.37f, 0.7f, 0.42f) : new Color(0.85f, 0.3f, 0.25f);
                y += ROW;
            }

            for (int i = row; i < MAX_ROWS; i++)
            {
                SetRow(_rows[i], false, 0f);
            }

            if (!string.IsNullOrEmpty(item.Description))
            {
                y += 6f;
                _description.gameObject.SetActive(true);
                _description.text = item.Description;
                UiKit.PlaceTopLeft(_description.rectTransform, new Vector2(PAD, y), new Vector2(WIDTH - PAD * 2f, 40f));
                y += 22f;
            }
            else
            {
                _description.gameObject.SetActive(false);
            }

            string footerText = compareOn ? "착용 장비와 비교 (▲ 상승 / ▼ 하락)" : string.Empty;

            if (!string.IsNullOrEmpty(footer))
            {
                footerText = string.IsNullOrEmpty(footerText) ? footer : footerText + "   ·   " + footer;
            }

            y += 8f;
            UiKit.PlaceTopLeft(_footer.rectTransform, new Vector2(PAD, y), new Vector2(WIDTH - PAD * 2f, 18f));
            _footer.text = footerText;
            y += 26f;

            _panel.sizeDelta = new Vector2(WIDTH, y);
            Follow();
        }

        /// <summary>마우스 옆으로 옮기고 화면 안에 머물게</summary>
        public void Follow()
        {
            if (!_panel.gameObject.activeSelf ||
                !RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, UiKit.Pointer, null, out Vector2 local))
            {
                return;
            }

            Rect area = _root.rect;
            Vector2 size = _panel.sizeDelta;
            Vector2 position = local + new Vector2(22f, -12f);

            if (position.x + size.x > area.xMax)
            {
                position.x = local.x - 22f - size.x;
            }

            position.y = Mathf.Clamp(position.y, area.yMin + size.y, area.yMax);
            _panel.anchoredPosition = position;
        }

        private void SetRow(Text[] texts, bool on, float y)
        {
            foreach (Text text in texts)
            {
                text.gameObject.SetActive(on);
            }

            if (!on)
            {
                return;
            }

            UiKit.PlaceTopLeft(texts[0].rectTransform, new Vector2(PAD, y), new Vector2(150f, ROW));
            UiKit.PlaceTopLeft(texts[1].rectTransform, new Vector2(WIDTH - PAD - 160f, y), new Vector2(80f, ROW));
            UiKit.PlaceTopLeft(texts[2].rectTransform, new Vector2(WIDTH - PAD - 76f, y), new Vector2(76f, ROW));
        }
    }
}
