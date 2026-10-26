using System;
using System.Collections.Generic;
using BrokenSovereign.Achievement;
using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 격 창 · 신격 트리 (10/26, Figma 17-B, 1640×900). HUD 메뉴 [격] 또는 G.
    /// - 위: 현재 격 · 영웅의 길 [길 변경] · 업적 포인트(사용 가능 / 획득) · [초기화]
    /// - 왼쪽: 노드 판 (공용·격의 힘 / 영웅의 길 / 반신 가지). 좌클릭 선택, 우클릭 비활성화(끝 노드부터)
    /// - 오른쪽: 선택한 노드 상세 · [활성화] · 최근 획득한 업적 포인트 · [업적 전체 보기]
    /// - 반신 전에는 판 위에 "반신 돌파 시 개방"
    /// 다른 창과 같이 EventSystem 없이 마우스 위치로 판정 (UiKit). 열려 있는 동안 전투 입력 차단.
    /// </summary>
    public class GyeokWindowView : MonoBehaviour
    {
        private const float WIDTH = 1640f;
        private const float HEIGHT = 900f;
        private const float GRID = 58f;
        private static readonly Vector2 BOARD_POS = new Vector2(33f, 137f);
        private static readonly Vector2 BOARD_SIZE = new Vector2(1077f, 729f);
        private static readonly Vector2 DETAIL_POS = new Vector2(1136f, 137f);
        private static readonly Vector2 DETAIL_SIZE = new Vector2(471f, 729f);
        /// <summary>시작 노드 위치 (판 왼쪽 위 기준)</summary>
        private static readonly Vector2 ORIGIN = new Vector2(538f, 673f);
        private const int RECENT_ROWS = 4;

        private static Color LockedColor => new Color(0.36f, 0.38f, 0.43f);

        private sealed class NodeView
        {
            public GyeokNode Node;
            public RectTransform Rect;
            public Image Outer;
            public Image Inner;
            public float Size;
        }

        private sealed class LinkView
        {
            public GyeokNode A, B;
            public Image Line;
        }

        private GyeokTree _tree;
        private AchievementTracker _achievements;
        private PlayerProgression _progression;
        private HeroPathController _hero;
        private Action _openAchievements;
        private Action _changePath;

        private RectTransform _root;
        private RectTransform _panel;
        private RectTransform _board;
        private Image _close;
        private Text _tierText;
        private Text _pathText;
        private Image _changeButton;
        private Text _pointsBig;
        private Text _pointsSmall;
        private Image _resetButton;
        private Text _heroBranchLabel;
        private GameObject _lockOverlay;
        private Text _lockHint;
        private Image _selectRing;

        private Text _detailName;
        private Image _detailBadge;
        private Text _detailBadgeText;
        private Text _detailEffects;
        private Text _detailCost;
        private Image _activateButton;
        private Text _activateText;
        private Text _detailHint;
        private readonly List<(Text name, Text points)> _recentRows = new List<(Text, Text)>();
        private Text _recentEmpty;
        private Image _achievementsButton;

        private readonly List<NodeView> _nodeViews = new List<NodeView>();
        private readonly List<LinkView> _links = new List<LinkView>();
        private GyeokNode _selected;
        private NodeView _hover;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        public static GyeokWindowView Create(RectTransform parent, GameObject player, Action openAchievements, Action changePath)
        {
            RectTransform root = HudGraphics.CreateRect("GyeokWindow", parent);
            HudGraphics.Stretch(root);
            GyeokWindowView view = root.gameObject.AddComponent<GyeokWindowView>();
            view._tree = player.GetComponent<GyeokTree>();
            view._achievements = player.GetComponent<AchievementTracker>();
            view._progression = player.GetComponent<PlayerProgression>();
            view._hero = player.GetComponent<HeroPathController>();
            view._openAchievements = openAchievements;
            view._changePath = changePath;
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
            if (IsOpen || GameplayInput.IsBlocked || _tree == null)
            {
                return;
            }

            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            GameplayInput.IsBlocked = true;

            if (_selected == null)
            {
                _selected = _tree.Nodes.Count > 1 ? _tree.Nodes[1] : _tree.StartNode;
            }

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
            Text title = UiKit.TitleTab(_panel, "격", 108f);
            title.transform.parent.GetComponent<Image>().gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.9f);

            _close = UiKit.CloseButton(_panel);
            _close.rectTransform.sizeDelta = new Vector2(40f, 40f);
            _close.rectTransform.anchoredPosition = new Vector2(-18f, -18f);

            BuildTopBar();
            BuildBoard();
            BuildDetail();
        }

        private RectTransform Inset(string name, Vector2 position, Vector2 size)
        {
            Image inset = UiKit.Round(name, _panel, UiTheme.WithAlpha(UiTheme.P.Inset, 0.92f));
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

        private static Image SmallButton(string name, RectTransform parent, Vector2 position, Vector2 size, string label, bool gold)
        {
            Image button = UiKit.Button(name, parent, label, 15);
            UiKit.PlaceTopLeft(button.rectTransform, position, size);
            button.gameObject.AddComponent<Outline>().effectColor = gold ? UiTheme.P.Accent : UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.9f);

            if (gold)
            {
                button.transform.Find("Label").GetComponent<Text>().color = UiTheme.P.Title;
            }

            return button;
        }

        private void BuildTopBar()
        {
            UiPalette p = UiTheme.P;
            RectTransform bar = Inset("TopBar", new Vector2(33f, 48f), new Vector2(1573f, 70f));

            Label("TierLabel", bar, new Vector2(18f, 8f), new Vector2(100f, 18f), 13, p.TextDim).text = "현재 격";
            _tierText = UiKit.Title("Tier", bar, 30, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_tierText.rectTransform, new Vector2(18f, 28f), new Vector2(110f, 36f));

            Image separator = UiKit.Box("Sep", bar, UiTheme.WithAlpha(p.PlateStroke, 0.6f));
            UiKit.PlaceTopLeft(separator.rectTransform, new Vector2(118f, 14f), new Vector2(1f, 42f));

            Text pathLabel = Label("PathLabel", bar, new Vector2(136f, 8f), new Vector2(200f, 18f), 13, p.TextDim);
            pathLabel.text = "영웅의 길";
            _pathText = Label("Path", bar, new Vector2(136f, 30f), new Vector2(320f, 30f), 22, p.Text, bold: true);
            _changeButton = SmallButton("ChangePath", bar, new Vector2(466f, 17f), new Vector2(82f, 36f), "길 변경", true);

            Text pointsLabel = Label("PointsLabel", bar, new Vector2(1080f, 8f), new Vector2(160f, 18f), 13, p.TextDim);
            pointsLabel.text = "업적 포인트";
            _pointsBig = Label("PointsBig", bar, new Vector2(1080f, 28f), new Vector2(40f, 36f), 30, p.Title, bold: true);
            _pointsBig.horizontalOverflow = HorizontalWrapMode.Overflow;
            _pointsSmall = Label("PointsSmall", bar, new Vector2(1124f, 32f), new Vector2(140f, 30f), 17, p.Text, bold: true);

            Text rule = Label("Rule", bar, new Vector2(1262f, 12f), new Vector2(196f, 18f), 12, p.TextDim);
            rule.text = "업적 1개 달성 = +1 (시련 업적 +3)";
            _resetButton = SmallButton("Reset", bar, new Vector2(1474f, 17f), new Vector2(84f, 36f), "초기화", false);
        }

        private void BuildBoard()
        {
            UiPalette p = UiTheme.P;
            _board = Inset("Board", BOARD_POS, BOARD_SIZE);

            // 가운데가 조금 밝은 바탕
            Image glow = HudGraphics.CreateRect("Glow", _board).gameObject.AddComponent<Image>();
            glow.sprite = UiSprites.Glow;
            glow.color = UiTheme.WithAlpha(p.PlateTop, 0.55f);
            glow.raycastTarget = false;
            UiKit.Place(glow.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(900f, 680f));

            // 범례
            LegendDot(new Vector2(16f, 14f), p.Accent, true, "활성");
            LegendDot(new Vector2(110f, 14f), p.Accent, false, "활성 가능");
            LegendDot(new Vector2(204f, 14f), LockedColor, false, "잠김");
            Text hint = Label("Hint", _board, new Vector2(BOARD_SIZE.x - 420f, 12f), new Vector2(400f, 20f), 13, p.TextDim, TextAnchor.MiddleRight);
            hint.text = "좌클릭: 선택   ·   우클릭: 비활성화 (끝 노드부터)";

            // 가지 이름
            BranchLabel("반신 가지", new Vector2(0f, -9f), p.Text);
            BranchLabel("공용 · 격의 힘", new Vector2(-4f, -8f), p.Text);
            _heroBranchLabel = BranchLabel("영웅의 길", new Vector2(4.4f, -8f), p.Title);

            // 연결선 (노드 아래)
            foreach (GyeokNode node in _tree != null ? _tree.Nodes : (IReadOnlyList<GyeokNode>)new GyeokNode[0])
            {
                foreach (GyeokNode link in node.Links)
                {
                    if (string.CompareOrdinal(node.Id, link.Id) < 0)
                    {
                        _links.Add(BuildLink(node, link));
                    }
                }
            }

            // 선택 표시 (노드 아래)
            _selectRing = HudGraphics.CreateRect("Select", _board).gameObject.AddComponent<Image>();
            _selectRing.sprite = HudGraphics.RingSprite;
            _selectRing.raycastTarget = false;
            _selectRing.rectTransform.anchorMin = _selectRing.rectTransform.anchorMax = new Vector2(0f, 1f);
            _selectRing.rectTransform.pivot = new Vector2(0.5f, 0.5f);

            if (_tree != null)
            {
                foreach (GyeokNode node in _tree.Nodes)
                {
                    _nodeViews.Add(BuildNode(node));
                }
            }

            // 반신 전 잠금
            Image lockDim = UiKit.Box("Lock", _board, new Color(0.02f, 0.02f, 0.03f, 0.78f));
            HudGraphics.Stretch(lockDim.rectTransform);
            _lockOverlay = lockDim.gameObject;
            Text lockTitle = UiKit.Title("LockTitle", lockDim.transform, 34, p.Title, TextAnchor.MiddleCenter);
            UiKit.Place(lockTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 24f), new Vector2(800f, 44f));
            lockTitle.text = "반신 돌파 시 개방";
            // 보조 글: 제목과 10px 띄움
            _lockHint = UiKit.Label("LockHint", lockDim.transform, 17, p.TextDim, TextAnchor.UpperCenter);
            UiKit.Place(_lockHint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(800f, 60f));
        }

        private void LegendDot(Vector2 position, Color color, bool filled, string label)
        {
            Image dot = UiKit.Box("Legend", _board, filled ? color : UiTheme.P.Inset);
            dot.sprite = HudGraphics.CircleSprite;
            UiKit.PlaceTopLeft(dot.rectTransform, position + new Vector2(0f, 3f), new Vector2(13f, 13f));

            if (!filled)
            {
                Image ring = UiKit.Box("Ring", dot.rectTransform, color);
                ring.sprite = HudGraphics.RingSprite;
                HudGraphics.Stretch(ring.rectTransform);
            }

            Text text = Label("LegendText", _board, position + new Vector2(20f, 0f), new Vector2(80f, 20f), 13, UiTheme.P.TextDim);
            text.text = label;
        }

        private Text BranchLabel(string label, Vector2 grid, Color color)
        {
            Text text = UiKit.Label("Branch", _board, 16, color, TextAnchor.MiddleCenter, FontStyle.Bold);
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = new Vector2(0f, 1f);
            text.rectTransform.pivot = new Vector2(0.5f, 0.5f);
            text.rectTransform.sizeDelta = new Vector2(260f, 24f);
            text.rectTransform.anchoredPosition = GridToBoard(grid);
            text.text = label;
            return text;
        }

        private static Vector2 GridToBoard(Vector2 grid)
        {
            return new Vector2(ORIGIN.x + grid.x * GRID, -(ORIGIN.y + grid.y * GRID));
        }

        private LinkView BuildLink(GyeokNode a, GyeokNode b)
        {
            Vector2 from = GridToBoard(a.Grid), to = GridToBoard(b.Grid);
            Vector2 delta = to - from;
            Image line = UiKit.Box("Link", _board, LockedColor);
            RectTransform rect = line.rectTransform;
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(delta.magnitude, 3f);
            rect.anchoredPosition = (from + to) * 0.5f;
            rect.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg);
            return new LinkView { A = a, B = b, Line = line };
        }

        private NodeView BuildNode(GyeokNode node)
        {
            float size;

            switch (node.Kind)
            {
                case GyeokNodeKind.Start: size = 40f; break;
                case GyeokNodeKind.Rare: size = 32f; break;
                case GyeokNodeKind.Glyph: size = 46f; break;
                default: size = 22f; break;
            }

            RectTransform rect = HudGraphics.CreateRect($"Node_{node.Id}", _board);
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(size + 10f, size + 10f);
            rect.anchoredPosition = GridToBoard(node.Grid);

            var view = new NodeView { Node = node, Rect = rect, Size = size };
            bool diamond = node.Kind == GyeokNodeKind.Rare || node.Kind == GyeokNodeKind.Start;
            float shape = diamond ? size * 0.72f : size;

            view.Outer = UiKit.Box("Outer", rect, LockedColor);
            view.Inner = UiKit.Box("Inner", rect, UiTheme.P.Inset);

            if (node.Kind == GyeokNodeKind.Glyph)
            {
                view.Outer.sprite = UiSprites.OctagonFill;
                view.Inner.sprite = UiSprites.OctagonFill;
            }
            else if (!diamond)
            {
                view.Outer.sprite = HudGraphics.CircleSprite;
                view.Inner.sprite = HudGraphics.CircleSprite;
            }

            UiKit.Place(view.Outer.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(shape, shape));
            UiKit.Place(view.Inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(shape - 5f, shape - 5f));

            if (diamond)
            {
                view.Outer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                view.Inner.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            }

            // 일반 노드가 아니면 이름 표시 (격의 고동은 오른쪽, 나머지는 아래)
            if (node.Kind != GyeokNodeKind.Normal)
            {
                Text label = UiKit.Label("Name", rect, 13, UiTheme.P.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                bool right = node.Id == "t_pulse";
                UiKit.Place(label.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(right ? 0f : 0.5f, 0.5f),
                    right ? new Vector2(size * 0.5f + 14f, 0f) : new Vector2(0f, -size * 0.5f - 14f), new Vector2(120f, 20f));
                label.alignment = right ? TextAnchor.MiddleLeft : TextAnchor.MiddleCenter;
                label.text = node.Name;
            }

            return view;
        }

        private void BuildDetail()
        {
            UiPalette p = UiTheme.P;
            RectTransform detail = Inset("Detail", DETAIL_POS, DETAIL_SIZE);
            float w = DETAIL_SIZE.x - 44f;

            Label("Selected", detail, new Vector2(22f, 18f), new Vector2(200f, 18f), 13, p.TextDim).text = "선택한 노드";
            _detailName = UiKit.Title("Name", detail, 28, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_detailName.rectTransform, new Vector2(22f, 40f), new Vector2(w, 38f));

            _detailBadge = UiKit.Round("Badge", detail, p.Accent);
            UiKit.PlaceTopLeft(_detailBadge.rectTransform, new Vector2(22f, 86f), new Vector2(68f, 20f));
            _detailBadgeText = UiKit.Label("Text", _detailBadge.transform, 12, new Color(0.1f, 0.08f, 0.04f), TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_detailBadgeText.rectTransform);
            _detailBadgeText.GetComponent<Shadow>().enabled = false;

            Divider(detail, 124f, w);
            _detailEffects = Label("Effects", detail, new Vector2(22f, 140f), new Vector2(w, 110f), 17, p.Text, TextAnchor.UpperLeft);
            _detailEffects.lineSpacing = 1.25f;

            Label("CostLabel", detail, new Vector2(22f, 262f), new Vector2(60f, 24f), 15, p.TextDim).text = "비용";
            _detailCost = Label("Cost", detail, new Vector2(78f, 262f), new Vector2(240f, 24f), 17, p.Title, bold: true);

            _activateButton = UiKit.Round("Activate", detail, p.Accent);
            UiKit.PlaceTopLeft(_activateButton.rectTransform, new Vector2(22f, 298f), new Vector2(w, 52f));
            _activateText = UiKit.Label("Text", _activateButton.transform, 20, new Color(0.12f, 0.09f, 0.04f), TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_activateText.rectTransform);
            _activateText.GetComponent<Shadow>().enabled = false;

            // 보조 글: 버튼과 10px 띄움
            _detailHint = Label("Hint", detail, new Vector2(22f, 360f), new Vector2(w, 18f), 13, p.TextDim);

            Divider(detail, 404f, w);
            Label("RecentTitle", detail, new Vector2(22f, 420f), new Vector2(w, 24f), 17, p.Accent, bold: true).text = "최근 획득한 업적 포인트";

            for (int i = 0; i < RECENT_ROWS; i++)
            {
                float y = 458f + i * 40f;
                Text name = Label("RecentName", detail, new Vector2(22f, y), new Vector2(w - 60f, 24f), 15, p.Text);
                Text points = Label("RecentPoints", detail, new Vector2(22f + w - 60f, y), new Vector2(60f, 24f), 15, p.Title, TextAnchor.MiddleRight, true);
                _recentRows.Add((name, points));
            }

            _recentEmpty = Label("RecentEmpty", detail, new Vector2(22f, 458f), new Vector2(w, 24f), 15, p.TextDim);
            _recentEmpty.text = "아직 달성한 업적이 없습니다";

            _achievementsButton = SmallButton("Achievements", detail, new Vector2(22f, DETAIL_SIZE.y - 66f), new Vector2(w, 44f), "업적 전체 보기", false);
        }

        private static void Divider(RectTransform parent, float y, float width)
        {
            Image line = UiKit.Box("Divider", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.6f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(22f, y), new Vector2(width, 1f));
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

            bool unlocked = _tree.IsUnlocked;

            if (ButtonClicked(_resetButton, unlocked && _tree.PointsSpent > 0))
            {
                _tree.ResetAll();
            }

            bool canChange = _progression != null && _progression.Tier >= AscensionTier.Demigod && _hero != null;

            if (ButtonClicked(_changeButton, canChange))
            {
                Close();
                _changePath?.Invoke();
                return;
            }

            if (ButtonClicked(_achievementsButton, true))
            {
                Close();
                _openAchievements?.Invoke();
                return;
            }

            // 노드: 마우스 올림 · 좌클릭 선택 · 우클릭 비활성화
            _hover = null;

            foreach (NodeView view in _nodeViews)
            {
                if (UiKit.IsHover(view.Rect))
                {
                    _hover = view;
                }
            }

            if (_hover != null && unlocked)
            {
                if (UiKit.Clicked)
                {
                    _selected = _hover.Node;
                }
                else if (mouse != null && mouse.rightButton.wasPressedThisFrame)
                {
                    _selected = _hover.Node;
                    _tree.Deactivate(_hover.Node);
                }
            }

            // [활성화] / [비활성화]
            bool active = _tree.IsActive(_selected);
            bool buttonEnabled = active ? _tree.CanDeactivate(_selected) : _tree.CanActivate(_selected);

            if (UiKit.IsHover(_activateButton.rectTransform) && UiKit.Clicked && buttonEnabled)
            {
                if (active)
                {
                    _tree.Deactivate(_selected);
                }
                else
                {
                    _tree.Activate(_selected);
                }
            }

            Refresh();
        }

        private static bool ButtonClicked(Image button, bool enabled)
        {
            bool hover = enabled && UiKit.IsHover(button.rectTransform);
            button.color = !enabled ? new Color(0.55f, 0.55f, 0.55f) : hover ? new Color(1f, 0.88f, 0.72f) : Color.white;
            return hover && UiKit.Clicked;
        }

        private void Refresh()
        {
            UiPalette p = UiTheme.P;
            bool unlocked = _tree.IsUnlocked;

            // 위쪽 막대
            _tierText.text = _progression != null ? AscensionModel.DisplayName(_progression.Tier) : "-";
            HeroPathDefinition path = _hero != null ? _hero.Current : null;
            _pathText.text = path != null ? $"{path.DisplayName} · {path.Title}" : unlocked ? "선택 전" : "반신 돌파 시 선택";
            _pathText.color = path != null ? p.Text : p.TextDim;
            _changeButton.transform.Find("Label").GetComponent<Text>().text = path != null ? "길 변경" : "길 선택";
            _pointsBig.text = _tree.PointsAvailable.ToString();
            _pointsSmall.text = $"/ {_tree.PointsEarned}  사용 가능";
            // 큰 숫자 폭만큼 뒤에 붙임 (두 자리 대비)
            Vector2 small = _pointsSmall.rectTransform.anchoredPosition;
            _pointsSmall.rectTransform.anchoredPosition = new Vector2(1080f + _pointsBig.preferredWidth + 8f, small.y);
            _heroBranchLabel.text = path != null ? $"영웅의 길 · {path.DisplayName}" : "영웅의 길";

            _lockOverlay.SetActive(!unlocked);
            _lockHint.text = _progression != null ? _progression.NextAscensionHint : string.Empty;

            // 노드 · 연결선
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);

            foreach (NodeView view in _nodeViews)
            {
                GyeokNode node = view.Node;
                bool active = _tree.IsActive(node);
                bool reachable = unlocked && _tree.IsReachable(node) &&
                    (node.Branch != GyeokBranch.Hero || _tree.HasHeroPath);

                if (active)
                {
                    view.Outer.color = node.Kind == GyeokNodeKind.Start ? Color.white : p.Title;
                    view.Inner.color = node.Kind == GyeokNodeKind.Start ? new Color(1f, 0.95f, 0.82f) : p.Accent;
                }
                else
                {
                    view.Outer.color = reachable ? p.Accent : LockedColor;
                    view.Inner.color = UiTheme.WithAlpha(p.Inset, 1f);
                }

                float scale = _hover == view && unlocked ? 1.15f : 1f;
                view.Rect.localScale = Vector3.Lerp(view.Rect.localScale, Vector3.one * scale, 0.35f);
            }

            foreach (LinkView link in _links)
            {
                bool lit = _tree.IsActive(link.A) && _tree.IsActive(link.B);
                link.Line.color = lit ? p.Accent : UiTheme.WithAlpha(LockedColor, 0.8f);
            }

            NodeView selectedView = _nodeViews.Find(v => v.Node == _selected);
            _selectRing.enabled = selectedView != null && unlocked;

            if (selectedView != null)
            {
                float ring = selectedView.Size + 24f;
                _selectRing.rectTransform.sizeDelta = new Vector2(ring, ring);
                _selectRing.rectTransform.anchoredPosition = selectedView.Rect.anchoredPosition;
                _selectRing.color = UiTheme.WithAlpha(p.Title, 0.45f + pulse * 0.5f);
            }

            RefreshDetail();
            RefreshRecent();
        }

        private void RefreshDetail()
        {
            UiPalette p = UiTheme.P;
            GyeokNode node = _selected;

            if (node == null)
            {
                return;
            }

            _detailName.text = node.Name;
            _detailBadgeText.text = node.KindName;
            _detailBadge.color = node.Kind == GyeokNodeKind.Normal ? p.TextDim : p.Accent;
            _detailEffects.text = _tree.Describe(node);
            _detailCost.text = node.Cost > 0 ? $"업적 포인트 {node.Cost}" : "없음";

            bool active = _tree.IsActive(node);
            string block = active ? null : _tree.GetBlockReason(node);
            bool canDeactivate = active && _tree.CanDeactivate(node);
            bool enabled = active ? canDeactivate : block == null;

            _activateText.text = active ? (canDeactivate ? "비활성화" : "활성화됨") : "활성화";
            bool hover = enabled && UiKit.IsHover(_activateButton.rectTransform);
            _activateButton.color = !enabled ? new Color(0.32f, 0.33f, 0.36f) : active ? new Color(0.75f, 0.62f, 0.4f) :
                hover ? Color.Lerp(p.Accent, Color.white, 0.25f) : p.Accent;
            _activateText.color = enabled ? new Color(0.12f, 0.09f, 0.04f) : p.TextDim;

            if (block != null && !active)
            {
                _detailHint.text = block;
                _detailHint.color = new Color(0.95f, 0.55f, 0.45f);
            }
            else
            {
                _detailHint.text = node == _tree.StartNode ? "시작 노드는 항상 활성" : "노드 우클릭 = 비활성화 (끝 노드부터)";
                _detailHint.color = p.TextDim;
            }
        }

        private void RefreshRecent()
        {
            IReadOnlyList<AchievementTracker.Entry> recent = _achievements != null ? _achievements.Recent : null;
            int count = recent != null ? recent.Count : 0;
            _recentEmpty.enabled = count == 0;

            for (int i = 0; i < _recentRows.Count; i++)
            {
                bool show = i < count;
                _recentRows[i].name.enabled = show;
                _recentRows[i].points.enabled = show;

                if (show)
                {
                    _recentRows[i].name.text = $"◆  {recent[i].Definition.Name}";
                    _recentRows[i].points.text = $"+{recent[i].Definition.Points}";
                }
            }
        }
    }
}
