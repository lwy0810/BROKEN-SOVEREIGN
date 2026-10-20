using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 화면 상단 가운데 대상 체력 게이지: 이름, 막대, 남은 체력 비율(%).
    /// 대상에 표시용 버팀 게이지(PoiseReceiver, 정예·보스)가 있으면 체력 막대 아래 버팀 막대, 무력화 중이면 "무력화" 표시.
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
        private RectTransform _poiseBar;
        private Image _poiseFill;

        private const float POISE_BAR_HEIGHT = 6f;
        private const float POISE_BAR_GAP = 3f;
        private static readonly Color POISE_COLOR = new Color(0.95f, 0.78f, 0.3f, 1f);
        private static readonly Color STAGGER_COLOR = new Color(1f, 0.45f, 0.15f, 1f);

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

            // 버팀 막대: 체력 막대 바로 아래 (야로미르 분쇄, 정예·보스만)
            _poiseBar = HudGraphics.CreateRect("PoiseBar", transform);
            _poiseBar.anchorMin = new Vector2(0f, 0f);
            _poiseBar.anchorMax = new Vector2(1f, 0f);
            _poiseBar.pivot = new Vector2(0.5f, 1f);
            _poiseBar.sizeDelta = new Vector2(0f, POISE_BAR_HEIGHT);
            _poiseBar.anchoredPosition = new Vector2(0f, -POISE_BAR_GAP);
            Image poiseBackground = _poiseBar.gameObject.AddComponent<Image>();
            poiseBackground.sprite = HudGraphics.WhiteSprite;
            poiseBackground.color = new Color(0f, 0f, 0f, 0.6f);
            poiseBackground.raycastTarget = false;

            _poiseFill = HudGraphics.CreateRect("Fill", _poiseBar).gameObject.AddComponent<Image>();
            HudGraphics.Stretch(_poiseFill.rectTransform);
            _poiseFill.sprite = HudGraphics.WhiteSprite;
            _poiseFill.type = Image.Type.Filled;
            _poiseFill.fillMethod = Image.FillMethod.Horizontal;
            _poiseFill.fillOrigin = (int)Image.OriginHorizontal.Left;
            _poiseFill.raycastTarget = false;
            _poiseBar.gameObject.SetActive(false);

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
            string percent = $"{Mathf.CeilToInt(ratio * 100f)}%";

            // 버팀 게이지 (분쇄 규칙이 첫 피해 때 붙이므로 매번 조회)
            PoiseReceiver poise = _target.GetComponent<PoiseReceiver>();
            bool staggered = poise != null && poise.IsStaggered;
            bool showPoise = poise != null && poise.ShowBar;
            _poiseBar.gameObject.SetActive(showPoise);

            if (showPoise)
            {
                _poiseFill.fillAmount = staggered ? 1f : poise.Ratio;
                _poiseFill.color = staggered ? STAGGER_COLOR : POISE_COLOR;
            }

            _percentText.text = staggered ? $"{percent}  무력화" : percent;
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
