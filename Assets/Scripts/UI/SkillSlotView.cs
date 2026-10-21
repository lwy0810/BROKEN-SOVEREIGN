using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 슬롯 하나: 키 표시, 재사용 대기 원형 막과 남은 초, 미개방 잠김, 남은 횟수(물약).
    /// 10/21 B안: 금속 테두리 칸(UiSprites.Tile) + 가운데 문양(기술 아이콘 도입 전), 기술 이름은 칸 위.
    /// </summary>
    public class SkillSlotView : MonoBehaviour
    {
        private const float INSET = 8f;

        private Image _background;
        private Image _icon;
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
            _background.sprite = UiSprites.Tile;
            _background.type = Image.Type.Sliced;
            _background.raycastTarget = false;

            _icon = CreateImage("Icon");
            _icon.sprite = UiSprites.Rune;
            _icon.rectTransform.offsetMin = new Vector2(20f, 20f);
            _icon.rectTransform.offsetMax = new Vector2(-20f, -20f);

            _cooldown = CreateImage("Cooldown");
            _cooldown.type = Image.Type.Filled;
            _cooldown.fillMethod = Image.FillMethod.Radial360;
            _cooldown.fillOrigin = (int)Image.Origin360.Top;
            _cooldown.fillClockwise = false;
            _cooldown.fillAmount = 0f;

            _locked = CreateImage("Locked");
            _locked.enabled = false;

            _cooldownText = CreateText("CooldownTime", TextAnchor.MiddleCenter);
            UiTheme.ApplyBody(_cooldownText, true);
            _keyText = CreateText("Key", TextAnchor.LowerRight);
            UiTheme.ApplyBody(_keyText, true);
            _keyText.text = key;
            _countText = CreateText("Count", TextAnchor.UpperLeft);
            UiTheme.ApplyBody(_countText, true);

            // 기술 이름: 슬롯 바로 위 (기술 바 판 위쪽 여백 안)
            _nameText = CreateText("SkillName", TextAnchor.LowerCenter);
            _nameText.color = UiTheme.P.TextDim;
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);
            _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _nameText.text = string.Empty;

            // 10/21: 기술 바에서는 기술 이름 표시 안 함 (기술 창에서 확인)
            _nameText.gameObject.SetActive(false);
        }

        /// <summary>장착 기술 이름 표시. null·빈 문자열이면 숨김</summary>
        /// <summary>기술 아이콘 (SkillIcons, 없으면 기본 문양)</summary>
        public void SetIcon(Sprite icon)
        {
            if (_icon.sprite != icon)
            {
                _icon.sprite = icon;
            }
        }

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
            nameRect.anchoredPosition = new Vector2(0f, settings.SkillNameGap + 2f);
        }

        public void ApplyStyle(SkillBarSettings settings)
        {
            // 색은 테마(UiTheme), 크기·방향만 설정값 사용
            _background.color = Color.white;
            _cooldown.color = new Color(0f, 0f, 0f, 0.62f);

            // 남은 비율만큼 막을 그림. 막을 반시계로 그리면 걷히는 쪽(밝아지는 영역)이 시계 방향으로 진행
            _cooldown.fillClockwise = !settings.CooldownSweepClockwise;
            _locked.color = new Color(0f, 0f, 0f, 0.6f);
            Vector2 inner = new Vector2(INSET, INSET);
            _cooldown.rectTransform.offsetMin = inner;
            _cooldown.rectTransform.offsetMax = -inner;
            _locked.rectTransform.offsetMin = inner;
            _locked.rectTransform.offsetMax = -inner;
            _keyText.fontSize = settings.KeyFontSize;
            _countText.fontSize = settings.KeyFontSize;
            _cooldownText.fontSize = settings.CooldownFontSize;

            Vector2 inset = new Vector2(INSET, INSET - 2f);
            _keyText.rectTransform.offsetMin = inset;
            _keyText.rectTransform.offsetMax = -inset;
            _countText.rectTransform.offsetMin = inset;
            _countText.rectTransform.offsetMax = -inset;
        }

        public void SetState(SkillSlotState state)
        {
            _locked.enabled = !state.IsUnlocked;
            _icon.enabled = state.IsUnlocked;
            _keyText.color = state.IsUnlocked ? UiTheme.P.Title : UiTheme.P.TextDisabled;
            _nameText.color = state.IsUnlocked ? UiTheme.P.TextDim : UiTheme.P.TextDisabled;

            bool coolingDown = state.IsUnlocked && state.CooldownRemaining > 0.01f;
            _cooldown.fillAmount = coolingDown ? state.CooldownRatio : 0f;
            _cooldownText.text = coolingDown && state.CooldownRemaining >= 0.1f
                ? (state.CooldownRemaining >= 1f ? $"{state.CooldownRemaining:0}" : $"{state.CooldownRemaining:0.0}")
                : string.Empty;
        }

        public void SetCount(int count)
        {
            _countText.text = $"x{count}";
            _countText.color = count > 0 ? UiTheme.P.Text : UiTheme.P.Taken;
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
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, 0.85f);
            shadow.effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
