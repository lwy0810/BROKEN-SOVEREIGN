using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 상단 보스 체력바 (10/21): 교전 중인 보스(BossStatus)의 이름·칭호, 체력(깎인 만큼 늦게 따라오는 흔적), 보호막,
    /// 버팀 게이지(야로미르), 격 수확 결정 수·흡수까지 남은 시간, 흡수 단계. 첫 교전 시 이름 배너, 처치 시 격파 배너.
    /// HudView가 생성합니다. 보스가 없으면 숨김.
    /// </summary>
    public class BossHudView : MonoBehaviour
    {
        private const float BAR_WIDTH = 760f;
        private const float BAR_HEIGHT = 18f;
        private const float TRAIL_DELAY = 0.45f;
        private const float TRAIL_SPEED = 0.6f;
        private const float BANNER_TIME = 2.6f;

        private static readonly Color HP_COLOR = new Color(0.75f, 0.12f, 0.1f);
        private static readonly Color TRAIL_COLOR = new Color(1f, 0.85f, 0.6f, 0.9f);
        private static readonly Color SHIELD_COLOR = new Color(0.55f, 0.85f, 1f, 0.85f);
        private static readonly Color POISE_COLOR = new Color(0.95f, 0.78f, 0.3f);
        private static readonly Color STAGGER_COLOR = new Color(1f, 0.45f, 0.15f);
        private static readonly Color CRYSTAL_COLOR = new Color(0.7f, 0.5f, 1f);

        private RectTransform _bar;
        private Image _hpFill;
        private Image _trailFill;
        private Image _shieldFill;
        private Text _nameText;
        private Text _percentText;
        private RectTransform _poiseBar;
        private Image _poiseFill;
        private Text _crystalText;
        private Text _bannerTitle;
        private Text _bannerSub;
        private CanvasGroup _bannerGroup;

        private BossStatus _boss;
        private BossStatus _introducedBoss;
        private float _trail = 1f;
        private float _lastHitTime;
        private float _lastRatio = 1f;
        private float _bannerEndTime;

        /// <summary>보스 체력바가 보이는 중 (대상 체력 게이지 위치 조정용)</summary>
        public bool IsShowing => _bar != null && _bar.gameObject.activeSelf;

        /// <summary>보이는 보스의 체력 (대상 게이지와 겹치지 않게 HudView가 비교)</summary>
        public Health ShownHealth => IsShowing && _boss != null ? _boss.Health : null;

        /// <summary>체력바가 차지하는 높이 (위 여백 포함)</summary>
        public float OccupiedHeight => 96f;

        public void Initialize()
        {
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _bar = HudGraphics.CreateRect("BossBar", root);
            UiKit.Place(_bar, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -46f),
                new Vector2(BAR_WIDTH, BAR_HEIGHT));
            Image background = UiKit.Box("Background", _bar, new Color(0f, 0f, 0f, 0.75f));
            HudGraphics.Stretch(background.rectTransform);
            Outline outline = background.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(UiKit.GOLD.r, UiKit.GOLD.g, UiKit.GOLD.b, 0.9f);
            outline.effectDistance = new Vector2(2f, -2f);

            _trailFill = UiKit.Fill("Trail", _bar, TRAIL_COLOR);
            _hpFill = UiKit.Fill("Health", _bar, HP_COLOR);
            _shieldFill = UiKit.Fill("Shield", _bar, SHIELD_COLOR);
            _shieldFill.fillOrigin = (int)Image.OriginHorizontal.Right;

            _percentText = UiKit.Label("Percent", _bar, 14, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_percentText.rectTransform);

            _nameText = UiKit.Label("Name", _bar, 22, UiKit.GOLD, TextAnchor.LowerCenter, FontStyle.Bold);
            UiKit.Place(_nameText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0f), new Vector2(0f, 4f),
                new Vector2(BAR_WIDTH, 28f));

            // 버팀 게이지 (야로미르 선택 시 첫 피해부터)
            _poiseBar = HudGraphics.CreateRect("Poise", _bar);
            UiKit.Place(_poiseBar, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -4f),
                new Vector2(BAR_WIDTH * 0.6f, 6f));
            Image poiseBackground = UiKit.Box("Background", _poiseBar, new Color(0f, 0f, 0f, 0.6f));
            HudGraphics.Stretch(poiseBackground.rectTransform);
            _poiseFill = UiKit.Fill("Fill", _poiseBar, POISE_COLOR);

            // 격 수확 결정
            _crystalText = UiKit.Label("Crystals", _bar, 15, CRYSTAL_COLOR, TextAnchor.UpperCenter, FontStyle.Bold);
            UiKit.Place(_crystalText.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 1f), new Vector2(0f, -14f),
                new Vector2(BAR_WIDTH, 20f));

            // 등장·격파 배너 (화면 가운데 위)
            RectTransform banner = HudGraphics.CreateRect("Banner", root);
            UiKit.Place(banner, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 170f),
                new Vector2(1200f, 120f));
            _bannerGroup = banner.gameObject.AddComponent<CanvasGroup>();
            _bannerGroup.blocksRaycasts = false;
            _bannerTitle = UiKit.Label("Title", banner, 64, UiKit.GOLD, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_bannerTitle.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 14f),
                new Vector2(1200f, 76f));
            _bannerSub = UiKit.Label("Sub", banner, 24, UiKit.SUBTLE, TextAnchor.MiddleCenter);
            UiKit.Place(_bannerSub.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                new Vector2(1200f, 32f));
            _bannerGroup.alpha = 0f;

            _bar.gameObject.SetActive(false);
            BossStatus.AnyDefeated += OnBossDefeated;
        }

        private void OnDestroy()
        {
            BossStatus.AnyDefeated -= OnBossDefeated;
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
                    ShowBanner(boss.BossName, boss.Title);
                }
            }

            _bar.gameObject.SetActive(_boss != null);

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

            _hpFill.fillAmount = ratio;
            _trailFill.fillAmount = _trail;
            _shieldFill.fillAmount = health.Max > 0f ? Mathf.Clamp01(_boss.Shield / health.Max) : 0f;
            _nameText.text = string.IsNullOrEmpty(_boss.Title) ? _boss.BossName : $"{_boss.BossName}  <size=16>{_boss.Title}</size>";
            _nameText.supportRichText = true;

            string shield = _boss.Shield > 0f ? $"  +보호막 {Mathf.CeilToInt(_boss.Shield)}" : string.Empty;
            _percentText.text = $"{Mathf.CeilToInt(ratio * 100f)}%{shield}";

            UpdatePoise();
            UpdateCrystals();
        }

        private void UpdatePoise()
        {
            PoiseReceiver poise = _boss.GetComponent<PoiseReceiver>();
            _poiseBar.gameObject.SetActive(poise != null);

            if (poise == null)
            {
                return;
            }

            bool staggered = poise.IsStaggered;
            _poiseFill.fillAmount = staggered ? 1f : poise.Ratio;
            _poiseFill.color = staggered ? STAGGER_COLOR : POISE_COLOR;
        }

        private void UpdateCrystals()
        {
            string text = string.Empty;

            if (_boss.ActiveCrystals > 0)
            {
                text = $"격 결정 {_boss.ActiveCrystals}개 — 흡수까지 {_boss.CrystalTimeRemaining:0.0}초";
            }

            if (_boss.AbsorbedStacks > 0)
            {
                string stacks = $"강화 {_boss.AbsorbedStacks}단계";
                text = string.IsNullOrEmpty(text) ? stacks : $"{text}   ·   {stacks}";
            }

            _crystalText.text = text;
            _crystalText.rectTransform.anchoredPosition = new Vector2(0f, _poiseBar.gameObject.activeSelf ? -14f : -6f);
        }

        // 교전 중인 보스 중 가장 가까운 보스 (플레이어 기준은 카메라 대신 첫 번째로 충분)
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
            _bannerGroup.transform.localScale = new Vector3(scale, scale, 1f);
        }

        private static float Ratio(Health health)
        {
            return health != null && health.Max > 0f ? Mathf.Clamp01(health.Current / health.Max) : 0f;
        }
    }
}
