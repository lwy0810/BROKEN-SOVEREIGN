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
        private const float NODE = 68f;
        private const float HEADER_H = 58f;
        /// <summary>열 머리글 아래 첫 기술 칸 위쪽 (트리 위 기준)</summary>
        private const float NODE_TOP = 98f;
        /// <summary>열 안 기술 간격 (칸 + 레벨 + 이름 두 줄)</summary>
        private const float NODE_STEP = 140f;
        /// <summary>칸 아래 레벨·이름이 끝나는 거리 (연결선 시작)</summary>
        private const float NAME_BOTTOM = 52f;
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
        /// <summary>격 6단계 열 (Figma 19-B). 신격·초월·태초는 아직 기술이 없어 자리만</summary>
        private static readonly string[] TIER_COLUMNS = { "범인", "초인", "반신", "신격", "초월", "태초" };
        private static readonly string[] TIER_JOBS = { "성기사 · 공용", "성투사", "영웅의 길" };

        private sealed class NodeDef
        {
            public SkillSlot Slot;
            public string Key;
            public int Column;
            public AscensionTier Tier;
            public string Name;
            public bool Hero;
            /// <summary>이 영웅만 가진 기술 (다른 영웅을 고르면 칸을 숨김). 비우면 모든 영웅</summary>
            public string HeroId;
        }

        private static readonly NodeDef[] NODES =
        {
            new NodeDef { Slot = SkillSlot.RightClick, Key = "RMB", Column = 0, Tier = AscensionTier.Mortal, Name = "강타" },
            new NodeDef { Slot = SkillSlot.Key1, Key = "1", Column = 1, Tier = AscensionTier.Mortal, Name = "격광 강타" },
            new NodeDef { Slot = SkillSlot.Key2, Key = "2", Column = 2, Tier = AscensionTier.Mortal, Name = "진격" },
            new NodeDef { Slot = SkillSlot.Key3, Key = "3", Column = 3, Tier = AscensionTier.Superhuman, Name = "불굴의 갑주" },
            new NodeDef { Slot = SkillSlot.Key4, Key = "4", Column = 4, Tier = AscensionTier.Superhuman, Name = "전장의 함성" },
            // 10/26: 영웅 기술은 5·6번 (초인까지 공용 1~4번 유지, 전장의 함성이 영웅 기술로 바뀌지 않음)
            new NodeDef { Slot = SkillSlot.Key5, Key = "5", Column = 5, Tier = AscensionTier.Demigod, Name = "분쇄 일격 · 난천무", Hero = true },
            new NodeDef { Slot = SkillSlot.Key6, Key = "6", Column = 6, Tier = AscensionTier.Demigod, Name = "모드변환", Hero = true, HeroId = "seokcheon" }
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
            public Image LevelFlash;
            public float FlashTime = float.NegativeInfinity;
        }

        private const float LEVEL_FLASH_TIME = 0.5f;

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
        // 열 머리글 (미도달 격은 ???로 가림, 10/26)
        private readonly List<(Text tier, Text job, Image header)> _tierHeaders = new List<(Text, Text, Image)>();
        private const string HIDDEN = "???";
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
            BuildResetConfirm();

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

        // ── 트리 (10/26 Figma 19-B: 격 6단계 열 배치) ─────────────
        // 왼→오 = 범인·초인·반신·신격·초월·태초. 단계마다 같은 폭의 열, 기술은 열 안에서 위→아래 (최대 3개).
        // 아직 없는 단계(신격·초월·태초)는 머리글을 흐리게 + 빈 칸으로 자리만 잡아 둠 (나중에 NODES에 추가하면 그대로 들어감)
        private void BuildTree()
        {
            UiPalette p = UiTheme.P;
            Image frame = UiKit.Box("Tree", _panel, UiTheme.WithAlpha(p.Inset, 0.55f));
            UiKit.PlaceTopLeft(frame.rectTransform, new Vector2(TREE_X, TREE_Y), new Vector2(TREE_W, TREE_H));
            float columnWidth = TREE_W / TIER_COLUMNS.Length;

            for (int c = 0; c < TIER_COLUMNS.Length; c++)
            {
                float x = TREE_X + c * columnWidth;
                bool future = c >= TIER_JOBS.Length;

                if (c > 0)
                {
                    Image line = UiKit.Box("ColumnLine", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.3f));
                    UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, TREE_Y + 12f), new Vector2(1f, TREE_H - 24f));
                }

                Image header = UiKit.Round("TierHeader", _panel, UiTheme.WithAlpha(future ? p.PlateStroke : p.Accent, future ? 0.1f : 0.14f));
                UiKit.PlaceTopLeft(header.rectTransform, new Vector2(x + 10f, TREE_Y + 12f), new Vector2(columnWidth - 20f, HEADER_H));

                Text tier = UiKit.Title("Tier", _panel, 22, future ? UiTheme.WithAlpha(p.TextDisabled, 0.8f) : p.Title, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(tier.rectTransform, new Vector2(x, TREE_Y + 14f), new Vector2(columnWidth, 30f));
                tier.text = TIER_COLUMNS[c];
                _tierHeaders.Add((tier, null, header));
                // 보조 글: 이름 아래 (10px 여백 규칙에 맞춰 이름과 겹치지 않게)
                Text job = UiKit.Label("Job", _panel, 12, future ? UiTheme.WithAlpha(p.TextDisabled, 0.8f) : p.TextDim, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(job.rectTransform, new Vector2(x, TREE_Y + 46f), new Vector2(columnWidth, 18f));
                job.text = future ? string.Empty : TIER_JOBS[c];
                _tierHeaders[c] = (tier, job, header);

                if (c < TIER_COLUMNS.Length - 1)
                {
                    Text arrow = UiKit.Label("Arrow", _panel, 24, c < TIER_JOBS.Length - 1 ? p.Accent : UiTheme.WithAlpha(p.TextDisabled, 0.7f),
                        TextAnchor.MiddleCenter, FontStyle.Bold);
                    UiKit.PlaceTopLeft(arrow.rectTransform, new Vector2(x + columnWidth - 12f, TREE_Y + 22f), new Vector2(24f, 30f));
                    arrow.text = "›";
                }

                // 아직 없는 단계: 빈 칸 3개 (자리만)
                if (future)
                {
                    for (int k = 0; k < 3; k++)
                    {
                        float cy = NodeCenterY(k);
                        Image slot = UiKit.Round("FutureSlot", _panel, UiTheme.WithAlpha(p.Inset, 0.4f));
                        UiKit.PlaceTopLeft(slot.rectTransform, new Vector2(x + columnWidth * 0.5f - NODE * 0.5f, cy - NODE * 0.5f), new Vector2(NODE, NODE));
                        slot.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.PlateStroke, 0.35f);
                        Image bar = UiKit.Round("FutureBar", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.18f));
                        UiKit.PlaceTopLeft(bar.rectTransform, new Vector2(x + columnWidth * 0.5f - 34f, cy + NODE * 0.5f + 12f), new Vector2(68f, 10f));
                    }
                }
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
                _nodes.Add(CreateNode(def));
            }

            // 같은 단계(열) 안에서만 위→아래로 연결. 단계 사이는 머리글의 › 로 표시
            for (int i = 0; i < NODES.Length - 1; i++)
            {
                Vector2 a = _nodes[i].Center, b = _nodes[i + 1].Center;
                bool sameTier = NODES[i].Tier == NODES[i + 1].Tier;
                _connectors[i * 3].gameObject.SetActive(sameTier);
                _connectors[i * 3 + 1].gameObject.SetActive(false);
                _connectors[i * 3 + 2].gameObject.SetActive(false);

                if (sameTier)
                {
                    PlaceLine(_connectors[i * 3], new Vector2(a.x, a.y + NODE * 0.5f + NAME_BOTTOM), new Vector2(b.x, b.y - NODE * 0.5f));
                }
            }
        }

        // 열 안 k번째 기술의 중심 y
        private static float NodeCenterY(int k)
        {
            return TREE_Y + NODE_TOP + k * NODE_STEP + NODE * 0.5f;
        }

        // 같은 단계에서 몇 번째 기술인지 (NODES 순서)
        private static int IndexInTier(NodeDef def)
        {
            int index = 0;

            foreach (NodeDef other in NODES)
            {
                if (other == def)
                {
                    return index;
                }

                if (other.Tier == def.Tier)
                {
                    index++;
                }
            }

            return index;
        }

        private static void PlaceLine(Image line, Vector2 from, Vector2 to)
        {
            Vector2 min = Vector2.Min(from, to), max = Vector2.Max(from, to);
            UiKit.PlaceTopLeft(line.rectTransform, min - new Vector2(1.5f, 1.5f), max - min + new Vector2(3f, 3f));
        }

        private NodeView CreateNode(NodeDef def)
        {
            UiPalette p = UiTheme.P;
            float columnWidth = TREE_W / TIER_COLUMNS.Length;
            float cx = TREE_X + ((int)def.Tier + 0.5f) * columnWidth;
            float cy = NodeCenterY(IndexInTier(def));
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

            // 강화 반짝임 (10/26): 테두리 위에 금빛 테두리를 잠깐 겹침
            node.LevelFlash = UiKit.Sliced("LevelFlash", node.Rect, ItemVisuals.Glow);
            HudGraphics.Stretch(node.LevelFlash.rectTransform);
            node.LevelFlash.color = Color.clear;
            node.LevelFlash.gameObject.AddComponent<Outline>().effectColor = Color.clear;

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
            UiKit.PlaceTopLeft(node.Name.rectTransform, new Vector2(cx - columnWidth * 0.5f, cy + NODE * 0.5f + 32f), new Vector2(columnWidth, 20f));
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
            UiKit.PlaceTopLeft(_bigSub.rectTransform, new Vector2(156f, 65f), new Vector2(440f, 22f));   // 10/26: 이름과 간격 +5

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
            _resetConfirm?.SetActive(false);
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
                // 초기화 확인 팝업이 떠 있으면 팝업만 닫음
                if (_resetConfirm != null && _resetConfirm.activeSelf)
                {
                    _resetConfirm.SetActive(false);
                    return;
                }

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
            // 초기화 확인 팝업이 떠 있으면 팝업만 처리
            if (_resetConfirm != null && _resetConfirm.activeSelf)
            {
                HandleResetConfirm();
                return;
            }

            for (int i = 0; i < _nodes.Count; i++)
            {
                bool hover = UiKit.IsHover(_nodes[i].Rect);
                _nodes[i].Tile.color = hover ? new Color(1f, 0.92f, 0.8f) : Color.white;

                if (hover && UiKit.Clicked && _selected != i && !IsHiddenTier(_nodes[i].Def.Tier))
                {
                    _selected = i;
                    _dirty = true;
                }

                // 10/26: 우클릭 = 그 기술 1포인트 강화
                if (hover && Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame && !IsHiddenTier(_nodes[i].Def.Tier))
                {
                    if (_selected != i)
                    {
                        _selected = i;
                        _dirty = true;
                    }

                    TryLevel(_nodes[i]);
                }
            }

            AnimateLevelFlash();

            HandleListDrag();

            NodeView node = _nodes[_selected];
            SkillLoadout loadout = _caster.Loadout;
            bool canLevel = CanLevel(node);
            ButtonHover(_levelButton, canLevel);

            SkillSlot actual = loadout.MapSlot(node.Def.Slot);

            if (canLevel && UiKit.IsHover(_levelButton.rectTransform) && UiKit.Clicked)
            {
                TryLevel(node);
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
                    // 10/26: 확인 팝업 (추후 초기화에 자원 소모 예정)
                    _resetConfirm.SetActive(true);
                    _resetConfirm.transform.SetAsLastSibling();
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

        // ── 초기화 확인 팝업 (10/26) ─────────────────────────
        private GameObject _resetConfirm;
        private Image _resetYes;
        private Image _resetNo;

        private void BuildResetConfirm()
        {
            UiPalette p = UiTheme.P;
            Image cover = UiKit.Box("ResetConfirm", _panel, new Color(0f, 0f, 0f, 0.6f));
            HudGraphics.Stretch(cover.rectTransform);
            _resetConfirm = cover.gameObject;

            RectTransform box = UiKit.Plate("Box", cover.transform).rectTransform;
            UiKit.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 200f));
            box.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.8f);

            Text message = UiKit.Label("Message", box, 20, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(message.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(420f, 40f));
            message.text = "스킬을 초기화 하시겠습니까?";
            // 보조 글: 문구와 10px 띄움
            Text sub = UiKit.Label("Sub", box, 14, p.TextDim, TextAnchor.UpperCenter);
            UiKit.Place(sub.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -86f), new Vector2(420f, 22f));
            sub.text = "사용한 기술 포인트를 모두 되돌립니다.";

            _resetYes = UiKit.Button("Yes", box, "초기화 (Enter)", 16);
            UiKit.Place(_resetYes.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 24f), new Vector2(160f, 44f));
            _resetNo = UiKit.Button("No", box, "취소 (Esc)", 16);
            UiKit.Place(_resetNo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(8f, 24f), new Vector2(160f, 44f));
            _resetConfirm.SetActive(false);
        }

        private void HandleResetConfirm()
        {
            Keyboard keyboard = Keyboard.current;
            ButtonHover(_resetYes, true);
            ButtonHover(_resetNo, true);
            bool yes = (UiKit.IsHover(_resetYes.rectTransform) && UiKit.Clicked) || (keyboard != null && keyboard.enterKey.wasPressedThisFrame);
            bool no = UiKit.IsHover(_resetNo.rectTransform) && UiKit.Clicked;

            if (yes)
            {
                _resetConfirm.SetActive(false);

                if (_caster.InCombat)
                {
                    ShowMessage("전투 중에는 초기화할 수 없습니다");
                }
                else
                {
                    _caster.Loadout.ResetGrowth();
                    ShowMessage("기술 포인트를 모두 되돌렸습니다");
                }
            }
            else if (no)
            {
                _resetConfirm.SetActive(false);
            }
        }

        private static void ButtonHover(Image button, bool enabled)
        {
            bool hover = enabled && UiKit.IsHover(button.rectTransform);
            button.color = !enabled ? new Color(1f, 1f, 1f, 0.45f) : hover ? new Color(1f, 0.92f, 0.8f) : Color.white;
        }

        // 강화 1회: 성공하면 안내 + 테두리 반짝임, 못 하면 이유 안내
        private void TryLevel(NodeView node)
        {
            SkillLoadout loadout = _caster.Loadout;
            SkillSlot actual = loadout.MapSlot(node.Def.Slot);

            // 전환 기술(모드변환): 포인트 1로 습득 (최대 1)
            if (node.Skill != null && SkillLoadout.RequiresLearning(node.Skill))
            {
                if (loadout.TryLearn(node.Skill))
                {
                    node.FlashTime = Time.unscaledTime;
                    ShowMessage($"{node.Skill.DisplayName} 습득");
                    _dirty = true;
                }
                else
                {
                    ShowMessage(loadout.IsLearned(node.Skill) ? "이미 습득했습니다 (최대 1포인트)" : "기술 포인트가 부족합니다");
                }

                return;
            }

            if (CanLevel(node) && loadout.TryLevelUp(actual))
            {
                node.FlashTime = Time.unscaledTime;
                ShowMessage($"{node.Skill.DisplayName} 강화  ·  Lv {loadout.GetSlotLevel(actual)}");
                _dirty = true;
                return;
            }

            if (node.Skill == null || node.Replaced)
            {
                ShowMessage("아직 사용할 수 없는 기술입니다");
            }
            else if (!SkillGrowth.CanGrow(node.Def.Slot, node.Skill))
            {
                ShowMessage("강화할 수 없는 기술입니다");
            }
            else if (loadout.GetSlotLevel(actual) >= SkillGrowth.MAX_LEVEL)
            {
                ShowMessage("최대 레벨입니다");
            }
            else
            {
                ShowMessage("기술 포인트가 부족합니다");
            }
        }

        // 강화 반짝임: 빠르게 밝아졌다가 사라짐 + 칸이 살짝 커졌다 돌아옴
        private void AnimateLevelFlash()
        {
            Color gold = UiTheme.P.Title;

            foreach (NodeView node in _nodes)
            {
                float t = (Time.unscaledTime - node.FlashTime) / LEVEL_FLASH_TIME;

                if (t < 0f || t > 1f)
                {
                    if (node.LevelFlash.color.a > 0f)
                    {
                        node.LevelFlash.color = Color.clear;
                        node.LevelFlash.GetComponent<Outline>().effectColor = Color.clear;
                        node.Rect.localScale = Vector3.one;
                    }

                    continue;
                }

                float alpha = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
                node.LevelFlash.color = new Color(gold.r, gold.g, gold.b, alpha * 0.9f);
                node.LevelFlash.GetComponent<Outline>().effectColor = new Color(1f, 0.95f, 0.8f, alpha);
                node.Rect.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(t * Mathf.PI));
            }
        }

        private bool CanLevel(NodeView node)
        {
            if (node.Skill != null && SkillLoadout.RequiresLearning(node.Skill))
            {
                return !_caster.Loadout.IsLearned(node.Skill) && _caster.Loadout.SkillPoints > 0;
            }

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
                node.Replaced = false;
                node.Skill = node.Def.Hero ? (hero ? equipped : null) : equipped;

                // 다른 영웅 전용 기술 칸은 숨김 (야로미르: 모드변환 없음)
                bool otherHero = hero && !string.IsNullOrEmpty(node.Def.HeroId) && _hero.Current.HeroId != node.Def.HeroId;
                SetNodeVisible(node, !otherHero);

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
                bool toggle = open && SkillLoadout.RequiresLearning(node.Skill);
                node.Points.text = toggle ? (loadout.IsLearned(node.Skill) ? "1/1" : "0/1")
                    : grows ? $"{level}/{SkillGrowth.MAX_LEVEL}" : open ? "-" : "0/15";
                node.Points.color = !open ? p.TextDisabled : level >= SkillGrowth.MAX_LEVEL ? p.Title : p.Text;
                node.Name.text = open ? node.Skill.DisplayName : node.Def.Name;
                node.Name.color = open ? p.Text : p.TextDisabled;

                if (!open)
                {
                    node.Name.text += node.Replaced ? "\n(영웅 기술로 교체)" : string.Empty;
                }

                // 10/26: 아직 도달하지 않은 격의 기술은 이름·키·레벨을 숨김
                if (IsHiddenTier(node.Def.Tier))
                {
                    node.Name.text = HIDDEN;
                    node.Key.text = string.Empty;
                    node.Points.text = string.Empty;
                    node.Icon.enabled = false;
                    node.Glow.enabled = false;
                }
            }

            // 열 머리글: 도달한 격만 이름·직업 표시, 나머지는 ???
            for (int c = 0; c < _tierHeaders.Count; c++)
            {
                bool hidden = c > (int)tier;
                (Text tierText, Text job, Image header) = _tierHeaders[c];
                tierText.text = hidden ? HIDDEN : TIER_COLUMNS[c];
                tierText.color = hidden ? UiTheme.WithAlpha(p.TextDisabled, 0.8f) : p.Title;
                job.text = hidden || c >= TIER_JOBS.Length ? string.Empty : TIER_JOBS[c];
                header.color = UiTheme.WithAlpha(hidden ? p.PlateStroke : p.Accent, hidden ? 0.1f : 0.14f);
            }

            for (int i = 0; i < _nodes.Count - 1; i++)
            {
                bool lit = _nodes[i].Skill != null && _nodes[i + 1].Skill != null;
                bool hiddenLink = !_nodes[i].Rect.gameObject.activeSelf || !_nodes[i + 1].Rect.gameObject.activeSelf;

                for (int s = 0; s < 3; s++)
                {
                    _connectors[i * 3 + s].color = hiddenLink ? Color.clear
                        : lit ? UiTheme.WithAlpha(p.Accent, 0.9f) : UiTheme.WithAlpha(p.PlateStroke, 0.35f);
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

        private static void SetNodeVisible(NodeView node, bool visible)
        {
            if (node.Rect.gameObject.activeSelf == visible)
            {
                return;
            }

            node.Rect.gameObject.SetActive(visible);
            node.Pill.gameObject.SetActive(visible);
            node.Name.gameObject.SetActive(visible);
            node.Glow.gameObject.SetActive(visible);
        }

        // 아직 도달하지 않은 격 (기술 창에서 정보를 숨김)
        private bool IsHiddenTier(AscensionTier nodeTier)
        {
            AscensionTier current = _progression != null ? _progression.Tier : AscensionTier.Mortal;
            return nodeTier > current;
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

            bool toggleSkill = SkillLoadout.RequiresLearning(skill);
            bool learned = loadout.IsLearned(skill);
            _levelBar.SetRatio(toggleSkill ? (learned ? 1f : 0f) : grows ? (float)level / SkillGrowth.MAX_LEVEL : 0f);
            _levelText.text = toggleSkill ? (learned ? "Lv 1 / 1" : "Lv 0 / 1") : grows ? $"Lv {level} / {SkillGrowth.MAX_LEVEL}" : "강화 없음";

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
                    // 단일 대상 기술(강타)은 범위가 아니라 사거리 (10/26)
                    texts[0].text = key == SkillParameterKeys.RADIUS && skill.Behaviour is SingleStrikeBehaviour ? "사거리" : label;
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
                _branchNames[i].text = $"{(char)('A' + i)}. {SkillGrowth.BRANCH_NAMES[i]}";
                _branchNames[i].color = chosen == i ? p.EquipText : branchOpen ? p.Title : p.TextDisabled;
                // 10/26: 설명 / (보충 · 상태) 두 단락
                string state = chosen == i ? "선택됨" : branchOpen ? "클릭해 선택" : $"Lv {SkillGrowth.BRANCH_LEVEL} 필요";
                string note = SkillGrowth.BRANCH_NOTES[i];
                _branchTexts[i].text = SkillGrowth.BRANCH_DESCRIPTIONS[i] + $"\n({(note.Length > 0 ? note + " " : string.Empty)}{state})";
                _branchTexts[i].color = branchOpen ? p.Text : p.TextDisabled;
            }

            _levelButtonText.text = toggleSkill ? (learned ? "습득 완료" : $"습득   (포인트 1 / 남은 {loadout.SkillPoints})")
                : grows && level >= SkillGrowth.MAX_LEVEL ? "최대 레벨"
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

            if (skill.Behaviour is SingleStrikeBehaviour)
            {
                parts.Add(radius > 0f ? $"단일 대상  ·  사거리 {radius:0.#}m" : "단일 대상");   // 10/26 강타
            }
            else if (radius > 0f)
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
