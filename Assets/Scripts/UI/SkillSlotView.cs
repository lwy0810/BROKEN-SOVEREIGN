using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 슬롯 하나: 키 표시, 재사용 대기 원형 막과 남은 초, 미개방 잠김, 남은 횟수(물약).
    /// </summary>
    public class SkillSlotView : MonoBehaviour
    {
        private Image _background;
        private Outline _frame;
        private Image _cooldown;
        private Image _locked;
        private Text _keyText;
        private Text _cooldownText;
        private Text _countText;
        private Text _nameText;
        private string _skillName;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize(string key)
        {
            _background = gameObject.AddComponent<Image>();
            _background.sprite = HudGraphics.WhiteSprite;
            _background.raycastTarget = false;
            _frame = gameObject.AddComponent<Outline>();
            _frame.effectDistance = new Vector2(2f, -2f);

            _cooldown = CreateImage("Cooldown");
            _cooldown.type = Image.Type.Filled;
            _cooldown.fillMethod = Image.FillMethod.Radial360;
            _cooldown.fillOrigin = (int)Image.Origin360.Top;
            _cooldown.fillClockwise = false;
            _cooldown.fillAmount = 0f;

            _locked = CreateImage("Locked");
            _locked.enabled = false;

            _cooldownText = CreateText("CooldownTime", TextAnchor.MiddleCenter);
            _keyText = CreateText("Key", TextAnchor.LowerRight);
            _keyText.text = key;
            _countText = CreateText("Count", TextAnchor.UpperLeft);

            // 기술 이름: 슬롯 바로 아래 (슬롯 밖)
            _nameText = CreateText("SkillName", TextAnchor.UpperCenter);
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 0f);
            nameRect.anchorMax = new Vector2(1f, 0f);
            nameRect.pivot = new Vector2(0.5f, 1f);
            _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameText.text = string.Empty;
        }

        /// <summary>장착 기술 이름 표시. null·빈 문자열이면 숨김</summary>
        public void SetSkillName(string skillName)
        {
            skillName ??= string.Empty;

            if (skillName == _skillName)
            {
                return;
            }

            _skillName = skillName;
            _nameText.text = skillName;
        }

        public void ApplyNameStyle(JobDisplaySettings settings)
        {
            _nameText.fontSize = settings.SkillNameFontSize;
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.sizeDelta = new Vector2(0f, settings.SkillNameFontSize + 6f);
            nameRect.anchoredPosition = new Vector2(0f, -settings.SkillNameGap);
        }

        public void ApplyStyle(SkillBarSettings settings)
        {
            _background.color = settings.SlotColor;
            _frame.effectColor = settings.FrameColor;
            _cooldown.color = settings.CooldownColor;

            // 남은 비율만큼 막을 그림. 막을 반시계로 그리면 걷히는 쪽(밝아지는 영역)이 시계 방향으로 진행
            _cooldown.fillClockwise = !settings.CooldownSweepClockwise;
            _locked.color = settings.LockedColor;
            _keyText.fontSize = settings.KeyFontSize;
            _countText.fontSize = settings.KeyFontSize;
            _cooldownText.fontSize = settings.CooldownFontSize;

            Vector2 inset = new Vector2(6f, 4f);
            _keyText.rectTransform.offsetMin = inset;
            _keyText.rectTransform.offsetMax = -inset;
            _countText.rectTransform.offsetMin = inset;
            _countText.rectTransform.offsetMax = -inset;
        }

        public void SetState(SkillSlotState state)
        {
            _locked.enabled = !state.IsUnlocked;
            _keyText.color = state.IsUnlocked ? Color.white : new Color(1f, 1f, 1f, 0.35f);

            bool coolingDown = state.IsUnlocked && state.CooldownRemaining > 0.01f;
            _cooldown.fillAmount = coolingDown ? state.CooldownRatio : 0f;
            _cooldownText.text = coolingDown && state.CooldownRemaining >= 0.1f
                ? (state.CooldownRemaining >= 1f ? $"{state.CooldownRemaining:0}" : $"{state.CooldownRemaining:0.0}")
                : string.Empty;
        }

        public void SetCount(int count)
        {
            _countText.text = $"x{count}";
            _countText.color = count > 0 ? Color.white : new Color(1f, 0.4f, 0.4f);
        }

        private Image CreateImage(string imageName)
        {
            Image image = HudGraphics.CreateRect(imageName, transform).gameObject.AddComponent<Image>();
            HudGraphics.Stretch(image.rectTransform);
            image.sprite = HudGraphics.WhiteSprite;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string textName, TextAnchor alignment)
        {
            Text text = HudGraphics.CreateRect(textName, transform).gameObject.AddComponent<Text>();
            HudGraphics.Stretch(text.rectTransform);
            text.font = HudGraphics.Font;
            text.alignment = alignment;
            text.color = Color.white;
            text.raycastTarget = false;
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
