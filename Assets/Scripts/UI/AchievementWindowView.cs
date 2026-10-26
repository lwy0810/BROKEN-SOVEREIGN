using System;
using System.Collections.Generic;
using BrokenSovereign.Achievement;
using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 업적 창 (10/26, Figma 18-A, 1400×880). HUD 메뉴 [업적] 또는 J, 격 창 [업적 전체 보기].
    /// - 위: 달성 수 · 업적 포인트(획득 · 사용 · 남음) · 칭호 · [신격 트리 열기]
    /// - 왼쪽: 분류 (전체 · 시련 · 전투 · 탐험 · 성장 · 특수·칭호) + 보상 안내
    /// - 오른쪽: 필터(전체 · 진행 중 · 달성) · 고정 카드(다음 격 돌파: 실제 레벨 조건) · 업적 목록 (달성 임박순, 휠 이동)
    /// 다른 창과 같이 EventSystem 없이 마우스 위치로 판정 (UiKit). 열려 있는 동안 전투 입력 차단.
    /// </summary>
    public class AchievementWindowView : MonoBehaviour
    {
        private const float WIDTH = 1400f;
        private const float HEIGHT = 880f;
        private const float LIST_X = 292f;
        private const float LIST_W = 1075f;
        private const float ROW_Y = 347f;
        private const float ROW_H = 67f;
        private const float ROW_PITCH = 78f;
        private const int VISIBLE_ROWS = 6;

        private static readonly string[] CATEGORY_NAMES = { "전체", "시련", "전투", "탐험", "성장", "특수 · 칭호" };
        private static readonly string[] FILTER_NAMES = { "전체", "진행 중", "달성" };
        private static Color DoneColor => new Color(0.5f, 0.86f, 0.52f);

        private sealed class RowView
        {
            public RectTransform Rect;
            public Image Background;
            public Image DiamondOuter;
            public Image DiamondInner;
            public Text Badge;
            public Text Name;
            public Text Description;
            public UiBar Bar;
            public Text Progress;
            public Text Reward;
        }

        private sealed class Segment
        {
            public Text Label;
            public Text Value;
            public UiBar Bar;
        }

        private AchievementTracker _tracker;
        private GyeokTree _tree;
        private PlayerProgression _progression;
        private Action _openTree;

        private RectTransform _root;
        private RectTransform _panel;
        private Image _close;
        private Text _doneBig;
        private Text _doneSmall;
        private UiBar _doneBar;
        private Text _donePercent;
        private Text _pointsBig;
        private Text _pointsSmall;
        private Text _titleText;
        private Image _treeButton;
        private readonly List<(Image box, Outline outline, Text name, Text count)> _categories = new List<(Image, Outline, Text, Text)>();
        private readonly List<(Image box, Text text)> _filters = new List<(Image, Text)>();
        private Text _pinTitle;
        private readonly List<Segment> _segments = new List<Segment>();
        private readonly List<RowView> _rows = new List<RowView>();
        private Text _emptyText;
        private Text _scrollHint;

        private int _category;
        private int _filter;
        private int _scroll;
        private readonly List<AchievementTracker.Entry> _visible = new List<AchievementTracker.Entry>();

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public static AchievementWindowView Create(RectTransform parent, GameObject player, Action openTree)
        {
            RectTransform root = HudGraphics.CreateRect("AchievementWindow", parent);
            HudGraphics.Stretch(root);
            AchievementWindowView view = root.gameObject.AddComponent<AchievementWindowView>();
            view._tracker = player.GetComponent<AchievementTracker>();
            view._tree = player.GetComponent<GyeokTree>();
            view._progression = player.GetComponent<PlayerProgression>();
            view._openTree = openTree;
            view.Build(root);
            root.gameObject.SetActive(false);
            return view;
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        public void Open()
        {
            if (IsOpen || GameplayInput.IsBlocked || _tracker == null)
            {
                return;
            }

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            GameplayInput.IsBlocked = true;
            _scroll = 0;
            Refresh();
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _root.gameObject.SetActive(false);
            GameplayInput.IsBlocked = false;
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }
        }

        // ------------------------------------------------------------------
        // 구성
        // ------------------------------------------------------------------

        private void Build(RectTransform root)
        {
            _root = root;
            UiPalette p = UiTheme.P;

            Image dim = UiKit.Box("Dim", root, new Color(0f, 0f, 0f, 0.55f));
            HudGraphics.Stretch(dim.rectTransform);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(WIDTH, HEIGHT));
            Text title = UiKit.TitleTab(_panel, "업  적", 136f);
            title.transform.parent.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.9f);

            _close = UiKit.CloseButton(_panel);
            _close.rectTransform.sizeDelta = new Vector2(40f, 40f);
            _close.rectTransform.anchoredPosition = new Vector2(-18f, -18f);

            BuildSummary();
            BuildCategories();
            BuildList();
        }

        private RectTransform Inset(string name, RectTransform parent, Vector2 position, Vector2 size, Color? color = null)
        {
            Image inset = UiKit.Round(name, parent, color ?? UiTheme.WithAlpha(UiTheme.P.Inset, 0.92f));
            UiKit.PlaceTopLeft(inset.rectTransform, position, size);
            inset.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.7f);
            return inset.rectTransform;
        }

        private static Text Label(string name, RectTransform parent, Vector2 position, Vector2 size, int fontSize, Color color,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            Text text = UiKit.Label(name, parent, fontSize, color, anchor, bold ? FontStyle.Bold : FontStyle.Normal);
            UiKit.PlaceTopLeft(text.rectTransform, position, size);
            return text;
        }

        private static UiBar Bar(RectTransform parent, Vector2 position, Vector2 size)
        {
            UiBar bar = UiKit.Bar("Bar", parent, UiTheme.P.Accent, UiTheme.P.Accent * 0.6f);
            UiKit.PlaceTopLeft(bar.Rect, position, size);
            return bar;
        }

        private void BuildSummary()
        {
            UiPalette p = UiTheme.P;
            RectTransform summary = Inset("Summary", _panel, new Vector2(33f, 48f), new Vector2(1334f, 82f));

            Label("DoneLabel", summary, new Vector2(18f, 10f), new Vector2(100f, 18f), 13, p.TextDim).text = "달성";
            _doneBig = Label("DoneBig", summary, new Vector2(18f, 32f), new Vector2(50f, 38f), 30, p.Title, bold: true);
            _doneSmall = Label("DoneSmall", summary, new Vector2(60f, 38f), new Vector2(60f, 28f), 18, p.Text, bold: true);
            _doneBar = Bar(summary, new Vector2(137f, 48f), new Vector2(300f, 9f));
            _donePercent = Label("Percent", summary, new Vector2(447f, 40f), new Vector2(60f, 24f), 15, p.TextDim);

            Separator(summary, 580f);
            Label("PointsLabel", summary, new Vector2(598f, 10f), new Vector2(160f, 18f), 13, p.TextDim).text = "업적 포인트";
            _pointsBig = Label("PointsBig", summary, new Vector2(598f, 32f), new Vector2(50f, 38f), 30, p.Title, bold: true);
            _pointsSmall = Label("PointsSmall", summary, new Vector2(646f, 38f), new Vector2(240f, 28f), 16, p.Text, bold: true);

            Separator(summary, 890f);
            Label("TitleLabel", summary, new Vector2(908f, 10f), new Vector2(160f, 18f), 13, p.TextDim).text = "칭호";
            _titleText = Label("Title", summary, new Vector2(908f, 34f), new Vector2(260f, 32f), 20, p.Text, bold: true);

            _treeButton = UiKit.Round("OpenTree", summary, p.Accent);
            UiKit.PlaceTopLeft(_treeButton.rectTransform, new Vector2(1186f, 21f), new Vector2(128f, 40f));
            Text label = UiKit.Label("Label", _treeButton.transform, 15, new Color(0.12f, 0.09f, 0.04f), TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(label.rectTransform);
            label.GetComponent<Shadow>().enabled = false;
            label.text = "신격 트리 열기";
        }

        private static void Separator(RectTransform parent, float x)
        {
            Image line = UiKit.Box("Sep", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.6f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, 16f), new Vector2(1f, 50f));
        }

        private void BuildCategories()
        {
            UiPalette p = UiTheme.P;
            RectTransform side = Inset("Categories", _panel, new Vector2(33f, 151f), new Vector2(239f, 695f));

            for (int i = 0; i < CATEGORY_NAMES.Length; i++)
            {
                Image box = UiKit.Round("Category", side, Color.clear);
                UiKit.PlaceTopLeft(box.rectTransform, new Vector2(10f, 12f + i * 52f), new Vector2(219f, 42f));
                Outline outline = box.gameObject.AddComponent<Outline>();
                Text name = Label("Name", box.rectTransform, new Vector2(16f, 0f), new Vector2(120f, 42f), 17, p.Text, bold: true);
                name.text = CATEGORY_NAMES[i];
                Text count = Label("Count", box.rectTransform, new Vector2(120f, 0f), new Vector2(85f, 42f), 13, p.TextDim, TextAnchor.MiddleRight);
                _categories.Add((box, outline, name, count));
            }

            float y = 12f + CATEGORY_NAMES.Length * 52f + 22f;
            Label("GuideTitle", side, new Vector2(18f, y), new Vector2(200f, 22f), 15, p.Accent, bold: true).text = "보상 안내";
            y += 34f;
            y = Guide(side, y, "일반 업적", "포인트 +1 · 스탯 보너스");
            y = Guide(side, y, "시련", "포인트 +3 · 격 돌파 기록");
            Guide(side, y, "특수 업적", "포인트 +1 · 칭호");
        }

        private static float Guide(RectTransform parent, float y, string title, string body)
        {
            Label("GuideName", parent, new Vector2(18f, y), new Vector2(200f, 18f), 13, UiTheme.P.Text).text = title;
            // 보조 글: 이름과 10px 띄움
            Label("GuideBody", parent, new Vector2(18f, y + 28f), new Vector2(200f, 18f), 13, UiTheme.P.TextDim).text = body;
            return y + 62f;
        }

        private void BuildList()
        {
            UiPalette p = UiTheme.P;
            float x = LIST_X;

            for (int i = 0; i < FILTER_NAMES.Length; i++)
            {
                float width = i == 1 ? 76f : 60f;
                Image box = UiKit.Round("Filter", _panel, p.Inset);
                UiKit.PlaceTopLeft(box.rectTransform, new Vector2(x, 151f), new Vector2(width, 30f));
                box.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.PlateStroke, 0.9f);
                Text text = UiKit.Label("Text", box.transform, 14, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                HudGraphics.Stretch(text.rectTransform);
                text.text = FILTER_NAMES[i];
                _filters.Add((box, text));
                x += width + 20f;
            }

            Label("Sort", _panel, new Vector2(LIST_X + LIST_W - 220f, 156f), new Vector2(200f, 20f), 13, p.TextDim, TextAnchor.MiddleRight).text = "정렬: 달성 임박순";

            // 고정 카드: 다음 격 돌파
            RectTransform pin = Inset("Pinned", _panel, new Vector2(LIST_X, 199f), new Vector2(LIST_W, 131f),
                Color.Lerp(p.Inset, p.Accent, 0.12f));
            pin.GetComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.9f);
            Image chip = UiKit.Round("Chip", pin, p.Accent);
            UiKit.PlaceTopLeft(chip.rectTransform, new Vector2(16f, 12f), new Vector2(118f, 20f));
            Text chipText = UiKit.Label("Text", chip.transform, 12, new Color(0.12f, 0.09f, 0.04f), TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(chipText.rectTransform);
            chipText.GetComponent<Shadow>().enabled = false;
            chipText.text = "고정 · 다음 격 돌파";
            _pinTitle = UiKit.Title("PinTitle", pin, 22, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_pinTitle.rectTransform, new Vector2(16f, 40f), new Vector2(700f, 32f));

            float segmentWidth = (LIST_W - 32f - 2f * 24f) / 3f;

            for (int i = 0; i < 3; i++)
            {
                float sx = 16f + i * (segmentWidth + 24f);
                var segment = new Segment
                {
                    Label = Label("SegLabel", pin, new Vector2(sx, 80f), new Vector2(segmentWidth * 0.6f, 20f), 13, p.TextDim),
                    Value = Label("SegValue", pin, new Vector2(sx + segmentWidth * 0.4f, 80f), new Vector2(segmentWidth * 0.6f, 20f), 13, p.Text, TextAnchor.MiddleRight, true),
                    Bar = Bar(pin, new Vector2(sx, 106f), new Vector2(segmentWidth, 6f))
                };
                _segments.Add(segment);
            }

            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                _rows.Add(BuildRow(ROW_Y + i * ROW_PITCH));
            }

            _emptyText = Label("Empty", _panel, new Vector2(LIST_X, ROW_Y + 20f), new Vector2(LIST_W, 30f), 16, p.TextDim, TextAnchor.MiddleCenter);
            _emptyText.text = "해당하는 업적이 없습니다";
            _scrollHint = Label("ScrollHint", _panel, new Vector2(LIST_X, HEIGHT - 32f), new Vector2(LIST_W, 18f), 12, p.TextDim, TextAnchor.MiddleRight);
        }

        private RowView BuildRow(float y)
        {
            UiPalette p = UiTheme.P;
            RectTransform rect = Inset("Row", _panel, new Vector2(LIST_X, y), new Vector2(LIST_W, ROW_H));
            var row = new RowView { Rect = rect, Background = rect.GetComponent<Image>() };

            row.DiamondOuter = UiKit.Box("Diamond", rect, p.Accent);
            UiKit.Place(row.DiamondOuter.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(34f, 0f), new Vector2(22f, 22f));
            row.DiamondOuter.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            row.DiamondInner = UiKit.Box("Inner", row.DiamondOuter.rectTransform, p.Inset);
            UiKit.Place(row.DiamondInner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(18f, 18f));

            Image badge = UiKit.Round("Badge", rect, Color.clear);
            UiKit.PlaceTopLeft(badge.rectTransform, new Vector2(62f, 12f), new Vector2(40f, 18f));
            badge.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.TextDim, 0.8f);
            row.Badge = UiKit.Label("Text", badge.transform, 11, p.TextDim, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(row.Badge.rectTransform);

            row.Name = Label("Name", rect, new Vector2(118f, 8f), new Vector2(460f, 26f), 18, p.Text, bold: true);
            // 보조 글: 분류 칩과 10px 띄움
            row.Description = Label("Desc", rect, new Vector2(62f, 40f), new Vector2(520f, 20f), 13, p.TextDim);

            row.Bar = Bar(rect, new Vector2(600f, 30f), new Vector2(200f, 7f));
            row.Progress = Label("Progress", rect, new Vector2(812f, 22f), new Vector2(80f, 22f), 15, p.Text, TextAnchor.MiddleCenter, true);
            row.Reward = Label("Reward", rect, new Vector2(890f, 22f), new Vector2(LIST_W - 908f, 22f), 14, p.Title, TextAnchor.MiddleRight, true);
            return row;
        }

        // ------------------------------------------------------------------
        // 입력 · 갱신
        // ------------------------------------------------------------------

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || UiKit.IsCloseClicked(_close))
            {
                Close();
                return;
            }

            bool hoverTree = UiKit.IsHover(_treeButton.rectTransform);
            _treeButton.color = hoverTree ? Color.Lerp(UiTheme.P.Accent, Color.white, 0.25f) : UiTheme.P.Accent;

            if (hoverTree && UiKit.Clicked)
            {
                Close();
                _openTree?.Invoke();
                return;
            }

            for (int i = 0; i < _categories.Count; i++)
            {
                if (UiKit.Clicked && UiKit.IsHover(_categories[i].box.rectTransform))
                {
                    _category = i;
                    _scroll = 0;
                }
            }

            for (int i = 0; i < _filters.Count; i++)
            {
                if (UiKit.Clicked && UiKit.IsHover(_filters[i].box.rectTransform))
                {
                    _filter = i;
                    _scroll = 0;
                }
            }

            if (mouse != null)
            {
                float wheel = mouse.scroll.ReadValue().y;

                if (Mathf.Abs(wheel) > 0.01f)
                {
                    _scroll += wheel > 0f ? -1 : 1;
                }
            }

            Refresh();
        }

        private void Refresh()
        {
            UiPalette p = UiTheme.P;

            // 요약
            (int done, int total) = _tracker.Count(null);
            _doneBig.text = done.ToString();
            _doneSmall.text = $"/ {total}";
            PlaceAfter(_doneSmall, _doneBig, 18f);
            float ratio = total > 0 ? (float)done / total : 0f;
            _doneBar.SetRatio(ratio);
            _donePercent.text = $"{Mathf.RoundToInt(ratio * 100f)}%";

            int earned = _tracker.PointsEarned;
            int spent = _tree != null ? _tree.PointsSpent : 0;
            _pointsBig.text = earned.ToString();
            _pointsSmall.text = $"획득  ·  사용 {spent}  ·  남음 {Mathf.Max(0, earned - spent)}";
            PlaceAfter(_pointsSmall, _pointsBig, 598f);
            _titleText.text = string.IsNullOrEmpty(_tracker.ActiveTitle) ? "없음" : _tracker.ActiveTitle;
            _titleText.color = string.IsNullOrEmpty(_tracker.ActiveTitle) ? p.TextDim : p.Text;

            // 분류 · 필터
            for (int i = 0; i < _categories.Count; i++)
            {
                (int cd, int ct) = _tracker.Count(i == 0 ? (AchievementCategory?)null : (AchievementCategory)(i - 1));
                bool selected = i == _category;
                bool hover = UiKit.IsHover(_categories[i].box.rectTransform);
                _categories[i].box.color = selected ? UiTheme.WithAlpha(p.Accent, 0.22f) : hover ? UiTheme.WithAlpha(p.Text, 0.06f) : Color.clear;
                _categories[i].outline.effectColor = selected ? p.Accent : Color.clear;
                _categories[i].name.color = selected ? p.Title : p.Text;
                _categories[i].count.text = $"{cd} / {ct}";
            }

            for (int i = 0; i < _filters.Count; i++)
            {
                bool selected = i == _filter;
                _filters[i].box.color = selected ? p.Accent : p.Inset;
                _filters[i].text.color = selected ? new Color(0.12f, 0.09f, 0.04f) : p.Text;
            }

            RefreshPinned();
            RefreshRows();
        }

        // 큰 숫자 바로 뒤에 작은 글자 붙이기 (자릿수 대비)
        private static void PlaceAfter(Text small, Text big, float bigX)
        {
            Vector2 position = small.rectTransform.anchoredPosition;
            small.rectTransform.anchoredPosition = new Vector2(bigX + big.preferredWidth + 6f, position.y);
        }

        // 다음 격 돌파: 실제 규칙(레벨)으로 표시
        private void RefreshPinned()
        {
            if (_progression == null)
            {
                return;
            }

            AscensionTier tier = _progression.Tier;
            int level = _progression.Level;
            (int trialDone, int trialTotal) = _tracker.Count(AchievementCategory.Trial);
            float expRatio = _progression.IsMaxLevel ? 1f : _progression.Progress;

            switch (tier)
            {
                case AscensionTier.Mortal:
                    _pinTitle.text = $"{AscensionModel.DisplayName(tier)} → {AscensionModel.DisplayName(AscensionTier.Superhuman)}";
                    SetSegment(0, "레벨", level, _progression.SuperhumanLevel);
                    break;
                case AscensionTier.Superhuman:
                    _pinTitle.text = $"{AscensionModel.DisplayName(tier)} → {AscensionModel.DisplayName(AscensionTier.Demigod)}";
                    SetSegment(0, "레벨", level, _progression.DemigodLevel);
                    break;
                default:
                    _pinTitle.text = $"{AscensionModel.DisplayName(tier)} — 최고 격 도달 (프로토타입 범위)";
                    SetSegment(0, "레벨", level, level);
                    break;
            }

            _segments[1].Label.text = "다음 레벨 경험치";
            _segments[1].Value.text = _progression.IsMaxLevel ? "최대" : $"{Mathf.RoundToInt(expRatio * 100f)}%";
            _segments[1].Bar.SetRatio(expRatio);
            SetSegment(2, "시련 업적", trialDone, trialTotal);
        }

        private void SetSegment(int index, string label, int value, int goal)
        {
            Segment segment = _segments[index];
            segment.Label.text = label;
            segment.Value.text = $"{Mathf.Min(value, goal)} / {goal}";
            segment.Bar.SetRatio(goal > 0 ? (float)value / goal : 1f);
        }

        private void RefreshRows()
        {
            UiPalette p = UiTheme.P;
            _visible.Clear();

            foreach (AchievementTracker.Entry entry in _tracker.Entries)
            {
                if (_category > 0 && entry.Definition.Category != (AchievementCategory)(_category - 1))
                {
                    continue;
                }

                if ((_filter == 1 && entry.IsDone) || (_filter == 2 && !entry.IsDone))
                {
                    continue;
                }

                _visible.Add(entry);
            }

            // 달성 임박순: 진행 중(비율 높은 순) → 달성(최근 순)
            _visible.Sort((a, b) =>
            {
                if (a.IsDone != b.IsDone) return a.IsDone ? 1 : -1;
                if (a.IsDone) return b.DoneOrder.CompareTo(a.DoneOrder);
                return b.Ratio.CompareTo(a.Ratio);
            });

            int maxScroll = Mathf.Max(0, _visible.Count - VISIBLE_ROWS);
            _scroll = Mathf.Clamp(_scroll, 0, maxScroll);
            _emptyText.enabled = _visible.Count == 0;
            _scrollHint.text = maxScroll > 0 ? $"휠: 목록 이동  ({_scroll + 1}~{Mathf.Min(_visible.Count, _scroll + VISIBLE_ROWS)} / {_visible.Count})" : string.Empty;

            for (int i = 0; i < _rows.Count; i++)
            {
                int index = _scroll + i;
                RowView row = _rows[i];
                bool show = index < _visible.Count;
                row.Rect.gameObject.SetActive(show);

                if (!show)
                {
                    continue;
                }

                AchievementTracker.Entry entry = _visible[index];
                AchievementDefinition definition = entry.Definition;
                row.Badge.text = AchievementCatalog.CategoryName(definition.Category);
                row.Name.text = definition.Name;
                row.Description.text = definition.Description;
                row.DiamondInner.color = entry.IsDone ? p.Accent : p.Inset;
                row.DiamondOuter.color = entry.IsDone ? p.Title : p.Accent;

                if (entry.IsDone)
                {
                    row.Bar.SetColor(p.TextDim, p.TextDim * 0.6f);
                    row.Bar.SetRatio(1f);
                    row.Progress.text = "완료";
                    row.Progress.color = DoneColor;
                    row.Reward.color = p.TextDim;
                    row.Description.color = UiTheme.WithAlpha(p.TextDim, 0.8f);
                }
                else
                {
                    row.Bar.SetColor(p.Accent, p.Accent * 0.6f);
                    row.Bar.SetRatio(entry.Ratio);
                    row.Progress.text = entry.Goal > 0 ? $"{entry.Progress} / {entry.Goal}" : "-";
                    row.Progress.color = p.Text;
                    row.Reward.color = p.Title;
                    row.Description.color = p.TextDim;
                }

                row.Reward.text = definition.RewardLabel;
            }
        }
    }
}
