using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 상단 보스 체력바 (10/21): 교전 중인 보스(BossStatus)의 이름·칭호, 체력(깎인 만큼 늦게 따라오는 흔적), 보호막,
    /// 버팀 게이지(야로미르), 격 수확 결정 수·흡수까지 남은 시간, 흡수 단계. 첫 교전 시 이름 배너, 처치 시 격파 배너.
    /// B안 스타일: 균열이 간 금속판 위 알약형 막대 (UiTheme). HudView가 생성합니다. 보스가 없으면 숨김.
    /// </summary>
    public class BossHudView : MonoBehaviour
    {
        private const float BAR_WIDTH = 440f;   // 10/25: BossPlate 폭 500 (= 막대 + 60)
        private const float BAR_HEIGHT = 22f;
        private const float PLATE_TOP = 18f;
        private const float PLATE_HEIGHT = 132f;
        private const float TRAIL_DELAY = 0.45f;
        private const float TRAIL_SPEED = 0.6f;
        private const float BANNER_TIME = 2.6f;
        private static readonly Color INTRO_FILL = new Color(0.55f, 0.57f, 0.6f);
        private static readonly Color INTRO_DARK = new Color(0.17f, 0.18f, 0.2f);
        private static readonly Color PHASE_PASSED = new Color(0.45f, 0.42f, 0.42f, 0.7f);
        private const int MAX_PHASE_MARKERS = 4;
        private const float BAR_TOP = -70f;

        private RectTransform _plate;
        private UiBar _bar;
        private UiBar _poise;
        private Text _nameText;
        private Text _titleText;
        private Text _crystalText;
        private Text _bannerTitle;
        private Text _bannerSub;
        private CanvasGroup _bannerGroup;
        private readonly Image[] _phaseMarkers = new Image[MAX_PHASE_MARKERS];

        private BossStatus _boss;
        private BossStatus _introducedBoss;
        private float _trail = 1f;
        private float _lastHitTime;
        private float _lastRatio = 1f;
        private float _bannerEndTime;

        /// <summary>보스 체력바가 보이는 중 (대상 체력 게이지 위치 조정용)</summary>
        public bool IsShowing => _plate != null && _plate.gameObject.activeSelf;

        /// <summary>보이는 보스의 체력 (대상 게이지와 겹치지 않게 HudView가 비교)</summary>
        public Health ShownHealth => IsShowing && _boss != null ? _boss.Health : null;

        /// <summary>체력바가 차지하는 높이 (위 여백 포함)</summary>
        public float OccupiedHeight => PLATE_TOP + PLATE_HEIGHT + 12f;

        public void Initialize()
        {
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);
            UiPalette p = UiTheme.P;

            // 균열 간 금속판 (보스 고정 균열)
            _plate = UiKit.Plate("BossPlate", root).rectTransform;
            UiKit.Place(_plate, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -PLATE_TOP),
                new Vector2(BAR_WIDTH + 60f, PLATE_HEIGHT));

            _titleText = UiKit.Title("Title", _plate, 14, Color.white, TextAnchor.MiddleCenter);   // 10/25: 흰색
            UiKit.Place(_titleText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -8f), new Vector2(BAR_WIDTH, 20f));
            _nameText = UiKit.Title("Name", _plate, 28, p.Title, TextAnchor.MiddleCenter);
            UiKit.Place(_nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -31f), new Vector2(BAR_WIDTH, 36f));   // 10/26: 칭호와 간격 +5

            _bar = UiKit.Bar("Health", _plate, p.Hp, p.HpDark);
            UiKit.Place(_bar.Rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -70f), new Vector2(BAR_WIDTH, BAR_HEIGHT));
            _bar.EnableTrail(p.Trail);
            _bar.EnableShield(UiTheme.WithAlpha(p.Shield, 0.85f));
            _bar.EnableText(13);

            // 페이즈 표식: 체력바 위, 다음 페이즈 시작 비율 위치에 빨간 역삼각형 (10/26)
            for (int i = 0; i < MAX_PHASE_MARKERS; i++)
            {
                Image marker = HudGraphics.CreateRect("PhaseMarker" + i, _plate).gameObject.AddComponent<Image>();
                marker.sprite = UiSprites.PhaseMarker;
                marker.raycastTarget = false;
                UiKit.Place(marker.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, BAR_TOP - 1f), new Vector2(16f, 13f));
                marker.gameObject.SetActive(false);
                _phaseMarkers[i] = marker;
            }

            // 버팀 게이지 (야로미르 선택 시 첫 피해부터)
            _poise = UiKit.Bar("Poise", _plate, p.Poise, p.ResourceDark);
            UiKit.Place(_poise.Rect, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -98f), new Vector2(BAR_WIDTH * 0.5f, 7f));

            // 격 수확 결정
            _crystalText = UiKit.Label("Crystals", _plate, 14, p.Crystal, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_crystalText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(BAR_WIDTH, 20f));

            // 등장·격파 배너 (화면 가운데 위, 가로 띠 + 위아래 금선)
            RectTransform banner = HudGraphics.CreateRect("Banner", root);
            UiKit.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f), new Vector2(1920f, 200f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            Image band = UiKit.Box("Band", banner, new Color(0f, 0f, 0f, 0.6f));
            HudGraphics.Stretch(band.rectTransform);
            Image lineTop = UiKit.Box("LineTop", banner, UiTheme.WithAlpha(p.CrackOuter, 0.8f));
            UiKit.Place(lineTop.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -18f), new Vector2(1000f, 2f));
            Image lineBottom = UiKit.Box("LineBottom", banner, UiTheme.WithAlpha(p.CrackOuter, 0.8f));
            UiKit.Place(lineBottom.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 18f), new Vector2(1000f, 2f));
            _bannerTitle = UiKit.Title("Title", banner, 76, p.Title, TextAnchor.MiddleCenter);
            UiKit.Place(_bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(1400f, 96f));
            _bannerTitle.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.CrackOuter, 0.45f);
            _bannerSub = UiKit.Title("Sub", banner, 24, p.Text, TextAnchor.MiddleCenter);
            UiKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -62f), new Vector2(1400f, 34f));   // 10/26: 제목과 간격 +10
            _bannerGroup.alpha = 0f;

            _plate.gameObject.SetActive(false);
            BossStatus.AnyDefeated += OnBossDefeated;
            BossStatus.AnyPhaseChanged += OnBossPhaseChanged;
        }

        private void OnDestroy()
        {
            BossStatus.AnyDefeated -= OnBossDefeated;
            BossStatus.AnyPhaseChanged -= OnBossPhaseChanged;
        }

        private void Update()
        {
            UpdateBanner();
            BossStatus boss = FindEngagedBoss();

            if (boss != _boss)
            {
                _boss = boss;
                _trail = _lastRatio = boss != null ? Ratio(boss.Health) : 1f;

                // 처음 교전한 보스면 이름 배너
                if (boss != null && boss != _introducedBoss)
                {
                    _introducedBoss = boss;
                    ShowBanner(boss.BossName, Spaced(boss.Title));
                }
            }

            _plate.gameObject.SetActive(_boss != null);

            if (_boss == null)
            {
                return;
            }

            Health health = _boss.Health;
            float ratio = Ratio(health);

            if (ratio < _lastRatio)
            {
                _lastHitTime = Time.time;
            }

            _lastRatio = ratio;

            // 흔적: 잠시 멈췄다가 따라 내려옴
            if (_trail < ratio)
            {
                _trail = ratio;
            }
            else if (Time.time - _lastHitTime > TRAIL_DELAY)
            {
                _trail = Mathf.MoveTowards(_trail, ratio, TRAIL_SPEED * Time.deltaTime);
            }

            // 등장 연출 중: 회색 (공격 불가), 끝나면 원래 색 (10/25)
            UiPalette palette = UiTheme.P;
            _bar.SetColor(_boss.IsIntro ? INTRO_FILL : palette.Hp, _boss.IsIntro ? INTRO_DARK : palette.HpDark);
            _bar.SetRatio(ratio);
            _bar.SetTrail(_trail);
            _bar.SetShield(health.Max > 0f ? Mathf.Clamp01(_boss.Shield / health.Max) : 0f);
            _nameText.text = _boss.BossName;
            _titleText.text = _boss.Title;

            string shield = _boss.Shield > 0f ? $"   +보호막 {Mathf.CeilToInt(_boss.Shield)}" : string.Empty;
            _bar.Text.text = _boss.IsIntro ? "공격 불가" : $"{Mathf.CeilToInt(ratio * 100f)}%{shield}";

            UpdatePoise();
            UpdateCrystals();
            UpdatePhaseMarkers(ratio);
        }

        // 지난 페이즈 표식은 회색, 남은 페이즈는 빨간색
        private void UpdatePhaseMarkers(float ratio)
        {
            float[] ratios = _boss.PhaseRatios;

            for (int i = 0; i < MAX_PHASE_MARKERS; i++)
            {
                Image marker = _phaseMarkers[i];
                bool show = i < ratios.Length && ratios[i] > 0f && ratios[i] < 1f;
                marker.gameObject.SetActive(show);

                if (!show)
                {
                    continue;
                }

                marker.rectTransform.anchoredPosition = new Vector2((ratios[i] - 0.5f) * BAR_WIDTH, BAR_TOP - 1f);
                marker.color = ratio <= ratios[i] ? PHASE_PASSED : Color.white;
            }
        }

        private void UpdatePoise()
        {
            PoiseReceiver poise = _boss.GetComponent<PoiseReceiver>();
            _poise.Rect.gameObject.SetActive(poise != null);

            if (poise == null)
            {
                return;
            }

            bool staggered = poise.IsStaggered;
            _poise.SetRatio(staggered ? 1f : poise.Ratio);
            Color color = staggered ? UiTheme.P.Stagger : UiTheme.P.Poise;
            _poise.SetColor(color, color);
        }

        private void UpdateCrystals()
        {
            string text = string.Empty;

            if (_boss.ActiveCrystals > 0)
            {
                text = $"◆ 격 결정 {_boss.ActiveCrystals}개 — 흡수까지 {_boss.CrystalTimeRemaining:0.0}초";
            }

            if (_boss.AbsorbedStacks > 0)
            {
                string stacks = $"강화 {_boss.AbsorbedStacks}단계";
                text = string.IsNullOrEmpty(text) ? stacks : $"{text}   ·   {stacks}";
            }

            _crystalText.text = text;
        }

        // 교전 중인 보스 중 첫 번째
        private static BossStatus FindEngagedBoss()
        {
            foreach (BossStatus boss in BossStatus.Active)
            {
                if (boss != null && boss.IsEngaged)
                {
                    return boss;
                }
            }

            return null;
        }

        private void OnBossDefeated(BossStatus boss)
        {
            ShowBanner($"{boss.BossName} 격파", "신의 격을 쓰러뜨렸다");
        }

        // 10/26: 페이즈 전환 배너 ("2 페이즈 · 진군의 노여움")
        private void OnBossPhaseChanged(BossStatus boss, int phase, string title, string subtitle)
        {
            string head = string.IsNullOrEmpty(title) ? $"{phase + 1} 페이즈" : $"{phase + 1} 페이즈 · {title}";
            ShowBanner(head, subtitle ?? string.Empty);
        }

        private void ShowBanner(string title, string sub)
        {
            _bannerTitle.text = title;
            _bannerSub.text = sub;
            _bannerEndTime = Time.unscaledTime + BANNER_TIME;
        }

        // 페이드 인 0.3초 → 유지 → 페이드 아웃 0.6초, 등장 시 살짝 확대
        private void UpdateBanner()
        {
            float remaining = _bannerEndTime - Time.unscaledTime;

            if (remaining <= 0f)
            {
                _bannerGroup.alpha = 0f;
                return;
            }

            float elapsed = BANNER_TIME - remaining;
            float alpha = Mathf.Min(Mathf.Clamp01(elapsed / 0.3f), Mathf.Clamp01(remaining / 0.6f));
            _bannerGroup.alpha = alpha;
            float scale = Mathf.Lerp(1.15f, 1f, Mathf.Clamp01(elapsed / 0.35f));
            _bannerTitle.transform.localScale = new Vector3(scale, scale, 1f);
        }

        // 칭호 글자 사이 띄우기 ("천계의 무신" → "천 계 의   무 신")
        private static string Spaced(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();

            foreach (char c in text)
            {
                builder.Append(c == ' ' ? "  " : c.ToString()).Append(' ');
            }

            return builder.ToString().TrimEnd();
        }

        private static float Ratio(Health health)
        {
            return health != null && health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
        }
    }
}
