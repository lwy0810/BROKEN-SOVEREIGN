using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 화면 상단 가운데 대상 체력 게이지: 이름, 막대, 남은 체력 비율(%).
    /// 대상이 없으면 숨깁니다.
    /// </summary>
    public class TargetFrameView : MonoBehaviour
    {
        private Image _background;
        private Outline _frame;
        private Image _fill;
        private Text _nameText;
        private Text _percentText;
        private Health _target;

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

            _percentText = CreateText("Percent");
            _percentText.fontStyle = FontStyle.Bold;
            HudGraphics.Stretch(_percentText.rectTransform);

            // 이름은 막대 바로 위
            _nameText = CreateText("Name");
            RectTransform nameRect = _nameText.rectTransform;
            nameRect.anchorMin = new Vector2(0f, 1f);
            nameRect.anchorMax = new Vector2(1f, 1f);
            nameRect.pivot = new Vector2(0.5f, 0f);

            SetTarget(null);
        }

        public void ApplyStyle(TargetSettings settings)
        {
            _background.color = settings.BarBackgroundColor;
            _frame.effectColor = settings.FrameColor;
            _fill.color = settings.BarColor;
            _nameText.fontSize = settings.NameFontSize;
            _percentText.fontSize = settings.PercentFontSize;

            RectTransform nameRect = _nameText.rectTransform;
            nameRect.sizeDelta = new Vector2(0f, settings.NameFontSize + 8f);
            nameRect.anchoredPosition = new Vector2(0f, 4f);
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
            _fill.fillAmount = ratio;

            // 살아 있는 동안은 0%로 보이지 않도록 올림
            _percentText.text = $"{Mathf.CeilToInt(ratio * 100f)}%";
        }

        private Text CreateText(string textName)
        {
            Text text = HudGraphics.CreateRect(textName, transform).gameObject.AddComponent<Text>();
            text.font = HudGraphics.Font;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1f, -1f);
            return text;
        }
    }
}
