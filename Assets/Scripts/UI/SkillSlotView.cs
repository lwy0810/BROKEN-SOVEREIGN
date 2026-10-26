using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 기술 슬롯 하나: 키 표시, 재사용 대기 원형 막과 남은 초, 미개방 잠김, 남은 횟수(물약).
    /// 10/21 B안: 금속 테두리 칸(UiSprites.Tile) + 가운데 문양(기술 아이콘 도입 전), 기술 이름은 칸 위.
    /// 10/26: 재사용 대기는 숫자 없이 검은 반투명 막이 시계 방향으로 걷힘. 대기가 끝나는 순간 테두리가 한 번 반짝임.
    /// </summary>
    public class SkillSlotView : MonoBehaviour
    {
        private const float INSET = 8f;
        private const float FLASH_TIME = 0.4f;
        /// <summary>이보다 짧은 재사용 대기는 반짝이지 않음 (초)</summary>
        private const float FLASH_MIN_COOLDOWN = 0.8f;

        private Image _flash;
        private float _flashStart = float.NegativeInfinity;
        private bool _wasCooling;
        private float _lastDuration;

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

            // 대기 끝 반짝임: 칸 테두리와 같은 모양을 밝게 덮었다가 사라짐
            _flash = CreateImage("ReadyFlash");
            _flash.sprite = ItemVisuals.Frame;   // 테두리만 있는 칸 틀
            _flash.type = Image.Type.Sliced;
            _flash.color = Color.clear;
            _flash.gameObject.AddComponent<Outline>().effectColor = Color.clear;

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
            _cooldown.color = new Color(0.42f, 0.43f, 0.46f, 0.72f);   // 10/26: 회색 반투명 막

            // 남은 비율만큼 막을 그림. 막을 반시계로 그리면 걷히는 쪽(밝아지는 영역)이 시계 방향으로 진행 (10/26 시계 방향 고정)
            _cooldown.fillClockwise = false;
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
            _cooldownText.text = string.Empty;   // 10/26: 남은 초 숫자 표시 안 함

            if (coolingDown)
            {
                _lastDuration = state.CooldownDuration;
            }
            else if (_wasCooling && _lastDuration >= FLASH_MIN_COOLDOWN)
            {
                _flashStart = Time.time;
            }

            _wasCooling = coolingDown;
        }

        private void Update()
        {
            float t = (Time.time - _flashStart) / FLASH_TIME;

            if (t < 0f || t > 1f)
            {
                if (_flash != null && _flash.color.a > 0f)
                {
                    _flash.color = Color.clear;
                    _flash.GetComponent<Outline>().effectColor = Color.clear;
                    _flash.rectTransform.localScale = Vector3.one;
                }

                return;
            }

            // 빠르게 밝아졌다가 서서히 사라짐 + 살짝 커졌다 돌아옴
            float alpha = t < 0.2f ? t / 0.2f : 1f - (t - 0.2f) / 0.8f;
            Color gold = UiTheme.P.Title;
            _flash.color = new Color(1f, 0.95f, 0.8f, alpha * 0.85f);
            _flash.GetComponent<Outline>().effectColor = new Color(gold.r, gold.g, gold.b, alpha);
            _flash.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(t * Mathf.PI));
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
