using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 바 툴팁 (10/26): 등록된 기술 칸에 마우스를 올리면 칸 위에 표시.
    /// 이름 · 키 · 슬롯 레벨 / 수치 요약(재사용 대기·기력·피해·범위·지속, SkillWindowText) / 남은 재사용 대기.
    /// 아이템 툴팁(ItemTooltipView)과 같은 판·띠 모양. HudView가 생성합니다.
    /// </summary>
    public sealed class SkillTooltipView
    {
        private const float WIDTH = 320f;
        private const float PAD = 16f;
        private const float GAP = 14f;

        private readonly RectTransform _root;
        private readonly RectTransform _panel;
        private readonly Text _name;
        private readonly Text _sub;
        private readonly Image _line;
        private readonly Text _summary;
        private readonly Text _growth;
        private readonly Text _cooldown;
        private SkillDefinition _shown;
        private int _shownLevel = -1;

        public bool IsVisible => _panel.gameObject.activeSelf;

        public SkillTooltipView(RectTransform root)
        {
            _root = root;
            UiPalette p = UiTheme.P;
            _panel = UiKit.Plate("SkillTooltip", root).rectTransform;
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0.5f);
            _panel.pivot = new Vector2(0.5f, 0f);

            Image band = UiKit.Box("Band", _panel, UiTheme.WithAlpha(p.Accent, 0.18f));
            UiKit.PlaceTopLeft(band.rectTransform, new Vector2(3f, 3f), new Vector2(WIDTH - 6f, 58f));
            Image border = UiKit.Sliced("Border", _panel, ItemVisuals.Frame);
            HudGraphics.Stretch(border.rectTransform);
            border.color = UiTheme.WithAlpha(p.Accent, 0.8f);

            _name = UiKit.Title("Name", _panel, 19, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_name.rectTransform, new Vector2(PAD, 6f), new Vector2(WIDTH - PAD * 2f, 28f));
            // 보조 글: 이름과 10px 띄움
            _sub = UiKit.Label("Sub", _panel, 13, p.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_sub.rectTransform, new Vector2(PAD, 40f), new Vector2(WIDTH - PAD * 2f, 18f));
            _line = UiKit.Box("Line", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            UiKit.PlaceTopLeft(_line.rectTransform, new Vector2(PAD, 70f), new Vector2(WIDTH - PAD * 2f, 1f));

            _summary = UiKit.Label("Summary", _panel, 14, p.Text, TextAnchor.UpperLeft);
            _summary.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.PlaceTopLeft(_summary.rectTransform, new Vector2(PAD, 80f), new Vector2(WIDTH - PAD * 2f, 40f));
            _growth = UiKit.Label("Growth", _panel, 12, p.TextDim, TextAnchor.UpperLeft);
            _growth.horizontalOverflow = HorizontalWrapMode.Wrap;
            _cooldown = UiKit.Label("Cooldown", _panel, 13, new Color(0.95f, 0.6f, 0.45f), TextAnchor.MiddleLeft, FontStyle.Bold);
            Hide();
        }

        public void Hide()
        {
            _panel.gameObject.SetActive(false);
            _shown = null;
        }

        /// <summary>slotRect 위에 표시 (key: 칸에 적힌 키, level: 슬롯 레벨, cooldown: 남은 재사용 대기 초)</summary>
        public void Show(SkillDefinition skill, string key, int level, bool grows, float cooldown, RectTransform slotRect)
        {
            if (skill == null)
            {
                Hide();
                return;
            }

            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();

            if (_shown != skill || _shownLevel != level)
            {
                _shown = skill;
                _shownLevel = level;
                _name.text = skill.DisplayName;
                _sub.text = grows ? $"{key} 키  ·  Lv {level} / {SkillGrowth.MAX_LEVEL}" : $"{key} 키";
                _summary.text = SkillWindowText.Describe(skill);
                _growth.text = grows ? $"슬롯 레벨 1당 피해(효과량) +5%  ·  현재 +{(level - 1) * 5}%\n기술 창(K)에서 우클릭으로 강화" : string.Empty;
            }

            float y = 80f + Mathf.Max(20f, _summary.preferredHeight) + 10f;

            if (_growth.text.Length > 0)
            {
                UiKit.PlaceTopLeft(_growth.rectTransform, new Vector2(PAD, y), new Vector2(WIDTH - PAD * 2f, 36f));
                y += Mathf.Max(18f, _growth.preferredHeight) + 8f;
            }

            _cooldown.gameObject.SetActive(cooldown > 0.05f);

            if (cooldown > 0.05f)
            {
                UiKit.PlaceTopLeft(_cooldown.rectTransform, new Vector2(PAD, y), new Vector2(WIDTH - PAD * 2f, 20f));
                _cooldown.text = $"재사용 대기 중  {cooldown:0.0}초";
                y += 26f;
            }

            _panel.sizeDelta = new Vector2(WIDTH, y + 8f);
            PlaceAbove(slotRect);
        }

        // 칸 위 가운데, 화면 밖으로 나가지 않게
        private void PlaceAbove(RectTransform slotRect)
        {
            Vector3[] corners = new Vector3[4];
            slotRect.GetWorldCorners(corners);
            Vector2 topCenter = (corners[1] + corners[2]) * 0.5f;

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, topCenter, null, out Vector2 local))
            {
                return;
            }

            Rect area = _root.rect;
            float half = WIDTH * 0.5f;
            local.x = Mathf.Clamp(local.x, area.xMin + half + 8f, area.xMax - half - 8f);
            local.y += GAP;
            _panel.anchoredPosition = local;
        }
    }
}
