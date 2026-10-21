using System;
using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 직업 선택 화면 (GDD 4.1): 1차 직업 카드 4장, 프로토타입은 성기사만 선택 가능.
    /// 열려 있는 동안 게임 일시정지 + 전투 입력 차단. HudView가 생성하고 PlayerProgression의 요청으로 엽니다.
    /// EventSystem 없이 마우스 위치로 직접 판정합니다 (HUD에 버튼 UI가 없어 EventSystem 미도입).
    /// </summary>
    public class JobSelectView : MonoBehaviour
    {
        private struct CardInfo
        {
            public string Name;
            public string Resource;
            public string Description;
            public bool Selectable;
        }

        private static readonly CardInfo[] CARDS =
        {
            new CardInfo { Name = "무사", Resource = "투기", Description = "창과 검으로\n싸우는 근접 전사", Selectable = false },
            new CardInfo { Name = "성기사", Resource = "신앙", Description = "맹세와 신앙으로\n싸우는 수호자", Selectable = true },
            new CardInfo { Name = "궁사", Resource = "집중", Description = "멀리서 적을\n꿰뚫는 사수", Selectable = false },
            new CardInfo { Name = "마법사", Resource = "마나", Description = "원소와 지식을\n다루는 술사", Selectable = false }
        };

        private static readonly Vector2 CARD_SIZE = new Vector2(260f, 340f);
        private const float CARD_SPACING = 32f;

        private static Color DIM_COLOR => new Color(0f, 0f, 0f, 0.8f);
        private static Color CARD_COLOR => Color.white;
        private static Color CARD_HOVER_COLOR => new Color(1f, 0.93f, 0.82f);
        private static Color CARD_LOCKED_COLOR => new Color(0.6f, 0.6f, 0.62f);
        private static Color GOLD => UiTheme.P.Title;
        private static Color MUTED => UiTheme.P.TextDisabled;

        private const float HOVER_SCALE = 1.06f;
        private const float CHOSEN_SCALE = 1.12f;
        private const float SCALE_SPEED = 12f;
        private const float GLOW_PADDING = 48f;
        private const float CHOOSE_DELAY = 0.35f;

        private RectTransform[] _cardRects;
        private Image[] _cardImages;
        private Image[] _glows;
        private Outline[] _outlines;
        private Text[] _footers;
        private int _chosenIndex = -1;
        private float _chosenTime;
        private PlayerJob _job;
        private float _previousTimeScale = 1f;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>직업을 선택했을 때 (선택한 직업)</summary>
        public event Action<JobDefinition> JobChosen;

        public void Initialize(PlayerJob job)
        {
            _job = job;
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            Image dim = gameObject.AddComponent<Image>();
            dim.sprite = HudGraphics.WhiteSprite;
            dim.color = DIM_COLOR;

            Text title = CreateText("Title", root, 44, GOLD, FontStyle.Bold);
            title.text = "격의 잔재가 길을 묻는다 — 직업 선택";
            UiTheme.ApplyTitle(title);
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, CARD_SIZE.y * 0.5f + 80f),
                new Vector2(1200f, 60f));

            _cardRects = new RectTransform[CARDS.Length];
            _cardImages = new Image[CARDS.Length];
            _glows = new Image[CARDS.Length];
            _outlines = new Outline[CARDS.Length];
            _footers = new Text[CARDS.Length];
            float totalWidth = CARDS.Length * CARD_SIZE.x + (CARDS.Length - 1) * CARD_SPACING;

            for (int i = 0; i < CARDS.Length; i++)
            {
                float x = -totalWidth * 0.5f + CARD_SIZE.x * 0.5f + i * (CARD_SIZE.x + CARD_SPACING);
                BuildCard(i, root, x);
            }

            gameObject.SetActive(false);
        }

        public void Open()
        {
            if (IsOpen)
            {
                return;
            }

            gameObject.SetActive(true);
            _chosenIndex = -1;

            for (int i = 0; i < CARDS.Length; i++)
            {
                _cardRects[i].localScale = Vector3.one;
            }

            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            GameplayInput.IsBlocked = true;
        }

        private void Close()
        {
            gameObject.SetActive(false);
            Time.timeScale = _previousTimeScale;
            GameplayInput.IsBlocked = false;
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                Time.timeScale = _previousTimeScale;
                GameplayInput.IsBlocked = false;
            }
        }

        // 일시정지 중이므로 unscaled 시간 사용. 마우스 위치로 강조·선택 판정
        private void Update()
        {
            float now = Time.unscaledTime;
            float step = 1f - Mathf.Exp(-SCALE_SPEED * Time.unscaledDeltaTime);

            // 선택 연출: 카드가 커지며 빛난 뒤 닫힘
            if (_chosenIndex >= 0)
            {
                float t = Mathf.Clamp01((now - _chosenTime) / CHOOSE_DELAY);
                AnimateCard(_chosenIndex, CHOSEN_SCALE, step);
                _cardImages[_chosenIndex].color = Color.Lerp(Color.white, CARD_HOVER_COLOR, t);
                _glows[_chosenIndex].color = new Color(GOLD.r, GOLD.g, GOLD.b, 1f - t * 0.5f);

                if (t >= 1f)
                {
                    Choose();
                }

                return;
            }

            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);

            for (int i = 0; i < CARDS.Length; i++)
            {
                bool hover = RectTransformUtility.RectangleContainsScreenPoint(_cardRects[i], pointer, null);
                bool selectable = CARDS[i].Selectable;

                if (!selectable)
                {
                    // 잠긴 카드: 올려도 살짝만 반응 (선택 불가 표시)
                    _cardImages[i].color = CARD_LOCKED_COLOR;
                    _footers[i].color = hover ? UiTheme.P.Danger : MUTED;
                    AnimateCard(i, 1f, step);
                    continue;
                }

                // 선택 가능한 카드: 확대 + 금빛 테두리 맥동 + 바깥 빛
                float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);
                _cardImages[i].color = hover ? CARD_HOVER_COLOR : CARD_COLOR;
                _outlines[i].effectDistance = hover ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
                _outlines[i].effectColor = hover ? Color.Lerp(GOLD, Color.white, pulse * 0.6f) : Color.clear;
                _glows[i].color = hover ? new Color(GOLD.r, GOLD.g, GOLD.b, 0.25f + pulse * 0.35f) : Color.clear;
                _footers[i].color = hover ? Color.Lerp(GOLD, Color.white, pulse) : GOLD;
                AnimateCard(i, hover ? HOVER_SCALE : 1f, step);

                if (hover && mouse != null && mouse.leftButton.wasPressedThisFrame)
                {
                    _chosenIndex = i;
                    _chosenTime = now;
                    return;
                }
            }
        }

        private void AnimateCard(int index, float targetScale, float step)
        {
            Vector3 target = Vector3.one * targetScale;
            _cardRects[index].localScale = Vector3.Lerp(_cardRects[index].localScale, target, step);
        }

        // 프로토타입: 선택 가능한 직업은 성기사 하나 (PlayerJob의 1차 직업)
        private void Choose()
        {
            JobDefinition job = _job != null ? _job.SelectableJob : null;
            Close();

            if (job == null)
            {
                Debug.LogWarning("JobSelectView: PlayerJob에 선택 가능한 직업이 없습니다.", this);
                return;
            }

            _job.ChooseJob(job);
            JobChosen?.Invoke(job);
        }

        private void BuildCard(int index, RectTransform root, float x)
        {
            CardInfo info = CARDS[index];
            RectTransform card = HudGraphics.CreateRect($"Card_{info.Name}", root);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(x, -20f), CARD_SIZE);

            // 바깥 빛: 카드 뒤에 조금 더 큰 판 (강조 시에만 보임)
            RectTransform glow = HudGraphics.CreateRect("Glow", card);
            glow.anchorMin = Vector2.zero;
            glow.anchorMax = Vector2.one;
            glow.offsetMin = new Vector2(-GLOW_PADDING, -GLOW_PADDING);
            glow.offsetMax = new Vector2(GLOW_PADDING, GLOW_PADDING);
            Image glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = UiSprites.Glow;
            glowImage.color = Color.clear;
            glowImage.raycastTarget = false;

            // 카드 배경 (빛 위에 그리도록 별도 자식)
            RectTransform face = HudGraphics.CreateRect("Face", card);
            HudGraphics.Stretch(face);
            Image image = face.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Plate;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;
            Outline outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = Color.clear;
            outline.effectDistance = new Vector2(2f, -2f);

            Color textColor = info.Selectable ? Color.white : MUTED;
            Text name = CreateText("Name", card, 40, info.Selectable ? GOLD : MUTED, FontStyle.Bold);
            name.text = info.Name;
            UiTheme.ApplyTitle(name);
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(CARD_SIZE.x, 50f));

            Text resource = CreateText("Resource", card, 22, textColor, FontStyle.Normal);
            resource.text = $"자원: {info.Resource}";
            Place(resource.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -100f), new Vector2(CARD_SIZE.x, 30f));

            Text description = CreateText("Description", card, 22, textColor, FontStyle.Normal);
            description.text = info.Description;
            Place(description.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -10f),
                new Vector2(CARD_SIZE.x - 30f, 90f));

            Text footer = CreateText("Footer", card, 22, info.Selectable ? GOLD : MUTED, FontStyle.Bold);
            footer.text = info.Selectable ? "클릭하여 선택" : "잠김";
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(CARD_SIZE.x, 30f));

            _cardRects[index] = card;
            _cardImages[index] = image;
            _glows[index] = glowImage;
            _outlines[index] = outline;
            _footers[index] = footer;
        }

        private static Text CreateText(string textName, Transform parent, int fontSize, Color color, FontStyle style)
        {
            Text text = HudGraphics.CreateRect(textName, parent).gameObject.AddComponent<Text>();
            text.font = HudGraphics.Font;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;

            if (style == FontStyle.Bold)
            {
                UiTheme.ApplyBody(text, true);
            }
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }

        private static void Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }
    }
}
