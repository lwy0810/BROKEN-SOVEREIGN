using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 창 (K, 10/21 Figma 시안 C-04 "기술 창 (트리·포인트·강화)").
    /// 위 왼쪽: 기술 트리 (격 행 범인·초인·반신 × 키 열 RMB·1·2·3·4·4′·5), 노드 = 기술 아이콘 + 누적 포인트 x/15 + 이름.
    /// 위 오른쪽: 장착 슬롯 목록 (끌어 놓아 두 키의 기술 맞바꾸기, 후보가 둘 이상이면 교체 버튼, 비전투만).
    /// 아래: 강화 패널 (선택 기술 아이콘·Lv 막대·현재→다음 수치·강화 분기 A/B·[강화 +1]·[초기화]). 남은 기술 포인트는 오른쪽 위.
    /// 규칙은 SkillGrowth (레벨당 피해 +5%, 최대 15, 우클릭 Lv 3 분기). 제목 탭 끌기 이동, ×·K·Esc 닫기. HudView가 생성합니다.
    /// </summary>
    public class SkillWindowView : MonoBehaviour
    {
        private const float WIDTH = 1600f;
        private const float HEIGHT = 960f;
        private const float TREE_X = 24f;
        private const float TREE_Y = 78f;
        private const float TREE_W = 1120f;
        private const float TREE_H = 520f;
        private const float NODE = 72f;
        private const float COLUMN_X = 200f;
        private const float COLUMN_STEP = 144f;
        private const float LIST_X = 1164f;
        private const float LIST_W = 412f;
        private const float PANEL_Y = 616f;
        private const float PANEL_H = 320f;
        private const int STAT_ROWS = 5;

        private static readonly SkillSlot[] LIST_SLOTS =
        {
            SkillSlot.LeftClick, SkillSlot.RightClick, SkillSlot.Key1, SkillSlot.Key2, SkillSlot.Key3,
            SkillSlot.Key4, SkillSlot.Key5, SkillSlot.Key6
        };

        private static readonly string[] LIST_KEYS = { "LMB", "RMB", "1", "2", "3", "4", "5", "6" };
        private static readonly string[] TIER_ROWS = { "범인", "초인", "반신" };
        private static readonly string[] TIER_JOBS = { "성기사 · 공용", "성투사", "영웅의 길" };

        private sealed class NodeDef
        {
            public SkillSlot Slot;
            public string Key;
            public int Column;
            public AscensionTier Tier;
            public string Name;
            public bool Hero;
        }

        private static readonly NodeDef[] NODES =
        {
            new NodeDef { Slot = SkillSlot.RightClick, Key = "RMB", Column = 0, Tier = AscensionTier.Mortal, Name = "강타" },
            new NodeDef { Slot = SkillSlot.Key1, Key = "1", Column = 1, Tier = AscensionTier.Mortal, Name = "격광 강타" },
            new NodeDef { Slot = SkillSlot.Key2, Key = "2", Column = 2, Tier = AscensionTier.Mortal, Name = "진격" },
            new NodeDef { Slot = SkillSlot.Key3, Key = "3", Column = 3, Tier = AscensionTier.Superhuman, Name = "불굴의 갑주" },
            new NodeDef { Slot = SkillSlot.Key4, Key = "4", Column = 4, Tier = AscensionTier.Superhuman, Name = "전장의 함성" },
            new NodeDef { Slot = SkillSlot.Key4, Key = "4′", Column = 5, Tier = AscensionTier.Demigod, Name = "분쇄 일격 · 난천무", Hero = true },
            new NodeDef { Slot = SkillSlot.Key5, Key = "5", Column = 6, Tier = AscensionTier.Demigod, Name = "모드변환" }
        };

        private sealed class NodeView
        {
            public NodeDef Def;
            public SkillDefinition Skill;
            public bool Replaced;
            public RectTransform Rect;
            public Image Glow;
            public Image Tile;
            public Image Icon;
            public Image Frame;
            public Image Pill;
            public Text Points;
            public Text Name;
            public Text Key;
            public Vector2 Center;
        }

        private sealed class ListRow
        {
            public SkillSlot Slot;
            public Image Background;
            public Image Icon;
            public Text Name;
            public Text Level;
            public Image Swap;
        }

        private SkillCaster _caster;
        private PlayerProgression _progression;
        private PlayerJob _job;
        private HeroPathController _hero;
        private RectTransform _panel;
        private Image _close;
        private WindowDrag _drag;
        private Text _header;
        private Text _pointsText;
        private Text _message;
        private float _messageEndTime;
        private bool _dirty = true;
        private int _selected;

        private readonly List<NodeView> _nodes = new List<NodeView>();
        private readonly List<Image> _connectors = new List<Image>();
        private readonly List<ListRow> _rows = new List<ListRow>();

        // 강화 패널
        private Image _bigTile;
        private Image _bigIcon;
        private Text _bigName;
        private Text _bigSub;
        private UiBar _levelBar;
        private Text _levelText;
        private Text _description;
        private Text _statHeader;
        private readonly List<Text[]> _statRows = new List<Text[]>();
        private Text _branchHeader;
        private readonly Image[] _branchCards = new Image[2];
        private readonly Text[] _branchNames = new Text[2];
        private readonly Text[] _branchTexts = new Text[2];
        private Image _levelButton;
        private Text _levelButtonText;
        private Image _resetButton;

        // 장착 슬롯 끌어 놓기 (키 순서 바꾸기)
        private const float DRAG_THRESHOLD = 6f;
        private int _pressRow = -1;
        private Vector2 _pressPosition;
        private bool _dragging;
        private RectTransform _ghost;
        private Image _ghostIcon;
        private Text _ghostText;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(SkillCaster caster, GameObject player)
        {
            _caster = caster;
            _progression = player.GetComponent<PlayerProgression>();
            _job = player.GetComponent<PlayerJob>();
            _hero = player.GetComponent<HeroPathController>();
            UiPalette p = UiTheme.P;

            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 10f), new Vector2(WIDTH, HEIGHT));
            _drag = new WindowDrag(_panel, (RectTransform)UiKit.TitleTab(_panel, "기술", 240f).transform.parent);
            _close = UiKit.CloseButton(_panel);

            _header = UiKit.Title("Header", _panel, 18, p.Text, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_header.rectTransform, new Vector2(32f, 28f), new Vector2(600f, 30f));

            Image chip = UiKit.Sliced("PointsChip", _panel, UiSprites.ButtonEquip);
            UiKit.PlaceTopLeft(chip.rectTransform, new Vector2(WIDTH - 60f - 280f, 22f), new Vector2(280f, 36f));
            _pointsText = UiKit.Label("Points", chip.transform, 15, p.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_pointsText.rectTransform);

            BuildTree();
            BuildList();
            BuildPanel();
            BuildGhost();

            _message = UiKit.Label("Message", _panel, 16, p.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, -30f), new Vector2(WIDTH, 26f));
            _message.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            if (_caster != null)
            {
                _caster.Loadout.Changed += OnLoadoutChanged;
                _caster.Loadout.GrowthChanged += MarkDirty;
            }

            _panel.gameObject.SetActive(false);
        }

        private void OnLoadoutChanged(SkillSlot slot, SkillDefinition skill)
        {
            _dirty = true;
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        // ── 트리 ─────────────────────────────────────────────
        private void BuildTree()
        {
            UiPalette p = UiTheme.P;
            Image frame = UiKit.Box("Tree", _panel, UiTheme.WithAlpha(p.Inset, 0.55f));
            UiKit.PlaceTopLeft(frame.rectTransform, new Vector2(TREE_X, TREE_Y), new Vector2(TREE_W, TREE_H));
            float rowHeight = TREE_H / 3f;

            for (int r = 0; r < 3; r++)
            {
                float y = TREE_Y + r * rowHeight;

                if (r > 0)
                {
                    Image line = UiKit.Box("RowLine", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.3f));
                    UiKit.PlaceTopLeft(line.rectTransform, new Vector2(TREE_X + 12f, y), new Vector2(TREE_W - 24f, 1f));
                }

                Text tier = UiKit.Title("Tier", _panel, 22, p.Title, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(tier.rectTransform, new Vector2(TREE_X + 22f, y + rowHeight * 0.5f - 26f), new Vector2(140f, 30f));
                tier.text = TIER_ROWS[r];
                Text job = UiKit.Label("Job", _panel, 13, p.TextDim, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(job.rectTransform, new Vector2(TREE_X + 22f, y + rowHeight * 0.5f + 4f), new Vector2(140f, 20f));
                job.text = TIER_JOBS[r];
            }

            // 연결선 먼저 (노드 아래에 그려지도록)
            for (int i = 0; i < NODES.Length - 1; i++)
            {
                for (int s = 0; s < 3; s++)
                {
                    _connectors.Add(UiKit.Box("Connector", _panel, Color.white));
                }
            }

            foreach (NodeDef def in NODES)
            {
                _nodes.Add(CreateNode(def, rowHeight));
            }

            for (int i = 0; i < NODES.Length - 1; i++)
            {
                Vector2 a = _nodes[i].Center, b = _nodes[i + 1].Center;
                float half = NODE * 0.5f;

                if (Mathf.Abs(a.y - b.y) < 1f)
                {
                    PlaceLine(_connectors[i * 3], new Vector2(a.x + half, a.y), new Vector2(b.x - half, b.y));
                    _connectors[i * 3 + 1].gameObject.SetActive(false);
                    _connectors[i * 3 + 2].gameObject.SetActive(false);
                }
                else
                {
                    // 오른쪽으로 나가서 아래로 내려간 뒤 다음 노드로
                    float midX = (a.x + b.x) * 0.5f;
                    PlaceLine(_connectors[i * 3], new Vector2(a.x + half, a.y), new Vector2(midX, a.y));
                    PlaceLine(_connectors[i * 3 + 1], new Vector2(midX, a.y), new Vector2(midX, b.y));
                    PlaceLine(_connectors[i * 3 + 2], new Vector2(midX, b.y), new Vector2(b.x - half, b.y));
                }
            }
        }

        private static void PlaceLine(Image line, Vector2 from, Vector2 to)
        {
            Vector2 min = Vector2.Min(from, to), max = Vector2.Max(from, to);
            UiKit.PlaceTopLeft(line.rectTransform, min - new Vector2(1.5f, 1.5f), max - min + new Vector2(3f, 3f));
        }

        private NodeView CreateNode(NodeDef def, float rowHeight)
        {
            UiPalette p = UiTheme.P;
            float cx = TREE_X + COLUMN_X + def.Column * COLUMN_STEP;
            float cy = TREE_Y + (int)def.Tier * rowHeight + 18f + NODE * 0.5f;
            var node = new NodeView { Def = def, Center = new Vector2(cx, cy) };

            node.Glow = UiKit.Sliced("Glow", _panel, UiSprites.Glow);
            node.Glow.type = Image.Type.Simple;
            UiKit.PlaceTopLeft(node.Glow.rectTransform, new Vector2(cx - NODE, cy - NODE), new Vector2(NODE * 2f, NODE * 2f));

            node.Tile = UiKit.Sliced("Node", _panel, UiSprites.Tile);
            node.Rect = node.Tile.rectTransform;
            UiKit.PlaceTopLeft(node.Rect, new Vector2(cx - NODE * 0.5f, cy - NODE * 0.5f), new Vector2(NODE, NODE));

            node.Icon = HudGraphics.CreateRect("Icon", node.Rect).gameObject.AddComponent<Image>();
            node.Icon.raycastTarget = false;
            node.Icon.preserveAspect = true;
            HudGraphics.Stretch(node.Icon.rectTransform);
            node.Icon.rectTransform.offsetMin = new Vector2(14f, 14f);
            node.Icon.rectTransform.offsetMax = new Vector2(-14f, -14f);
            node.Icon.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            node.Frame = UiKit.Sliced("Frame", node.Rect, ItemVisuals.Frame);
            HudGraphics.Stretch(node.Frame.rectTransform);

            node.Key = UiKit.Label("Key", node.Rect, 11, p.Title, TextAnchor.LowerRight, FontStyle.Bold);
            HudGraphics.Stretch(node.Key.rectTransform);
            node.Key.rectTransform.offsetMin = new Vector2(4f, 3f);
            node.Key.rectTransform.offsetMax = new Vector2(-6f, -3f);
            node.Key.text = def.Key;

            node.Pill = UiKit.Sliced("Pill", _panel, UiSprites.BarFrame);
            UiKit.PlaceTopLeft(node.Pill.rectTransform, new Vector2(cx - 34f, cy + NODE * 0.5f + 6f), new Vector2(68f, 22f));
            node.Points = UiKit.Label("Points", node.Pill.transform, 13, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(node.Points.rectTransform);

            node.Name = UiKit.Label("Name", _panel, 14, p.Text, TextAnchor.UpperCenter);
            UiKit.PlaceTopLeft(node.Name.rectTransform, new Vector2(cx - 72f, cy + NODE * 0.5f + 32f), new Vector2(144f, 20f));
            return node;
        }

        // ── 장착 슬롯 목록 ───────────────────────────────────
        private void BuildList()
        {
            UiPalette p = UiTheme.P;
            Image frame = UiKit.Box("List", _panel, UiTheme.WithAlpha(p.Inset, 0.55f));
            UiKit.PlaceTopLeft(frame.rectTransform, new Vector2(LIST_X, TREE_Y), new Vector2(LIST_W, TREE_H));
            Text title = UiKit.Title("ListTitle", _panel, 17, p.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(title.rectTransform, new Vector2(LIST_X + 18f, TREE_Y + 10f), new Vector2(200f, 28f));
            title.text = "장착 슬롯";
            Text hint = UiKit.Label("ListHint", _panel, 12, p.TextDim, TextAnchor.MiddleRight);
            UiKit.PlaceTopLeft(hint.rectTransform, new Vector2(LIST_X + LIST_W - 230f, TREE_Y + 10f), new Vector2(212f, 28f));
            hint.text = "끌어서 키 순서 바꾸기 (비전투)";

            float rowH = (TREE_H - 52f) / LIST_SLOTS.Length;

            for (int i = 0; i < LIST_SLOTS.Length; i++)
            {
                float y = TREE_Y + 46f + i * rowH;
                var row = new ListRow { Slot = LIST_SLOTS[i] };
                row.Background = UiKit.Round("Row", _panel, UiTheme.WithAlpha(p.Hover, 0f));
                UiKit.PlaceTopLeft(row.Background.rectTransform, new Vector2(LIST_X + 10f, y), new Vector2(LIST_W - 20f, rowH - 4f));

                Image tile = UiKit.Sliced("Tile", row.Background.transform, UiSprites.Tile);
                UiKit.PlaceTopLeft(tile.rectTransform, new Vector2(6f, 3f), new Vector2(rowH - 10f, rowH - 10f));
                row.Icon = HudGraphics.CreateRect("Icon", tile.transform).gameObject.AddComponent<Image>();
                row.Icon.raycastTarget = false;
                row.Icon.preserveAspect = true;
                HudGraphics.Stretch(row.Icon.rectTransform);
                row.Icon.rectTransform.offsetMin = new Vector2(9f, 9f);
                row.Icon.rectTransform.offsetMax = new Vector2(-9f, -9f);

                Text key = UiKit.Label("Key", row.Background.transform, 13, p.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.PlaceTopLeft(key.rectTransform, new Vector2(rowH + 4f, 0f), new Vector2(44f, rowH - 4f));
                key.text = LIST_KEYS[i];
                row.Name = UiKit.Label("Name", row.Background.transform, 15, p.Text, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(row.Name.rectTransform, new Vector2(rowH + 48f, 0f), new Vector2(170f, rowH - 4f));
                row.Level = UiKit.Label("Level", row.Background.transform, 13, p.TextDim, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.PlaceTopLeft(row.Level.rectTransform, new Vector2(LIST_W - 20f - 132f, 0f), new Vector2(60f, rowH - 4f));

                row.Swap = UiKit.Button("Swap", row.Background.transform, "교체");
                UiKit.PlaceTopLeft(row.Swap.rectTransform, new Vector2(LIST_W - 20f - 64f, 6f), new Vector2(56f, rowH - 16f));
                row.Swap.GetComponentInChildren<Text>().fontSize = 12;
                _rows.Add(row);
            }
        }

        private void BuildGhost()
        {
            _ghost = UiKit.Plate("DragGhost", _panel).rectTransform;
            _ghost.sizeDelta = new Vector2(220f, 46f);
            _ghost.pivot = new Vector2(0.5f, 0.5f);
            _ghost.anchorMin = _ghost.anchorMax = new Vector2(0.5f, 0.5f);
            _ghostIcon = HudGraphics.CreateRect("Icon", _ghost).gameObject.AddComponent<Image>();
            _ghostIcon.raycastTarget = false;
            _ghostIcon.preserveAspect = true;
            UiKit.PlaceTopLeft(_ghostIcon.rectTransform, new Vector2(10f, 7f), new Vector2(32f, 32f));
            _ghostText = UiKit.Label("Name", _ghost, 15, UiTheme.P.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(_ghostText.rectTransform, new Vector2(50f, 0f), new Vector2(160f, 46f));
            _ghost.gameObject.SetActive(false);
        }

        // ── 강화 패널 ───────────────────────────────────────
        private void BuildPanel()
        {
            UiPalette p = UiTheme.P;
            Image frame = UiKit.Box("Enhance", _panel, UiTheme.WithAlpha(p.Inset, 0.55f));
            RectTransform box = frame.rectTransform;
            UiKit.PlaceTopLeft(box, new Vector2(TREE_X, PANEL_Y), new Vector2(WIDTH - 48f, PANEL_H));

            _bigTile = UiKit.Sliced("BigTile", box, UiSprites.Tile);
            UiKit.PlaceTopLeft(_bigTile.rectTransform, new Vector2(24f, 24f), new Vector2(112f, 112f));
            _bigIcon = HudGraphics.CreateRect("BigIcon", _bigTile.transform).gameObject.AddComponent<Image>();
            _bigIcon.raycastTarget = false;
            _bigIcon.preserveAspect = true;
            HudGraphics.Stretch(_bigIcon.rectTransform);
            _bigIcon.rectTransform.offsetMin = new Vector2(22f, 22f);
            _bigIcon.rectTransform.offsetMax = new Vector2(-22f, -22f);
            _bigIcon.gameObject.AddComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.8f);

            _bigName = UiKit.Title("Name", box, 28, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_bigName.rectTransform, new Vector2(156f, 20f), new Vector2(440f, 40f));
            _bigSub = UiKit.Label("Sub", box, 14, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_bigSub.rectTransform, new Vector2(156f, 60f), new Vector2(440f, 22f));

            _levelBar = UiKit.Bar("Level", box, p.Accent, p.Accent * 0.5f);
            UiKit.PlaceTopLeft(_levelBar.Rect, new Vector2(156f, 96f), new Vector2(330f, 14f));
            _levelText = UiKit.Label("LevelText", box, 15, p.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(_levelText.rectTransform, new Vector2(498f, 92f), new Vector2(110f, 22f));

            _description = UiKit.Label("Desc", box, 14, p.Text, TextAnchor.UpperLeft);
            _description.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.PlaceTopLeft(_description.rectTransform, new Vector2(24f, 156f), new Vector2(580f, 140f));
            _description.lineSpacing = 1.25f;

            Image divider = UiKit.Box("Divider", box, UiTheme.WithAlpha(p.PlateStroke, 0.35f));
            UiKit.PlaceTopLeft(divider.rectTransform, new Vector2(628f, 20f), new Vector2(1f, PANEL_H - 40f));

            _statHeader = UiKit.Title("StatHeader", box, 17, p.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_statHeader.rectTransform, new Vector2(652f, 20f), new Vector2(320f, 28f));

            for (int i = 0; i < STAT_ROWS; i++)
            {
                float y = 60f + i * 34f;
                Text label = UiKit.Label("StatName", box, 15, p.TextDim, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(label.rectTransform, new Vector2(660f, y), new Vector2(140f, 30f));
                Text now = UiKit.Label("Now", box, 16, p.Text, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.PlaceTopLeft(now.rectTransform, new Vector2(800f, y), new Vector2(70f, 30f));
                Text arrow = UiKit.Label("Arrow", box, 14, p.TextDim, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(arrow.rectTransform, new Vector2(872f, y), new Vector2(30f, 30f));
                arrow.text = "→";
                Text next = UiKit.Label("Next", box, 16, new Color(0.37f, 0.7f, 0.42f), TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.PlaceTopLeft(next.rectTransform, new Vector2(904f, y), new Vector2(80f, 30f));
                _statRows.Add(new[] { label, now, arrow, next });
            }

            Image divider2 = UiKit.Box("Divider", box, UiTheme.WithAlpha(p.PlateStroke, 0.35f));
            UiKit.PlaceTopLeft(divider2.rectTransform, new Vector2(1000f, 20f), new Vector2(1f, PANEL_H - 40f));

            _branchHeader = UiKit.Title("BranchHeader", box, 17, p.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_branchHeader.rectTransform, new Vector2(1024f, 20f), new Vector2(480f, 28f));

            for (int i = 0; i < 2; i++)
            {
                _branchCards[i] = UiKit.Sliced("Branch", box, UiSprites.Button);
                UiKit.PlaceTopLeft(_branchCards[i].rectTransform, new Vector2(1024f + i * 252f, 58f), new Vector2(240f, 104f));
                _branchNames[i] = UiKit.Title("Name", _branchCards[i].transform, 19, p.Title, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(_branchNames[i].rectTransform, new Vector2(0f, 16f), new Vector2(240f, 30f));
                _branchTexts[i] = UiKit.Label("Text", _branchCards[i].transform, 14, p.Text, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(_branchTexts[i].rectTransform, new Vector2(0f, 52f), new Vector2(240f, 36f));
            }

            _resetButton = UiKit.Button("Reset", box, "초기화");
            UiKit.PlaceTopLeft(_resetButton.rectTransform, new Vector2(1024f, 230f), new Vector2(150f, 52f));

            _levelButton = UiKit.Button("LevelUp", box, string.Empty);
            UiKit.PlaceTopLeft(_levelButton.rectTransform, new Vector2(1186f, 230f), new Vector2(318f, 52f));
            _levelButtonText = _levelButton.GetComponentInChildren<Text>();
            _levelButtonText.fontSize = 17;
        }

        // ── 열기·닫기 ───────────────────────────────────────
        private void OnDestroy()
        {
            if (_caster != null)
            {
                _caster.Loadout.Changed -= OnLoadoutChanged;
                _caster.Loadout.GrowthChanged -= MarkDirty;
            }

            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }
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
            if (IsOpen || GameplayInput.IsBlocked || _caster == null)
            {
                return;
            }

            _dirty = true;
            _panel.gameObject.SetActive(true);
            GameplayInput.IsBlocked = true;
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CancelDrag();
            _panel.gameObject.SetActive(false);
            GameplayInput.IsBlocked = false;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.kKey.wasPressedThisFrame)
            {
                Toggle();
            }
            else if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (IsOpen && UiKit.IsCloseClicked(_close))
            {
                Close();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            _drag.Tick();
            _message.enabled = Time.unscaledTime < _messageEndTime;
            HandleInput();

            if (_dirty)
            {
                _dirty = false;
                Refresh();
            }
        }

        private void HandleInput()
        {
            for (int i = 0; i < _nodes.Count; i++)
            {
                bool hover = UiKit.IsHover(_nodes[i].Rect);
                _nodes[i].Tile.color = hover ? new Color(1f, 0.92f, 0.8f) : Color.white;

                if (hover && UiKit.Clicked && _selected != i)
                {
                    _selected = i;
                    _dirty = true;
                }
            }

            HandleListDrag();

            NodeView node = _nodes[_selected];
            SkillLoadout loadout = _caster.Loadout;
            bool canLevel = CanLevel(node);
            ButtonHover(_levelButton, canLevel);

            SkillSlot actual = loadout.MapSlot(node.Def.Slot);

            if (canLevel && UiKit.IsHover(_levelButton.rectTransform) && UiKit.Clicked && loadout.TryLevelUp(actual))
            {
                ShowMessage($"{node.Skill.DisplayName} 강화  ·  Lv {loadout.GetSlotLevel(actual)}");
            }

            ButtonHover(_resetButton, true);

            if (UiKit.IsHover(_resetButton.rectTransform) && UiKit.Clicked)
            {
                if (_caster.InCombat)
                {
                    ShowMessage("전투 중에는 초기화할 수 없습니다");
                }
                else
                {
                    loadout.ResetGrowth();
                    ShowMessage("기술 포인트를 모두 되돌렸습니다");
                }
            }

            if (node.Skill != null && SkillGrowth.HasBranch(node.Def.Slot))
            {
                bool open = loadout.GetSlotLevel(actual) >= SkillGrowth.BRANCH_LEVEL;

                for (int i = 0; i < 2; i++)
                {
                    if (!open || !UiKit.IsHover(_branchCards[i].rectTransform) || !UiKit.Clicked)
                    {
                        continue;
                    }

                    if (_caster.InCombat)
                    {
                        ShowMessage("전투 중에는 분기를 바꿀 수 없습니다");
                    }
                    else if (loadout.GetBranch(node.Skill) != i)
                    {
                        loadout.SetBranch(node.Skill, i);
                        ShowMessage($"강화 분기: {SkillGrowth.BRANCH_NAMES[i]}");
                    }
                }
            }
        }

        // 장착 슬롯 목록: 클릭 = 트리 노드 선택, 끌어 놓기 = 두 키의 기술 맞바꾸기 (좌클릭 제외)
        private void HandleListDrag()
        {
            Mouse mouse = Mouse.current;
            int hoverRow = -1;

            for (int i = 0; i < _rows.Count; i++)
            {
                ListRow row = _rows[i];
                bool hover = UiKit.IsHover(row.Background.rectTransform);
                bool target = _dragging && hover && i != _pressRow && row.Slot != SkillSlot.LeftClick;
                row.Background.color = UiTheme.WithAlpha(target ? UiTheme.P.Accent : UiTheme.P.Hover, target ? 0.35f : hover || i == _pressRow && _dragging ? 0.6f : 0f);

                if (hover)
                {
                    hoverRow = i;
                }

                bool swapHover = row.Swap.gameObject.activeSelf && UiKit.IsHover(row.Swap.rectTransform);
                row.Swap.color = swapHover ? new Color(1f, 0.92f, 0.8f) : Color.white;

                if (swapHover && UiKit.Clicked)
                {
                    Swap(row.Slot);
                    return;
                }
            }

            if (mouse == null)
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && hoverRow >= 0)
            {
                int index = FindNode(_rows[hoverRow].Slot);

                if (index >= 0 && index != _selected)
                {
                    _selected = index;
                    _dirty = true;
                }

                if (_rows[hoverRow].Slot != SkillSlot.LeftClick)
                {
                    _pressRow = hoverRow;
                    _pressPosition = UiKit.Pointer;
                }
            }

            if (_pressRow >= 0 && mouse.leftButton.isPressed && !_dragging &&
                Vector2.Distance(_pressPosition, UiKit.Pointer) > DRAG_THRESHOLD)
            {
                SkillDefinition skill = _caster.GetSkill(_rows[_pressRow].Slot);
                _dragging = true;
                _ghost.gameObject.SetActive(true);
                _ghost.SetAsLastSibling();
                _ghostIcon.sprite = SkillIcons.Get(skill);
                _ghostIcon.color = UiTheme.P.Icon;
                _ghostIcon.enabled = skill != null;
                _ghostText.text = skill != null ? skill.DisplayName : "빈 칸";
            }

            if (_dragging && RectTransformUtility.ScreenPointToLocalPointInRectangle(_panel, UiKit.Pointer, null, out Vector2 local))
            {
                _ghost.anchoredPosition = local;
            }

            if (_pressRow >= 0 && !mouse.leftButton.isPressed)
            {
                if (_dragging && hoverRow >= 0 && hoverRow != _pressRow && _rows[hoverRow].Slot != SkillSlot.LeftClick)
                {
                    SkillSlot from = _rows[_pressRow].Slot, to = _rows[hoverRow].Slot;

                    if (_caster.Loadout.SwapSlots(from, to, _caster.InCombat))
                    {
                        ShowMessage($"{LIST_KEYS[_pressRow]} ↔ {LIST_KEYS[hoverRow]} 키 순서 변경");
                    }
                    else if (_caster.InCombat)
                    {
                        ShowMessage("전투 중에는 키 순서를 바꿀 수 없습니다");
                    }
                }

                CancelDrag();
            }
        }

        private void CancelDrag()
        {
            _dragging = false;
            _pressRow = -1;

            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
        }

        private static void ButtonHover(Image button, bool enabled)
        {
            bool hover = enabled && UiKit.IsHover(button.rectTransform);
            button.color = !enabled ? new Color(1f, 1f, 1f, 0.45f) : hover ? new Color(1f, 0.92f, 0.8f) : Color.white;
        }

        private bool CanLevel(NodeView node)
        {
            return node.Skill != null && !node.Replaced && SkillGrowth.CanGrow(node.Def.Slot, node.Skill) &&
                _caster.Loadout.SkillPoints > 0 && _caster.Loadout.GetSlotLevel(_caster.Loadout.MapSlot(node.Def.Slot)) < SkillGrowth.MAX_LEVEL;
        }

        private void Swap(SkillSlot slot)
        {
            EquipResult result = _caster.EquipNext(slot);

            switch (result)
            {
                case EquipResult.Success:
                    ShowMessage($"{_caster.GetSkill(slot).DisplayName} 장착");
                    break;
                case EquipResult.InCombat:
                    ShowMessage("전투 중에는 교체할 수 없습니다");
                    break;
                default:
                    ShowMessage("바꿀 수 있는 해금 기술이 없습니다");
                    break;
            }
        }

        private int FindNode(SkillSlot actual)
        {
            SkillSlot slot = _caster.Loadout.DesignatedOf(actual);
            bool hero = _hero != null && _hero.HasPath;

            for (int i = 0; i < _nodes.Count; i++)
            {
                if (_nodes[i].Def.Slot == slot && (slot != SkillSlot.Key4 || _nodes[i].Def.Hero == hero))
                {
                    return i;
                }
            }

            return -1;
        }

        private static string KeyName(SkillSlot slot)
        {
            int index = System.Array.IndexOf(LIST_SLOTS, slot);
            return index >= 0 ? LIST_KEYS[index] : slot.ToString();
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _messageEndTime = Time.unscaledTime + 2f;
        }

        // ── 표시 갱신 ───────────────────────────────────────
        private void Refresh()
        {
            UiPalette p = UiTheme.P;
            SkillLoadout loadout = _caster.Loadout;
            bool hero = _hero != null && _hero.HasPath;
            AscensionTier tier = _progression != null ? _progression.Tier : AscensionTier.Mortal;

            _header.text = JobChain();
            _pointsText.text = $"남은 기술 포인트  {loadout.SkillPoints}";

            foreach (NodeView node in _nodes)
            {
                SkillSlot actual = loadout.MapSlot(node.Def.Slot);
                SkillDefinition equipped = loadout.GetEquipped(actual);
                node.Replaced = node.Def.Slot == SkillSlot.Key4 && !node.Def.Hero && hero;
                node.Skill = node.Def.Hero ? (hero ? equipped : null) : node.Replaced ? null : equipped;

                bool open = node.Skill != null;
                bool grows = open && SkillGrowth.CanGrow(node.Def.Slot, node.Skill);
                int level = loadout.GetSlotLevel(actual);
                bool selected = _nodes.IndexOf(node) == _selected;
                node.Key.text = actual == node.Def.Slot ? node.Def.Key : KeyName(actual);

                node.Icon.enabled = open;
                node.Icon.sprite = SkillIcons.Get(node.Skill);
                node.Icon.color = p.Icon;
                node.Tile.color = Color.white;
                node.Frame.color = selected ? new Color(1f, 0.89f, 0.66f)
                    : open ? p.Accent : UiTheme.WithAlpha(p.PlateStroke, 0.45f);
                node.Glow.enabled = selected || open;
                node.Glow.color = UiTheme.WithAlpha(p.Accent, selected ? 0.45f : 0.16f);
                node.Key.color = open ? p.Title : p.TextDisabled;
                node.Pill.color = open ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                node.Points.text = grows ? $"{level}/{SkillGrowth.MAX_LEVEL}" : open ? "-" : "0/15";
                node.Points.color = !open ? p.TextDisabled : level >= SkillGrowth.MAX_LEVEL ? p.Title : p.Text;
                node.Name.text = open ? node.Skill.DisplayName : node.Def.Name;
                node.Name.color = open ? p.Text : p.TextDisabled;

                if (!open)
                {
                    node.Name.text += node.Replaced ? "\n(영웅 기술로 교체)" : tier < node.Def.Tier ? $"\n({AscensionModel.DisplayName(node.Def.Tier)} 돌파)" : string.Empty;
                }
            }

            for (int i = 0; i < _nodes.Count - 1; i++)
            {
                bool lit = _nodes[i].Skill != null && _nodes[i + 1].Skill != null;

                for (int s = 0; s < 3; s++)
                {
                    _connectors[i * 3 + s].color = lit ? UiTheme.WithAlpha(p.Accent, 0.9f) : UiTheme.WithAlpha(p.PlateStroke, 0.35f);
                }
            }

            foreach (ListRow row in _rows)
            {
                SkillDefinition skill = _caster.GetSkill(row.Slot);
                row.Icon.enabled = skill != null || row.Slot == SkillSlot.LeftClick;
                row.Icon.sprite = SkillIcons.Get(skill);
                row.Icon.color = p.Icon;
                row.Name.text = skill != null ? skill.DisplayName : row.Slot == SkillSlot.LeftClick ? "기본 공격" : "미개방";
                row.Name.color = skill != null || row.Slot == SkillSlot.LeftClick ? p.Text : p.TextDisabled;
                row.Level.text = skill != null && SkillGrowth.CanGrow(row.Slot, skill) ? $"Lv {loadout.GetSlotLevel(row.Slot)}" : string.Empty;
                row.Swap.gameObject.SetActive(loadout.GetCandidates(row.Slot).Count > 1);
            }

            RefreshPanel();
        }

        private void RefreshPanel()
        {
            UiPalette p = UiTheme.P;
            SkillLoadout loadout = _caster.Loadout;
            NodeView node = _nodes[Mathf.Clamp(_selected, 0, _nodes.Count - 1)];
            SkillDefinition skill = node.Skill;
            SkillSlot slot = node.Def.Slot;
            SkillSlot actualSlot = loadout.MapSlot(slot);
            int level = loadout.GetSlotLevel(actualSlot);
            bool grows = skill != null && SkillGrowth.CanGrow(slot, skill);

            _bigIcon.enabled = skill != null;
            _bigIcon.sprite = SkillIcons.Get(skill);
            _bigIcon.color = p.Icon;
            _bigName.text = skill != null ? skill.DisplayName : node.Def.Name;
            _bigName.color = skill != null ? p.Title : p.TextDisabled;
            _bigSub.text = $"{(actualSlot == slot ? node.Def.Key : KeyName(actualSlot))} 키  ·  {AscensionModel.DisplayName(node.Def.Tier)}" +
                (skill == null ? (node.Replaced ? "  ·  영웅 기술로 교체됨" : "  ·  잠김") : string.Empty);

            _levelBar.SetRatio(grows ? (float)level / SkillGrowth.MAX_LEVEL : 0f);
            _levelText.text = grows ? $"Lv {level} / {SkillGrowth.MAX_LEVEL}" : "강화 없음";

            if (skill == null)
            {
                _description.text = node.Replaced ? "영웅의 길 기술이 이 키를 대신합니다."
                    : $"{AscensionModel.DisplayName(node.Def.Tier)} 돌파 후 사용할 수 있습니다.";
            }
            else
            {
                string growth = grows ? $"\n슬롯 레벨 1당 피해(효과량) +{SkillGrowth.PER_LEVEL * 100f:0}%  ·  기술을 바꿔도 슬롯 레벨 유지" : "\n이 기술은 레벨로 강해지지 않습니다.";
                _description.text = SkillWindowText.Describe(skill) + growth;
            }

            // 현재 → 다음
            int row = 0;
            _statHeader.text = grows && level < SkillGrowth.MAX_LEVEL ? $"현재 → 다음 레벨 (Lv {level + 1})" : "현재 수치";

            if (skill != null)
            {
                int branch = loadout.GetBranch(skill);
                SkillParameterSet now = SkillGrowth.Preview(skill, slot, level, branch);
                SkillParameterSet next = SkillGrowth.Preview(skill, slot, Mathf.Min(level + 1, SkillGrowth.MAX_LEVEL), branch);

                foreach ((string label, string key, bool percent) in SkillGrowth.DisplayKeys())
                {
                    if (row >= STAT_ROWS || !now.Has(key) || now.Get(key) <= 0f)
                    {
                        continue;
                    }

                    Text[] texts = _statRows[row++];
                    bool changes = grows && level < SkillGrowth.MAX_LEVEL && Mathf.Abs(next.Get(key) - now.Get(key)) > 0.0001f;
                    texts[0].text = label;
                    texts[1].text = Format(now.Get(key), percent, key);
                    texts[2].enabled = changes;
                    texts[3].text = changes ? Format(next.Get(key), percent, key) : string.Empty;
                    SetRow(texts, true);
                }
            }

            for (int i = row; i < STAT_ROWS; i++)
            {
                SetRow(_statRows[i], false);
            }

            // 강화 분기
            bool hasBranch = skill != null && SkillGrowth.HasBranch(slot);
            bool branchOpen = hasBranch && level >= SkillGrowth.BRANCH_LEVEL;
            int chosen = skill != null ? loadout.GetBranch(skill) : -1;
            _branchHeader.text = hasBranch ? $"강화 분기  ·  Lv {SkillGrowth.BRANCH_LEVEL}에 개방, 택 1 (비전투 시 변경)"
                : "강화 분기  ·  우클릭 기술만 (프로토타입)";

            for (int i = 0; i < 2; i++)
            {
                _branchCards[i].gameObject.SetActive(hasBranch);
                _branchCards[i].sprite = chosen == i ? UiSprites.ButtonEquip : UiSprites.Button;
                _branchCards[i].color = branchOpen ? Color.white : new Color(1f, 1f, 1f, 0.45f);
                _branchNames[i].text = $"{(char)('A' + i)}  {SkillGrowth.BRANCH_NAMES[i]}";
                _branchNames[i].color = chosen == i ? p.EquipText : branchOpen ? p.Title : p.TextDisabled;
                _branchTexts[i].text = SkillGrowth.BRANCH_DESCRIPTIONS[i] + (chosen == i ? "\n선택됨" : branchOpen ? "\n클릭해 선택" : $"\nLv {SkillGrowth.BRANCH_LEVEL} 필요");
                _branchTexts[i].color = branchOpen ? p.Text : p.TextDisabled;
            }

            _levelButtonText.text = grows && level >= SkillGrowth.MAX_LEVEL ? "최대 레벨"
                : $"강화 +1   (포인트 1 / 남은 {loadout.SkillPoints})";
        }

        private static string Format(float value, bool percent, string key)
        {
            if (key == SkillParameterKeys.RADIUS)
            {
                return $"{value:0.##}m";
            }

            if (key == SkillParameterKeys.DURATION)
            {
                return $"{value:0.#}초";
            }

            return percent ? $"{value * 100f:0.#}%" : $"{value:0.##}";
        }

        private static void SetRow(Text[] texts, bool on)
        {
            foreach (Text text in texts)
            {
                text.gameObject.SetActive(on);
            }
        }

        private string JobChain()
        {
            var names = new List<string>();

            for (JobDefinition job = _job != null ? _job.Current : null; job != null; job = job.ParentJob)
            {
                names.Insert(0, job.DisplayName);
            }

            if (_hero != null && _hero.HasPath)
            {
                names.Add(_hero.Current.DisplayName);
            }

            return names.Count > 0 ? string.Join("  ·  ", names) : "무직";
        }
    }

    /// <summary>기술 수치 요약 (재사용 대기·비용·피해·범위·지속). HUD 툴팁·기술 창 공용</summary>
    public static class SkillWindowText
    {
        public static string Describe(SkillDefinition skill)
        {
            var parts = new List<string>();

            if (skill.IsChanneled)
            {
                parts.Add("키 유지");
            }
            else if (skill.Cooldown > 0f)
            {
                parts.Add($"재사용 대기 {skill.Cooldown:0.#}초");
            }
            else
            {
                parts.Add("즉시 발동");
            }

            if (skill.ResourceCost > 0f)
            {
                parts.Add($"기력 {skill.ResourceCost:0}");
            }

            float coefficient = skill.GetBaseParameter(SkillParameterKeys.COEFFICIENT);

            if (coefficient > 0f)
            {
                parts.Add($"기본 피해 {coefficient * 100f:0}%");
            }

            float radius = skill.GetBaseParameter(SkillParameterKeys.RADIUS);

            if (radius > 0f)
            {
                parts.Add($"범위 {radius:0.#}m");
            }

            float duration = skill.GetBaseParameter(SkillParameterKeys.DURATION);

            if (duration > 0f)
            {
                parts.Add($"지속 {duration:0.#}초");
            }

            return string.Join("  ·  ", parts);
        }
    }
}
