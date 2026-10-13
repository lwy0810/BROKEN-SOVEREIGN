using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 바 위 직업 게이지 막대 (신앙 등). 평소: "신앙 37 / 100", 버프 중: "충만 6.2초" + 밝게 깜빡임.
    /// 직업 게이지가 없으면(무직) 숨깁니다.
    /// </summary>
    public class JobGaugeView : MonoBehaviour
    {
        private Image _background;
        private Outline _frame;
        private Image _fill;
        private Text _label;
        private JobGaugeSettings _settings;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize()
        {
            _background = gameObject.AddComponent<Image>();
            _background.sprite = HudGraphics.WhiteSprite;
            _background.raycastTarget = false;
            _frame = gameObject.AddComponent<Outline>();
            _frame.effectDistance = new Vector2(2f, -2f);

            _fill = HudGraphics.CreateRect("Fill", transform).gameObject.AddComponent<Image>();
            HudGraphics.Stretch(_fill.rectTransform);
            _fill.sprite = HudGraphics.WhiteSprite;
            _fill.type = Image.Type.Filled;
            _fill.fillMethod = Image.FillMethod.Horizontal;
            _fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _fill.raycastTarget = false;

            _label = HudGraphics.CreateRect("Label", transform).gameObject.AddComponent<Text>();
            HudGraphics.Stretch(_label.rectTransform);
            _label.font = HudGraphics.Font;
            _label.alignment = TextAnchor.MiddleCenter;
            _label.fontStyle = FontStyle.Bold;
            _label.color = Color.white;
            _label.raycastTarget = false;
            _label.horizontalOverflow = HorizontalWrapMode.Overflow;
            _label.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
        }

        public void ApplyStyle(JobGaugeSettings settings)
        {
            _settings = settings;
            _background.color = settings.BackgroundColor;
            _frame.effectColor = settings.FrameColor;
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

            _fill.fillAmount = gauge.Max > 0f ? Mathf.Clamp01(gauge.Current / gauge.Max) : 0f;

            if (gauge.IsBuffActive)
            {
                // 버프 중: 밝게 깜빡이며 남은 시간 표시
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * _settings.BuffPulseSpeed);
                _fill.color = Color.Lerp(gauge.DisplayColor, Color.white, pulse * _settings.BuffPulseStrength);
                _frame.effectColor = Color.Lerp(_settings.FrameColor, gauge.DisplayColor, pulse);
                _label.text = $"{gauge.BuffName} {gauge.BuffRemaining:0.0}초";
            }
            else
            {
                _fill.color = gauge.DisplayColor;
                _frame.effectColor = _settings.FrameColor;
                _label.text = $"{gauge.GaugeName} {Mathf.FloorToInt(gauge.Current)} / {Mathf.CeilToInt(gauge.Max)}";
            }
        }
    }
}
