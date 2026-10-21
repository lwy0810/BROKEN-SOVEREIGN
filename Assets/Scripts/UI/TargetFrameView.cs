using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 화면 상단 가운데 대상 체력 게이지: 금속판 위 이름, 알약형 막대, 남은 체력 비율(%).
    /// 대상에 표시용 버팀 게이지(PoiseReceiver, 정예·보스)가 있으면 체력 막대 아래 버팀 막대, 무력화 중이면 "무력화" 표시.
    /// 대상이 없으면 숨깁니다. 10/21 B안 스타일 (UiTheme).
    /// </summary>
    public class TargetFrameView : MonoBehaviour
    {
        private const float POISE_BAR_HEIGHT = 7f;
        private const float POISE_BAR_GAP = 5f;

        private Image _plate;
        private UiBar _bar;
        private UiBar _poise;
        private Text _nameText;
        private Text _percentText;
        private Health _target;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize()
        {
            // 바깥 금속판 (이름·버팀 막대 자리 포함, 막대보다 먼저 = 뒤에 그림)
            _plate = UiKit.Plate("Plate", transform);
            UiKit.Surround(_plate.rectTransform, 20f, 26f, 20f, 34f);

            _bar = UiKit.Bar("Bar", transform, UiTheme.P.Hp, UiTheme.P.HpDark);
            HudGraphics.Stretch(_bar.Rect);
            _percentText = _bar.EnableText(13);

            _nameText = UiKit.Title("Name", transform, 18, UiTheme.P.Text, TextAnchor.LowerCenter);
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);

            // 버팀 막대: 체력 막대 바로 아래 가운데 (야로미르 분쇄, 정예·보스만)
            _poise = UiKit.Bar("PoiseBar", transform, UiTheme.P.Poise, UiTheme.P.ResourceDark);
            RectTransform poiseRect = _poise.Rect;
            poiseRect.anchorMin = new Vector2(0.2f, 0f);
            poiseRect.anchorMax = new Vector2(0.8f, 0f);
            poiseRect.pivot = new Vector2(0.5f, 1f);
            poiseRect.sizeDelta = new Vector2(0f, POISE_BAR_HEIGHT);
            poiseRect.anchoredPosition = new Vector2(0f, -POISE_BAR_GAP);
            poiseRect.gameObject.SetActive(false);

            SetTarget(null);
        }

        public void ApplyStyle(TargetSettings settings)
        {
            // 색은 테마, 글자 크기만 설정값 사용
            _nameText.fontSize = settings.NameFontSize;
            _percentText.fontSize = settings.PercentFontSize - 2;

            RectTransform nameRect = _nameText.rectTransform;
            nameRect.sizeDelta = new Vector2(0f, settings.NameFontSize + 8f);
            nameRect.anchoredPosition = new Vector2(0f, 5f);
        }

        public void SetTarget(Health target)
        {
            _target = target;
            gameObject.SetActive(target != null);

            if (target != null)
            {
                _nameText.text = target.gameObject.name;
                Refresh();
            }
        }

        private void Update()
        {
            // 대상 오브젝트가 파괴되면 숨김
            if (_target == null)
            {
                gameObject.SetActive(false);
                return;
            }

            Refresh();
        }

        private void Refresh()
        {
            float ratio = _target.Max > 0f ? Mathf.Clamp01(_target.Current / _target.Max) : 0f;
            _bar.SetRatio(ratio);

            // 살아 있는 동안은 0%로 보이지 않도록 올림
            string percent = $"{Mathf.CeilToInt(ratio * 100f)}%";

            // 버팀 게이지 (분쇄 규칙이 첫 피해 때 붙이므로 매번 조회)
            PoiseReceiver poise = _target.GetComponent<PoiseReceiver>();
            bool staggered = poise != null && poise.IsStaggered;
            bool showPoise = poise != null && poise.ShowBar;
            _poise.Rect.gameObject.SetActive(showPoise);

            if (showPoise)
            {
                _poise.SetRatio(staggered ? 1f : poise.Ratio);
                Color color = staggered ? UiTheme.P.Stagger : UiTheme.P.Poise;
                _poise.SetColor(color, color);
            }

            _percentText.text = staggered ? $"{percent}  무력화" : percent;
        }
    }
}
