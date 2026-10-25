using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using BrokenSovereign.Item;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 캐릭터 창 (C, 10/21 Figma 10-A/B/C "캐릭터+성물 통합"). 장착 모습(모델)은 인벤토리 창으로 옮기고 성물 창을 합쳤습니다.
    /// [정보] 탭: 왼쪽 성장 정보(레벨·경험치·신앙·직업·누적 처치·플레이 시간) / 가운데 영웅 카드 + 성물 카드
    ///   (반신 전: 봉인된 성물 + 반신까지 진행도·실제 돌파 조건 / 반신 후: 금색 성물 카드·고유 메커니즘·성물 효과·[성물 자세히])
    ///   / 오른쪽 능력치(핵심 4종 고정 + 5분류 휠 스크롤).
    /// [성물] 탭(반신에 열림, 새로 열리면 알림 점): 성물 그림·일화 / 단계 트랙 1~5 / 고유 메커니즘 흐름·현재 상태.
    /// 격 단계·권역은 표시하지 않음 (격은 별도 창 예정). 제목 탭 끌기 이동, ×·C·Esc 닫기. HudView가 생성합니다.
    /// </summary>
    public class CharacterWindowView : MonoBehaviour
    {
        private const float WIDTH = 1124f;
        private const float HEIGHT = 720f;
        private const float PANEL_Y = 100f;
        private const float PANEL_H = 596f;
        private const float LEFT_X = 24f, LEFT_W = 300f;
        private const float MID_X = 340f, MID_W = 444f;
        private const float RIGHT_X = 800f, RIGHT_W = 300f;
        private static readonly Color PURPLE = new Color(0.71f, 0.55f, 1f);
        private static readonly Color RED = new Color(0.85f, 0.3f, 0.25f);
        private static readonly Color GREEN = new Color(0.37f, 0.7f, 0.42f);

        private RectTransform _panel;
        private Image _close;
        private WindowDrag _drag;
        private Text _message;
        private float _messageEndTime;

        // 탭
        private Image _tabInfo;
        private Image _tabRelic;
        private Text _tabInfoText;
        private Text _tabRelicText;
        private Text _relicLockNote;
        private Image _relicDot;
        private RectTransform _infoPage;
        private RectTransform _relicPage;
        private bool _relicTab;

        // 성장 정보
        private Text _level;
        private UiBar _expBar;
        private Text _expText;
        private UiBar _faithBar;
        private Text _faithLabel;
        private Text _faithText;
        private Text _job1;
        private Text _job2;
        private Text _kills;
        private Text _playTime;

        // 영웅 카드
        private Image _portraitIcon;
        private Text _heroName;
        private Text _heroTitle;
        private Text _heroJobs;
        private Image _badge;
        private Text _badgeText;
        private Text _heroMechanic;

        // 성물 카드
        private Image _relicCard;
        private Image _relicCardFrame;
        private Image _cardIcon;
        private RectTransform _lockedCard;
        private RectTransform _openCard;
        private Text _lockedTitle;
        private Text _lockedLine;
        private Text _lockedDesc;
        private Text _progressText;
        private UiBar _progressBar;
        private readonly List<Text[]> _conditions = new List<Text[]>();
        private readonly List<Image> _conditionMarks = new List<Image>();
        private Text _openName;
        private Text _openStage;
        private Text _openTagline;
        private Text _openMechTitle;
        private Text _openMechDesc;
        private Text _openEffect1;
        private Text _openEffect2;
        private Image _detailButton;

        // 능력치
        private readonly List<(StatType Stat, Text Value)> _core = new List<(StatType, Text)>();
        private StatListView _stats;

        // 성물 탭
        private Image _artIcon;
        private Text _relicName;
        private Text _relicSub;
        private Text _relicLore;
        private Text _mechHeader;
        private Text _mechSummary;
        private readonly Text[] _flowTitles = new Text[3];
        private readonly Text[] _flowTexts = new Text[3];
        private Text _stateLabel;
        private Text _stateValue;
        private UiBar _stateBar;
        private Text _stateNote;
        private readonly Text[] _stageEffect = new Text[1];

        private GameObject _player;
        private PlayerProgression _progression;
        private PlayerJob _job;
        private HeroPathController _hero;
        private CharacterStats _characterStats;
        private Health _health;
        private PlayerRecord _record;
        private PlayerController _controller;
        private readonly List<IJobGauge> _gauges = new List<IJobGauge>();

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        private HeroRelic Relic => _player != null ? _player.GetComponent<HeroRelic>() : null;
        private bool RelicOpen => Relic is HeroRelic relic && relic.Info != null && relic.Stage > 0;

        public void Initialize(GameObject player, RectTransform hudRoot)
        {
            _player = player;
            _progression = player.GetComponent<PlayerProgression>();
            _job = player.GetComponent<PlayerJob>();
            _hero = player.GetComponent<HeroPathController>();
            _characterStats = player.GetComponent<CharacterStats>();
            _health = player.GetComponent<Health>();
            _record = player.GetComponent<PlayerRecord>();
            _controller = player.GetComponent<PlayerController>();

            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(WIDTH, HEIGHT));
            _drag = new WindowDrag(_panel, (RectTransform)UiKit.TitleTab(_panel, "캐릭터").transform.parent);
            _close = UiKit.CloseButton(_panel);

            BuildTabs();
            _infoPage = Page("InfoPage");
            _relicPage = Page("RelicPage");
            BuildGrowth();
            BuildHero();
            BuildStats();
            BuildRelicPage();

            _message = UiKit.Label("Message", _panel, 15, UiTheme.P.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.PlaceTopLeft(_message.rectTransform, new Vector2(0f, HEIGHT - 22f), new Vector2(WIDTH, 20f));

            _panel.gameObject.SetActive(false);
        }

        // ── 공용 그리기 ─────────────────────────────────────
        private RectTransform Page(string name)
        {
            RectTransform page = HudGraphics.CreateRect(name, _panel);
            HudGraphics.Stretch(page);
            return page;
        }

        private static RectTransform Box(string name, Transform parent, Vector2 position, Vector2 size, Color border, float fill = 0.5f)
        {
            Image background = UiKit.Box(name, parent, UiTheme.WithAlpha(UiTheme.P.Inset, fill));
            UiKit.PlaceTopLeft(background.rectTransform, position, size);
            Image frame = UiKit.Sliced("Frame", background.transform, ItemVisuals.Frame);
            HudGraphics.Stretch(frame.rectTransform);
            frame.color = border;
            return background.rectTransform;
        }

        private static Text Header(Transform parent, string title, float x, float y, float width)
        {
            Text text = UiKit.Title("Header", parent, 17, UiTheme.P.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(text.rectTransform, new Vector2(x, y), new Vector2(width, 26f));
            text.text = title;
            Image line = UiKit.Box("Line", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.45f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, y + 28f), new Vector2(width, 1f));
            return text;
        }

        private static Text Label(Transform parent, string text, int size, Color color, Vector2 position, Vector2 box,
            TextAnchor anchor = TextAnchor.MiddleLeft, bool bold = false)
        {
            Text label = UiKit.Label("Text", parent, size, color, anchor, bold ? FontStyle.Bold : FontStyle.Normal);
            UiKit.PlaceTopLeft(label.rectTransform, position, box);
            label.text = text;
            return label;
        }

        private static void Divider(Transform parent, float x, float y, float width)
        {
            Image line = UiKit.Box("Divider", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.35f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, y), new Vector2(width, 1f));
        }

        private static Image Glyph(Transform parent, Sprite sprite, Vector2 position, Vector2 size, Color color)
        {
            Image image = HudGraphics.CreateRect("Glyph", parent).gameObject.AddComponent<Image>();
            image.raycastTarget = false;
            image.preserveAspect = true;
            image.sprite = sprite;
            image.color = color;
            UiKit.PlaceTopLeft(image.rectTransform, position, size);
            return image;
        }

        // ── 탭 ─────────────────────────────────────────────
        private void BuildTabs()
        {
            UiPalette p = UiTheme.P;
            _tabInfo = UiKit.Sliced("TabInfo", _panel, UiSprites.Button);
            UiKit.PlaceTopLeft(_tabInfo.rectTransform, new Vector2(24f, 40f), new Vector2(120f, 32f));
            _tabInfoText = Label(_tabInfo.transform, "정보", 14, p.Text, Vector2.zero, new Vector2(120f, 32f), TextAnchor.MiddleCenter, true);

            _tabRelic = UiKit.Sliced("TabRelic", _panel, UiSprites.Button);
            UiKit.PlaceTopLeft(_tabRelic.rectTransform, new Vector2(152f, 40f), new Vector2(120f, 32f));
            _tabRelicText = Label(_tabRelic.transform, "성물", 14, p.Text, Vector2.zero, new Vector2(120f, 32f), TextAnchor.MiddleCenter, true);
            _relicDot = Glyph(_tabRelic.transform, UiSprites.Pill, new Vector2(108f, 2f), new Vector2(9f, 9f), RED);
            _relicLockNote = Label(_panel, "반신에 열림", 12, PURPLE, new Vector2(282f, 40f), new Vector2(160f, 32f));

            Divider(_panel, 24f, 84f, WIDTH - 48f);
        }

        // ── [정보] 왼쪽: 성장 정보 ──────────────────────────
        private void BuildGrowth()
        {
            UiPalette p = UiTheme.P;
            RectTransform box = Box("Growth", _infoPage, new Vector2(LEFT_X, PANEL_Y), new Vector2(LEFT_W, PANEL_H),
                UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            float x = 16f, w = LEFT_W - 32f;
            Header(box, "성장 정보", x, 10f, w);

            Label(box, "레벨", 13, p.TextDim, new Vector2(x, 50f), new Vector2(120f, 24f));
            _level = UiKit.Title("Level", box, 22, p.Text, TextAnchor.MiddleRight);
            UiKit.PlaceTopLeft(_level.rectTransform, new Vector2(x + w - 120f, 46f), new Vector2(120f, 30f));

            Label(box, "경험치", 12, p.TextDim, new Vector2(x, 86f), new Vector2(120f, 18f));
            _expText = Label(box, string.Empty, 12, p.Text, new Vector2(x + w - 140f, 86f), new Vector2(140f, 18f), TextAnchor.MiddleRight, true);
            _expBar = UiKit.Bar("Exp", box, p.Accent, p.Accent * 0.5f);
            UiKit.PlaceTopLeft(_expBar.Rect, new Vector2(x, 108f), new Vector2(w, 8f));
            Divider(box, x, 130f, w);

            _faithLabel = Label(box, "신앙", 13, p.TextDim, new Vector2(x, 142f), new Vector2(120f, 20f));
            _faithText = Label(box, string.Empty, 12, p.Text, new Vector2(x + w - 160f, 142f), new Vector2(160f, 20f), TextAnchor.MiddleRight, true);
            _faithBar = UiKit.Bar("Faith", box, p.Resource, p.ResourceDark);
            UiKit.PlaceTopLeft(_faithBar.Rect, new Vector2(x, 166f), new Vector2(w, 8f));
            Divider(box, x, 190f, w);

            Label(box, "1차 직업", 13, p.TextDim, new Vector2(x, 202f), new Vector2(120f, 24f));
            _job1 = Label(box, string.Empty, 13, p.Text, new Vector2(x + w - 160f, 202f), new Vector2(160f, 24f), TextAnchor.MiddleRight, true);
            Label(box, "2차 직업", 13, p.TextDim, new Vector2(x, 232f), new Vector2(120f, 24f));
            _job2 = Label(box, string.Empty, 13, p.Text, new Vector2(x + w - 160f, 232f), new Vector2(160f, 24f), TextAnchor.MiddleRight, true);
            Divider(box, x, 266f, w);

            Label(box, "누적 처치", 13, p.TextDim, new Vector2(x, 278f), new Vector2(120f, 24f));
            _kills = Label(box, string.Empty, 13, p.Text, new Vector2(x + w - 160f, 278f), new Vector2(160f, 24f), TextAnchor.MiddleRight, true);
            Label(box, "플레이 시간", 13, p.TextDim, new Vector2(x, 308f), new Vector2(120f, 24f));
            _playTime = Label(box, string.Empty, 13, p.Text, new Vector2(x + w - 160f, 308f), new Vector2(160f, 24f), TextAnchor.MiddleRight, true);
        }

        // ── [정보] 가운데: 영웅 + 성물 카드 ─────────────────
        private void BuildHero()
        {
            UiPalette p = UiTheme.P;
            RectTransform mid = HudGraphics.CreateRect("Hero", _infoPage);
            UiKit.PlaceTopLeft(mid, new Vector2(MID_X, PANEL_Y), new Vector2(MID_W, PANEL_H));
            Header(mid, "영웅", 0f, 10f, MID_W);

            RectTransform portrait = Box("Portrait", mid, new Vector2(0f, 46f), new Vector2(150f, 190f), UiTheme.WithAlpha(p.PlateStroke, 0.6f), 0.8f);
            _portraitIcon = Glyph(portrait, CharacterGlyphs.Portrait, new Vector2(15f, 30f), new Vector2(120f, 160f), UiTheme.WithAlpha(p.TextDim, 0.35f));

            _heroName = UiKit.Title("Name", mid, 28, p.Text, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_heroName.rectTransform, new Vector2(168f, 44f), new Vector2(276f, 40f));
            _heroTitle = Label(mid, string.Empty, 15, p.Accent, new Vector2(168f, 86f), new Vector2(276f, 22f));
            _heroJobs = Label(mid, string.Empty, 13, p.TextDim, new Vector2(168f, 112f), new Vector2(276f, 20f));
            _badge = UiKit.Sliced("Badge", mid, UiSprites.Pill);
            UiKit.PlaceTopLeft(_badge.rectTransform, new Vector2(168f, 140f), new Vector2(64f, 24f));
            _badgeText = Label(_badge.transform, string.Empty, 12, Color.white, Vector2.zero, new Vector2(64f, 24f), TextAnchor.MiddleCenter, true);
            _heroMechanic = Label(mid, string.Empty, 12, PURPLE, new Vector2(168f, 176f), new Vector2(276f, 20f));

            // 성물 카드
            _relicCard = UiKit.Box("RelicCard", mid, UiTheme.WithAlpha(p.Inset, 0.7f));
            UiKit.PlaceTopLeft(_relicCard.rectTransform, new Vector2(0f, 254f), new Vector2(MID_W, PANEL_H - 254f));
            _relicCardFrame = UiKit.Sliced("Frame", _relicCard.transform, ItemVisuals.Frame);
            HudGraphics.Stretch(_relicCardFrame.rectTransform);
            RectTransform card = _relicCard.rectTransform;

            RectTransform iconBox = Box("IconBox", card, new Vector2(16f, 16f), new Vector2(96f, 96f), UiTheme.WithAlpha(p.PlateStroke, 0.6f), 0.9f);
            _cardIcon = Glyph(iconBox, null, new Vector2(14f, 10f), new Vector2(68f, 76f), Color.white);

            // 잠김
            _lockedCard = HudGraphics.CreateRect("Locked", card);
            HudGraphics.Stretch(_lockedCard);
            _lockedTitle = UiKit.Title("Title", _lockedCard, 20, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_lockedTitle.rectTransform, new Vector2(128f, 16f), new Vector2(300f, 30f));
            _lockedLine = Label(_lockedCard, string.Empty, 13, p.Text, new Vector2(128f, 50f), new Vector2(300f, 20f), TextAnchor.MiddleLeft, true);
            _lockedDesc = Label(_lockedCard, string.Empty, 12, p.TextDim, new Vector2(128f, 74f), new Vector2(300f, 36f), TextAnchor.UpperLeft);
            _lockedDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Divider(_lockedCard, 16f, 128f, MID_W - 32f);
            Label(_lockedCard, "반신까지", 13, p.Text, new Vector2(16f, 140f), new Vector2(200f, 20f), TextAnchor.MiddleLeft, true);
            _progressText = Label(_lockedCard, string.Empty, 13, PURPLE, new Vector2(MID_W - 116f, 140f), new Vector2(100f, 20f), TextAnchor.MiddleRight, true);
            _progressBar = UiKit.Bar("Progress", _lockedCard, PURPLE, PURPLE * 0.5f);
            UiKit.PlaceTopLeft(_progressBar.Rect, new Vector2(16f, 164f), new Vector2(MID_W - 32f, 8f));

            for (int i = 0; i < 3; i++)
            {
                float y = 186f + i * 30f;
                _conditionMarks.Add(Glyph(_lockedCard, CharacterGlyphs.Check, new Vector2(16f, y + 4f), new Vector2(16f, 16f), GREEN));
                Text name = Label(_lockedCard, string.Empty, 13, p.Text, new Vector2(40f, y), new Vector2(260f, 24f));
                Text value = Label(_lockedCard, string.Empty, 12, p.TextDim, new Vector2(MID_W - 176f, y), new Vector2(160f, 24f), TextAnchor.MiddleRight, true);
                _conditions.Add(new[] { name, value });
            }

            Label(_lockedCard, "※ 프로토타입 돌파 규칙 기준 (격 시스템 확정 후 교체)", 11, UiTheme.WithAlpha(p.TextDim, 0.7f),
                new Vector2(16f, 284f), new Vector2(MID_W - 32f, 18f));

            // 개방
            _openCard = HudGraphics.CreateRect("Open", card);
            HudGraphics.Stretch(_openCard);
            _openName = UiKit.Title("Name", _openCard, 22, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_openName.rectTransform, new Vector2(128f, 16f), new Vector2(300f, 32f));
            _openStage = Label(_openCard, string.Empty, 13, PURPLE, new Vector2(128f, 50f), new Vector2(300f, 20f), TextAnchor.MiddleLeft, true);
            _openTagline = Label(_openCard, string.Empty, 12, p.Text, new Vector2(128f, 74f), new Vector2(300f, 36f), TextAnchor.UpperLeft);
            _openTagline.horizontalOverflow = HorizontalWrapMode.Wrap;
            Divider(_openCard, 16f, 128f, MID_W - 32f);
            _openMechTitle = Label(_openCard, string.Empty, 14, p.Accent, new Vector2(16f, 140f), new Vector2(MID_W - 32f, 22f), TextAnchor.MiddleLeft, true);
            _openMechDesc = Label(_openCard, string.Empty, 13, p.Text, new Vector2(16f, 166f), new Vector2(MID_W - 32f, 42f), TextAnchor.UpperLeft);
            _openMechDesc.horizontalOverflow = HorizontalWrapMode.Wrap;
            Label(_openCard, "성물 효과", 13, p.Accent, new Vector2(16f, 216f), new Vector2(200f, 20f), TextAnchor.MiddleLeft, true);
            _openEffect1 = Label(_openCard, string.Empty, 13, p.Text, new Vector2(16f, 240f), new Vector2(200f, 22f));
            _openEffect2 = Label(_openCard, string.Empty, 13, p.Text, new Vector2(216f, 240f), new Vector2(212f, 22f));
            _detailButton = UiKit.Button("Detail", _openCard, "성물 자세히 ▶", 13);
            UiKit.PlaceTopLeft(_detailButton.rectTransform, new Vector2(MID_W - 16f - 150f, 280f), new Vector2(150f, 32f));
        }

        // ── [정보] 오른쪽: 능력치 ───────────────────────────
        private void BuildStats()
        {
            UiPalette p = UiTheme.P;
            RectTransform box = Box("Stats", _infoPage, new Vector2(RIGHT_X, PANEL_Y), new Vector2(RIGHT_W, PANEL_H),
                UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            Header(box, "능력치", 16f, 10f, RIGHT_W - 32f);

            RectTransform core = Box("Core", box, new Vector2(10f, 46f), new Vector2(RIGHT_W - 20f, 132f), UiTheme.WithAlpha(p.Accent, 0.8f), 0.6f);
            Label(core, "핵심", 11, p.Accent, new Vector2(10f, 6f), new Vector2(80f, 16f), TextAnchor.MiddleLeft, true);
            StatType[] coreStats = { StatType.AttackPower, StatType.Armor, StatType.MaxHealth, StatType.CritChance };

            for (int i = 0; i < coreStats.Length; i++)
            {
                float y = 26f + i * 25f;
                Label(core, StatText.Name(coreStats[i]), 13, p.TextDim, new Vector2(10f, y), new Vector2(140f, 24f));
                Text value = Label(core, string.Empty, 14, p.Text, new Vector2(RIGHT_W - 20f - 130f, y), new Vector2(120f, 24f), TextAnchor.MiddleRight, true);
                _core.Add((coreStats[i], value));
            }

            _stats = new StatListView(box, new Vector2(16f, 192f), new Vector2(RIGHT_W - 48f, PANEL_H - 192f - 12f), _player);
        }

        // ── [성물] 탭 ───────────────────────────────────────
        private void BuildRelicPage()
        {
            UiPalette p = UiTheme.P;

            // 왼쪽: 성물 그림·일화
            RectTransform left = Box("Relic", _relicPage, new Vector2(LEFT_X, PANEL_Y), new Vector2(LEFT_W, PANEL_H), UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            Header(left, "성물", 16f, 10f, LEFT_W - 32f);
            RectTransform art = Box("Art", left, new Vector2(16f, 46f), new Vector2(LEFT_W - 32f, 300f), UiTheme.WithAlpha(p.Accent, 0.7f), 0.85f);
            Image glow = Glyph(art, UiSprites.Glow, new Vector2(14f, 30f), new Vector2(240f, 240f), UiTheme.WithAlpha(p.Accent, 0.28f));
            glow.preserveAspect = false;
            _artIcon = Glyph(art, null, new Vector2(54f, 40f), new Vector2(160f, 220f), p.Title);
            _relicName = UiKit.Title("Name", left, 24, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_relicName.rectTransform, new Vector2(16f, 356f), new Vector2(LEFT_W - 32f, 34f));
            _relicSub = Label(left, string.Empty, 13, PURPLE, new Vector2(16f, 392f), new Vector2(LEFT_W - 32f, 20f), TextAnchor.MiddleLeft, true);
            _relicLore = Label(left, string.Empty, 13, p.Text, new Vector2(16f, 420f), new Vector2(LEFT_W - 32f, 150f), TextAnchor.UpperLeft);
            _relicLore.horizontalOverflow = HorizontalWrapMode.Wrap;
            _relicLore.lineSpacing = 1.2f;

            // 가운데: 단계 트랙
            RectTransform mid = Box("Stages", _relicPage, new Vector2(MID_X, PANEL_Y), new Vector2(MID_W, PANEL_H), UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            Header(mid, "성물 단계", 16f, 10f, MID_W - 32f);
            Image track = UiKit.Box("Track", mid, UiTheme.WithAlpha(p.PlateStroke, 0.6f));
            UiKit.PlaceTopLeft(track.rectTransform, new Vector2(33f, 70f), new Vector2(2f, 4 * 100f));
            string[] titles = { "1단계 · 각성", "2단계", "3단계", "4단계", "5단계 · 완성" };
            string[] subs = { "반신 달성 시 개방", "조건 미정", "조건 미정", "조건 미정", "신의 격에서 개방 (안)" };

            for (int i = 0; i < 5; i++)
            {
                float y = 56f + i * 100f;
                bool open = i == 0;
                Image circle = Glyph(mid, UiSprites.Pill, new Vector2(20f, y), new Vector2(28f, 28f), open ? p.Accent : p.Inset);
                circle.preserveAspect = false;
                Label(mid, (i + 1).ToString(), 14, open ? p.Inset : p.TextDim, new Vector2(20f, y), new Vector2(28f, 28f), TextAnchor.MiddleCenter, true);

                float tx = 60f;

                if (!open)
                {
                    Glyph(mid, CharacterGlyphs.Lock, new Vector2(60f, y + 6f), new Vector2(12f, 14f), p.TextDim);
                    tx = 78f;
                }

                Text title = UiKit.Title("StageTitle", mid, 16, open ? p.Title : p.TextDim, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(title.rectTransform, new Vector2(tx, y), new Vector2(260f, 26f));
                title.text = titles[i];
                Label(mid, subs[i], 12, open ? p.Text : UiTheme.WithAlpha(p.TextDim, 0.7f), new Vector2(tx, y + 26f), new Vector2(300f, 18f));

                if (open)
                {
                    _stageEffect[0] = Label(mid, string.Empty, 12, p.TextDim, new Vector2(tx, y + 46f), new Vector2(340f, 18f));
                    Image now = UiKit.Sliced("Now", mid, UiSprites.ButtonEquip);
                    UiKit.PlaceTopLeft(now.rectTransform, new Vector2(MID_W - 16f - 64f, y + 2f), new Vector2(56f, 22f));
                    Label(now.transform, "현재", 11, p.EquipText, Vector2.zero, new Vector2(56f, 22f), TextAnchor.MiddleCenter, true);
                }
            }

            // 오른쪽: 고유 메커니즘 흐름 + 현재 상태
            RectTransform right = Box("Mechanic", _relicPage, new Vector2(RIGHT_X, PANEL_Y), new Vector2(RIGHT_W, PANEL_H), UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            _mechHeader = Header(right, string.Empty, 16f, 10f, RIGHT_W - 32f);
            _mechSummary = Label(right, string.Empty, 13, p.Text, new Vector2(16f, 46f), new Vector2(RIGHT_W - 32f, 40f), TextAnchor.UpperLeft, true);
            _mechSummary.horizontalOverflow = HorizontalWrapMode.Wrap;
            Color[] flowColors = { p.Accent, PURPLE, RED };

            for (int i = 0; i < 3; i++)
            {
                float y = 100f + i * 110f;
                RectTransform step = Box("Flow", right, new Vector2(16f, y), new Vector2(RIGHT_W - 32f, 80f), UiTheme.WithAlpha(flowColors[i], 0.85f), 0.6f);
                _flowTitles[i] = Label(step, string.Empty, 15, flowColors[i], new Vector2(12f, 8f), new Vector2(RIGHT_W - 56f, 24f), TextAnchor.MiddleLeft, true);
                _flowTexts[i] = Label(step, string.Empty, 12, p.Text, new Vector2(12f, 36f), new Vector2(RIGHT_W - 56f, 38f), TextAnchor.UpperLeft);
                _flowTexts[i].horizontalOverflow = HorizontalWrapMode.Wrap;

                if (i < 2)
                {
                    Label(right, "▼", 12, p.TextDim, new Vector2(16f, y + 84f), new Vector2(RIGHT_W - 32f, 22f), TextAnchor.MiddleCenter);
                }
            }

            Divider(right, 16f, 438f, RIGHT_W - 32f);
            Label(right, "현재 상태", 13, p.Accent, new Vector2(16f, 448f), new Vector2(200f, 22f), TextAnchor.MiddleLeft, true);
            _stateLabel = Label(right, string.Empty, 12, p.Text, new Vector2(16f, 474f), new Vector2(150f, 20f));
            _stateValue = Label(right, string.Empty, 12, p.TextDim, new Vector2(RIGHT_W - 16f - 130f, 474f), new Vector2(130f, 20f), TextAnchor.MiddleRight, true);
            _stateBar = UiKit.Bar("State", right, p.Accent, p.Accent * 0.5f);
            UiKit.PlaceTopLeft(_stateBar.Rect, new Vector2(16f, 500f), new Vector2(RIGHT_W - 32f, 8f));
            _stateNote = Label(right, string.Empty, 11, UiTheme.WithAlpha(p.TextDim, 0.8f), new Vector2(16f, 516f), new Vector2(RIGHT_W - 32f, 40f), TextAnchor.UpperLeft);
            _stateNote.horizontalOverflow = HorizontalWrapMode.Wrap;
        }

        // ── 열기·닫기 ───────────────────────────────────────
        private void OnDestroy()
        {
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
            else if (!GameplayInput.IsBlocked)
            {
                _panel.gameObject.SetActive(true);
                GameplayInput.IsBlocked = true;
                _stats.ResetScroll();
                SetTab(false);
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _panel.gameObject.SetActive(false);
            GameplayInput.IsBlocked = false;
        }

        private void SetTab(bool relic)
        {
            _relicTab = relic && RelicOpen;
            _infoPage.gameObject.SetActive(!_relicTab);
            _relicPage.gameObject.SetActive(_relicTab);

            if (_relicTab && Relic is HeroRelic heroRelic)
            {
                heroRelic.IsUnseen = false;
            }
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _messageEndTime = Time.unscaledTime + 2f;
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
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
            }

            if (!IsOpen)
            {
                return;
            }

            _drag.Tick();
            _message.enabled = Time.unscaledTime < _messageEndTime;
            HandleTabs();

            if (_relicTab)
            {
                RefreshRelicPage();
            }
            else
            {
                _stats.Tick();
                RefreshInfo();
            }
        }

        private void HandleTabs()
        {
            UiPalette p = UiTheme.P;
            bool open = RelicOpen;
            bool infoHover = UiKit.IsHover(_tabInfo.rectTransform);
            bool relicHover = UiKit.IsHover(_tabRelic.rectTransform);

            if (infoHover && UiKit.Clicked)
            {
                SetTab(false);
            }
            else if (relicHover && UiKit.Clicked)
            {
                if (open)
                {
                    SetTab(true);
                }
                else
                {
                    ShowMessage("반신에 이르면 성물이 깨어납니다");
                }
            }

            _tabInfo.color = _tabRelic.color = Color.white;
            _tabInfo.sprite = !_relicTab ? UiSprites.ButtonEquip : UiSprites.Button;
            _tabRelic.sprite = _relicTab ? UiSprites.ButtonEquip : UiSprites.Button;
            _tabInfoText.color = !_relicTab ? p.EquipText : p.Text;
            _tabRelicText.color = _relicTab ? p.EquipText : open ? p.Text : p.TextDisabled;
            _tabRelicText.text = open ? "성물" : "성물 (잠김)";
            _relicLockNote.gameObject.SetActive(!open);
            _relicDot.gameObject.SetActive(open && Relic is HeroRelic relic && relic.IsUnseen);

            if (infoHover && _relicTab)
            {
                _tabInfo.color = new Color(1f, 0.92f, 0.8f);
            }

            if (relicHover && !_relicTab && open)
            {
                _tabRelic.color = new Color(1f, 0.92f, 0.8f);
            }
        }

        // ── 갱신: [정보] ────────────────────────────────────
        private void RefreshInfo()
        {
            UiPalette p = UiTheme.P;

            if (_progression != null)
            {
                _level.text = _progression.Level.ToString();
                _expBar.SetRatio(_progression.IsMaxLevel ? 1f : _progression.Progress);
                _expText.text = _progression.IsMaxLevel ? "최대" : $"{_progression.Progress * 100f:0.0}%";
            }

            IJobGauge gauge = FindJobGauge();
            _faithLabel.text = gauge != null ? gauge.GaugeName : "직업 게이지";
            _faithText.text = gauge != null ? $"{gauge.Current:N0} / {gauge.Max:N0}" : "직업 선택 전";
            _faithBar.SetRatio(gauge != null && gauge.Max > 0f ? gauge.Current / gauge.Max : 0f);

            List<string> jobs = JobChain();
            _job1.text = jobs.Count > 0 ? jobs[0] : "무직";
            _job2.text = jobs.Count > 1 ? jobs[1] : "-";
            _job1.color = jobs.Count > 0 ? p.Text : p.TextDisabled;
            _job2.color = jobs.Count > 1 ? p.Text : p.TextDisabled;

            _kills.text = _record != null ? $"{_record.Kills:N0}" : "-";
            _playTime.text = _record != null ? PlayTime(_record.PlaySeconds) : "-";

            foreach ((StatType stat, Text value) in _core)
            {
                value.text = stat == StatType.MaxHealth && _health != null ? $"{Mathf.CeilToInt(_health.Max):N0}"
                    : _characterStats != null ? StatText.Value(stat, _characterStats.Sheet.Get(stat)) : "-";
            }

            RefreshHeroCard(jobs);
            RefreshRelicCard();
        }

        private void RefreshHeroCard(List<string> jobs)
        {
            UiPalette p = UiTheme.P;
            AscensionTier tier = _progression != null ? _progression.Tier : AscensionTier.Mortal;
            HeroPathDefinition path = _hero != null ? _hero.Current : null;

            if (path != null)
            {
                _heroName.text = path.DisplayName;
                _heroName.color = p.Text;
                _heroTitle.text = path.Title;
                _heroMechanic.text = $"고유 메커니즘 · {path.MechanicName}";
                _portraitIcon.color = UiTheme.WithAlpha(p.Accent, 0.45f);
            }
            else
            {
                _heroName.text = "영웅의 길";
                _heroName.color = p.TextDim;
                _heroTitle.text = "반신 돌파 후 선택";
                _heroMechanic.text = $"후보: {CandidateNames()}";
                _portraitIcon.color = UiTheme.WithAlpha(p.TextDim, 0.3f);
            }

            _heroJobs.text = jobs.Count > 0 ? string.Join(" · ", jobs) : "무직";
            _badgeText.text = AscensionModel.DisplayName(tier);
            _badge.color = UiTheme.WithAlpha(PURPLE, tier >= AscensionTier.Demigod ? 0.85f : 0.45f);
        }

        private void RefreshRelicCard()
        {
            UiPalette p = UiTheme.P;
            HeroRelic relic = Relic;
            bool open = RelicOpen;
            _lockedCard.gameObject.SetActive(!open);
            _openCard.gameObject.SetActive(open);
            _relicCardFrame.color = open ? p.Accent : UiTheme.WithAlpha(p.PlateStroke, 0.6f);
            _relicCard.color = open ? UiTheme.WithAlpha(Color.Lerp(p.Inset, p.Accent, 0.12f), 0.85f) : UiTheme.WithAlpha(p.Inset, 0.7f);

            if (open)
            {
                RelicInfo info = relic.Info;
                _cardIcon.sprite = CharacterGlyphs.Relic(info.Shape);
                _cardIcon.color = p.Title;
                _openName.text = info.Name;
                _openStage.text = $"성물 {relic.Stage}단계 · 각성";
                _openTagline.text = info.Tagline;
                _openMechTitle.text = $"고유 메커니즘 · {_hero.Current.MechanicName}";
                _openMechDesc.text = info.MechanicDescription;
                _openEffect1.text = info.Stage1Effects.Length > 0 ? info.Stage1Effects[0] : string.Empty;
                _openEffect2.text = info.Stage1Effects.Length > 1 ? info.Stage1Effects[1] : string.Empty;

                bool hover = UiKit.IsHover(_detailButton.rectTransform);
                _detailButton.color = hover ? new Color(1f, 0.92f, 0.8f) : Color.white;

                if (hover && UiKit.Clicked)
                {
                    SetTab(true);
                }

                return;
            }

            AscensionTier tier = _progression != null ? _progression.Tier : AscensionTier.Mortal;
            bool waiting = tier >= AscensionTier.Demigod;
            _cardIcon.sprite = CharacterGlyphs.Relic("mace");
            _cardIcon.color = UiTheme.WithAlpha(p.TextDim, 0.35f);
            _lockedTitle.text = waiting ? "깨어나기 직전의 성물" : "봉인된 성물";
            _lockedLine.text = waiting ? "영웅의 길을 고르면 성물이 깨어납니다" : "반신에 이르면 성물이 깨어납니다";
            _lockedDesc.text = "성물이 깨어나면 고유 메커니즘과 [성물] 탭이 열립니다.";

            // 실제 돌파 조건: 직업 선택 → 초인 돌파 → 반신 레벨
            int level = _progression != null ? _progression.Level : 1;
            int demigod = _progression != null ? _progression.DemigodLevel : 10;
            int superhuman = _progression != null ? _progression.SuperhumanLevel : 5;
            bool hasJob = _job != null && _job.HasJob;
            float ratio = waiting ? 1f : Mathf.Clamp01((float)level / demigod);
            _progressBar.SetRatio(ratio);
            _progressText.text = $"{ratio * 100f:0}%";

            SetCondition(0, "직업 선택", hasJob, hasJob ? _job.Current.DisplayName : "미선택");
            SetCondition(1, $"초인 돌파 (Lv {superhuman})", tier >= AscensionTier.Superhuman, tier >= AscensionTier.Superhuman ? "완료" : "미완료");
            SetCondition(2, $"레벨 {demigod} 달성", level >= demigod, $"{Mathf.Min(level, demigod)} / {demigod}");
        }

        private void SetCondition(int index, string name, bool done, string value)
        {
            UiPalette p = UiTheme.P;
            _conditionMarks[index].sprite = done ? CharacterGlyphs.Check : CharacterGlyphs.Ring;
            _conditionMarks[index].color = done ? GREEN : p.TextDim;
            _conditions[index][0].text = name;
            _conditions[index][0].color = done ? p.Text : p.TextDim;
            _conditions[index][1].text = value;
            _conditions[index][1].color = done ? GREEN : p.TextDim;
        }

        // ── 갱신: [성물] ────────────────────────────────────
        private void RefreshRelicPage()
        {
            HeroRelic relic = Relic;

            if (relic == null || relic.Info == null)
            {
                SetTab(false);
                return;
            }

            RelicInfo info = relic.Info;
            HeroPathDefinition path = _hero.Current;
            _artIcon.sprite = CharacterGlyphs.Relic(info.Shape);
            _relicName.text = info.Name;
            _relicSub.text = $"{path.DisplayName}의 성물 · {relic.Stage}단계 각성";
            _relicLore.text = info.Lore;
            _stageEffect[0].text = string.Join("  ·  ", info.Stage1Effects);
            _mechHeader.text = $"고유 메커니즘 · {path.MechanicName}";
            _mechSummary.text = info.MechanicSummary;

            for (int i = 0; i < _flowTitles.Length; i++)
            {
                _flowTitles[i].text = i < info.Flow.Length ? info.Flow[i].Title : string.Empty;
                _flowTexts[i].text = i < info.Flow.Length ? info.Flow[i].Description : string.Empty;
            }

            if (info.HeroId == "seokcheon")
            {
                StaffReachMode staff = _player.GetComponent<StaffReachMode>();
                _stateLabel.text = "현재 모드";
                _stateValue.text = staff == null ? "-" : staff.Mode == StaffMode.Short ? "단봉" : "장봉";
                _stateBar.SetRatio(relic.BuffRemaining / RelicCatalog.SEOKCHEON_DURATION);
                _stateNote.text = relic.IsBuffActive ? $"성물 효과 중: 피해 +15% ({relic.BuffRemaining:0.0}초)" : "모드변환하면 3초간 피해 +15%";
                return;
            }

            PoiseReceiver poise = _controller != null && _controller.CurrentTarget != null
                ? _controller.CurrentTarget.GetComponent<PoiseReceiver>() : null;
            _stateLabel.text = "대상 버팀 게이지";
            _stateValue.text = poise == null ? "대상 없음" : poise.IsStaggered ? "무력화" : $"{poise.Ratio * 100f:0}%";
            _stateBar.SetRatio(poise != null ? poise.Ratio : 0f);
            _stateNote.text = "전투 중에는 대상 게이지 아래에 같은 버팀 막대가 표시됩니다.";
        }

        // ── 도움 ───────────────────────────────────────────
        private List<string> JobChain()
        {
            var names = new List<string>();

            // 현재 직업에서 상위 직업으로 거슬러 올라가며 (성투사 → 성기사), 무직은 제외
            for (JobDefinition job = _job != null && _job.HasJob ? _job.Current : null; job != null; job = job.ParentJob)
            {
                names.Insert(0, job.DisplayName);
            }

            names.Remove("무직");
            return names;
        }

        private string CandidateNames()
        {
            var names = new List<string>();

            if (_job != null && _job.HasJob)
            {
                foreach (HeroPathDefinition path in _job.Current.ResolveHeroPaths())
                {
                    names.Add(path.DisplayName);
                }
            }

            return names.Count > 0 ? string.Join(" · ", names) : "직업 선택 후 공개";
        }

        private IJobGauge FindJobGauge()
        {
            _player.GetComponents(_gauges);

            foreach (IJobGauge gauge in _gauges)
            {
                if (!(gauge is Behaviour behaviour) || behaviour.isActiveAndEnabled)
                {
                    return gauge;
                }
            }

            return null;
        }

        private static string PlayTime(float seconds)
        {
            int total = Mathf.FloorToInt(seconds);
            int hours = total / 3600, minutes = total / 60 % 60;
            return hours > 0 ? $"{hours}시간 {minutes}분" : minutes > 0 ? $"{minutes}분 {total % 60}초" : $"{total % 60}초";
        }
    }

    /// <summary>캐릭터 창 도형 (코드 생성 흰 스프라이트): 초상 실루엣·성물(철퇴·신봉)·체크·빈 원·자물쇠</summary>
    public static class CharacterGlyphs
    {
        private static Sprite _portrait;
        private static Sprite _staff;
        private static Sprite _check;
        private static Sprite _ring;
        private static Sprite _lock;

        public static Sprite Portrait => _portrait != null ? _portrait : _portrait = UiSprites.MakeSuper("GlyphPortrait", 128, (u, v) =>
        {
            float hx = u - 0.5f, hy = v - 0.72f;
            bool head = hx * hx + hy * hy < 0.026f;
            float bx = (u - 0.5f) / 0.36f, by = (v - 0.18f) / 0.34f;
            bool body = v < 0.52f && bx * bx + by * by < 1f;
            return head || body ? Color.white : Color.clear;
        });

        public static Sprite Check => _check != null ? _check : _check = UiSprites.MakeSuper("GlyphCheck", 32, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));

            if (d > 0.48f)
            {
                return Color.clear;
            }

            bool tick = Seg(u, v, 0.27f, 0.5f, 0.44f, 0.33f, 0.07f) || Seg(u, v, 0.44f, 0.33f, 0.74f, 0.66f, 0.07f);
            return tick ? new Color(0.06f, 0.07f, 0.09f) : Color.white;
        });

        public static Sprite Ring => _ring != null ? _ring : _ring = UiSprites.MakeSuper("GlyphRing", 32, (u, v) =>
        {
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f));
            return d < 0.48f && d > 0.38f ? Color.white : Color.clear;
        });

        public static Sprite Lock => _lock != null ? _lock : _lock = UiSprites.MakeSuper("GlyphLock", 32, (u, v) =>
        {
            bool body = u > 0.18f && u < 0.82f && v > 0.08f && v < 0.56f;
            float d = Mathf.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.56f) * (v - 0.56f));
            bool shackle = v > 0.56f && d < 0.3f && d > 0.18f;
            bool sides = v > 0.5f && v <= 0.56f && ((u > 0.2f && u < 0.32f) || (u > 0.68f && u < 0.8f));
            return body || shackle || sides ? Color.white : Color.clear;
        });

        public static Sprite Relic(string shape)
        {
            if (shape == "staff")
            {
                return _staff != null ? _staff : _staff = UiSprites.MakeSuper("GlyphStaff", 128, (u, v) =>
                {
                    bool rod = Mathf.Abs(u - 0.5f) < 0.045f && v > 0.06f && v < 0.94f;
                    bool caps = Mathf.Abs(u - 0.5f) < 0.08f && (Mathf.Abs(v - 0.9f) < 0.05f || Mathf.Abs(v - 0.1f) < 0.05f);
                    bool bands = Mathf.Abs(u - 0.5f) < 0.065f && (Mathf.Abs(v - 0.62f) < 0.015f || Mathf.Abs(v - 0.38f) < 0.015f);
                    return rod || caps || bands ? Color.white : Color.clear;
                });
            }

            return ItemVisuals.Icon(ItemIcon.Mace);
        }

        private static bool Seg(float x, float y, float ax, float ay, float bx, float by, float half)
        {
            float dx = bx - ax, dy = by - ay;
            float t = Mathf.Clamp01(((x - ax) * dx + (y - ay) * dy) / (dx * dx + dy * dy));
            float px = ax + dx * t - x, py = ay + dy * t - y;
            return px * px + py * py < half * half;
        }
    }
}
