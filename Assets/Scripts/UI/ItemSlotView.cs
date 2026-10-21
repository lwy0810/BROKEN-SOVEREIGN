using BrokenSovereign.Item;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 아이템 칸 하나 (가방·장비 칸 공용, 10/21): 움푹한 바탕 + 희귀도 안쪽 빛 + 아이콘 + 희귀도 테두리 + 개수.
    /// 빈 칸은 placeholder 글자(장비 부위 이름)를 흐리게 표시합니다.
    /// </summary>
    public sealed class ItemSlotView
    {
        private readonly Image _glow;
        private readonly Image _icon;
        private readonly Image _frame;
        private readonly Text _count;
        private readonly Text _placeholder;
        private bool _highlight;
        private int _countValue;
        private bool _dim;

        public RectTransform Rect { get; }
        public ItemDefinition Item { get; private set; }

        public ItemSlotView(string name, Transform parent, string placeholder = null)
        {
            Image background = UiKit.Sliced(name, parent, UiSprites.Tile);
            Rect = background.rectTransform;

            _glow = UiKit.Sliced("Glow", Rect, ItemVisuals.Glow);
            HudGraphics.Stretch(_glow.rectTransform);
            _glow.rectTransform.offsetMin = new Vector2(3f, 3f);
            _glow.rectTransform.offsetMax = new Vector2(-3f, -3f);

            _placeholder = UiKit.Label("Placeholder", Rect, 11, UiTheme.WithAlpha(UiTheme.P.TextDim, 0.45f), TextAnchor.MiddleCenter);
            HudGraphics.Stretch(_placeholder.rectTransform);
            _placeholder.text = placeholder ?? string.Empty;

            _icon = HudGraphics.CreateRect("Icon", Rect).gameObject.AddComponent<Image>();
            _icon.raycastTarget = false;
            _icon.preserveAspect = true;
            HudGraphics.Stretch(_icon.rectTransform);
            _icon.rectTransform.offsetMin = new Vector2(9f, 9f);
            _icon.rectTransform.offsetMax = new Vector2(-9f, -9f);
            Outline outline = _icon.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0.05f, 0.05f, 0.07f, 0.9f);
            outline.effectDistance = new Vector2(1.2f, -1.2f);

            _frame = UiKit.Sliced("Frame", Rect, ItemVisuals.Frame);
            HudGraphics.Stretch(_frame.rectTransform);

            _count = UiKit.Label("Count", Rect, 12, UiTheme.P.Text, TextAnchor.LowerRight, FontStyle.Bold);
            HudGraphics.Stretch(_count.rectTransform);
            _count.rectTransform.offsetMin = new Vector2(2f, 2f);
            _count.rectTransform.offsetMax = new Vector2(-5f, -2f);

            Set(null, 0);
        }

        /// <summary>표시 갱신. dim = 탭 필터에서 제외된 아이템</summary>
        public void Set(ItemDefinition item, int count, bool dim = false)
        {
            Item = item;
            _countValue = count;
            _dim = dim;
            bool has = item != null;
            _icon.enabled = has;
            _glow.enabled = has;
            _placeholder.enabled = !has;
            _count.text = has && count > 1 ? count.ToString() : string.Empty;

            if (has)
            {
                Color rarity = ItemVisuals.RarityColor(item.Rarity);
                _icon.sprite = ItemVisuals.Icon(item.Icon);
                _icon.color = UiTheme.WithAlpha(item.IconColor, dim ? 0.25f : 1f);
                _glow.color = UiTheme.WithAlpha(rarity, dim ? 0.08f : 0.32f);
                _frame.color = _highlight ? new Color(1f, 0.89f, 0.66f) : UiTheme.WithAlpha(rarity, dim ? 0.3f : 1f);
                _count.color = UiTheme.WithAlpha(UiTheme.P.Text, dim ? 0.3f : 1f);
            }
            else
            {
                _frame.color = _highlight ? new Color(1f, 0.89f, 0.66f) : UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.35f);
            }
        }

        /// <summary>마우스 올림·끌어 놓기 대상 강조</summary>
        public void SetHighlight(bool on)
        {
            if (_highlight == on)
            {
                return;
            }

            _highlight = on;
            Set(Item, _countValue, _dim);
        }
    }
}
