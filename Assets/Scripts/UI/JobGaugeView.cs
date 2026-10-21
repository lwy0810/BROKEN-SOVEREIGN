using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 체력 막대 위 직업 게이지 막대 (신앙 등). 평소: "신앙 37 / 100", 버프 중: "충만 6.2초" + 밝게 깜빡임.
    /// 직업 게이지가 없으면(무직) 숨깁니다. 10/21 B안: 알약형 막대(UiBar).
    /// </summary>
    public class JobGaugeView : MonoBehaviour
    {
        private UiBar _bar;
        private Text _label;
        private JobGaugeSettings _settings;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize()
        {
            _bar = UiKit.Bar("Gauge", transform, UiTheme.P.Resource, UiTheme.P.ResourceDark);
            HudGraphics.Stretch(_bar.Rect);
            _label = _bar.EnableText(13);
        }

        public void ApplyStyle(JobGaugeSettings settings)
        {
            _settings = settings;
            _label.fontSize = settings.FontSize;
        }

        public void SetGauge(IJobGauge gauge)
        {
            bool visible = gauge != null;

            if (gameObject.activeSelf != visible)
            {
                gameObject.SetActive(visible);
            }

            if (!visible)
            {
                return;
            }

            _bar.SetRatio(gauge.Max > 0f ? Mathf.Clamp01(gauge.Current / gauge.Max) : 0f);

            if (gauge.IsBuffActive)
            {
                // 버프 중: 밝게 깜빡이며 남은 시간 표시
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _settings.BuffPulseSpeed);
                Color bright = Color.Lerp(gauge.DisplayColor, Color.white, pulse * _settings.BuffPulseStrength);
                _bar.SetColor(bright, bright);
                _bar.Frame.color = Color.Lerp(Color.white, UiTheme.P.Title, pulse);
                _label.text = $"{gauge.BuffName} {gauge.BuffRemaining:0.0}초";
            }
            else
            {
                _bar.SetColor(gauge.DisplayColor, gauge.DisplayColor * 0.6f);
                _bar.Frame.color = Color.white;
                _label.text = $"{gauge.GaugeName} {Mathf.FloorToInt(gauge.Current)} / {Mathf.CeilToInt(gauge.Max)}";
            }
        }
    }
}
