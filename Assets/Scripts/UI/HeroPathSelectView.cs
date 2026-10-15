using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 영웅의 길 선택 화면 (GDD 5장): 반신 돌파 시 직업의 영웅 후보 카드를 보여 주고 하나를 고릅니다.
    /// 영웅 이름은 이 화면에서 처음 공개합니다. 열려 있는 동안 게임 일시정지 + 전투 입력 차단.
    /// HudView가 생성하고 PlayerProgression의 요청으로 엽니다. 직업 선택 화면(JobSelectView)과 같은 방식으로 마우스 위치를 직접 판정합니다.
    /// </summary>
    public class HeroPathSelectView : MonoBehaviour
    {
        private static readonly Vector2 CARD_SIZE = new Vector2(340f, 420f);
        private const float CARD_SPACING = 48f;

        private static readonly Color DIM_COLOR = new Color(0f, 0f, 0f, 0.8f);
        private static readonly Color CARD_COLOR = new Color(0.1f, 0.09f, 0.12f, 0.95f);
        private static readonly Color CARD_HOVER_COLOR = new Color(0.22f, 0.18f, 0.1f, 0.98f);
        private static readonly Color GOLD = new Color(0.95f, 0.8f, 0.45f);
        private static readonly Color SUBTLE = new Color(0.85f, 0.82f, 0.75f);

        private const float HOVER_SCALE = 1.05f;
        private const float CHOSEN_SCALE = 1.1f;
        private const float SCALE_SPEED = 12f;
        private const float GLOW_PADDING = 16f;
        private const float CHOOSE_DELAY = 0.45f;

        private readonly List<RectTransform> _cardRects = new List<RectTransform>();
        private readonly List<Image> _cardImages = new List<Image>();
        private readonly List<Image> _glows = new List<Image>();
        private readonly List<Outline> _outlines = new List<Outline>();
        private readonly List<Text> _footers = new List<Text>();
        private readonly List<HeroPathDefinition> _paths = new List<HeroPathDefinition>();

        private RectTransform _cardRoot;
        private HeroPathController _hero;
        private int _chosenIndex = -1;
        private float _chosenTime;
        private float _previousTimeScale = 1f;

        public bool IsOpen => gameObject.activeSelf;

        /// <summary>영웅의 길을 선택했을 때</summary>
        public event Action<HeroPathDefinition> PathChosen;

        public void Initialize(HeroPathController hero)
        {
            _hero = hero;
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            Image dim = gameObject.AddComponent<Image>();
            dim.sprite = HudGraphics.WhiteSprite;
            dim.color = DIM_COLOR;

            Text title = CreateText("Title", root, 44, GOLD, FontStyle.Bold);
            title.text = "반신의 격 — 걸어갈 영웅의 길을 고르라";
            Place(title.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, CARD_SIZE.y * 0.5f + 80f),
                new Vector2(1200f, 60f));

            _cardRoot = HudGraphics.CreateRect("Cards", root);
            HudGraphics.Stretch(_cardRoot);
            gameObject.SetActive(false);
        }

        /// <summary>후보 카드를 만들고 엽니다. 이미 열려 있거나 후보가 없으면 무시</summary>
        public void Open(IReadOnlyList<HeroPathDefinition> paths)
        {
            if (IsOpen || paths == null || paths.Count == 0 || _hero == null || _hero.HasPath)
            {
                return;
            }

            BuildCards(paths);
            gameObject.SetActive(true);
            _chosenIndex = -1;

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

        // 일시정지 중이므로 unscaled 시간 사용
        private void Update()
        {
            float now = Time.unscaledTime;
            float step = 1f - Mathf.Exp(-SCALE_SPEED * Time.unscaledDeltaTime);

            if (_chosenIndex >= 0)
            {
                float t = Mathf.Clamp01((now - _chosenTime) / CHOOSE_DELAY);
                AnimateCard(_chosenIndex, CHOSEN_SCALE, step);
                _cardImages[_chosenIndex].color = Color.Lerp(Color.white, CARD_HOVER_COLOR, t);
                _glows[_chosenIndex].color = new Color(GOLD.r, GOLD.g, GOLD.b, 1f - t * 0.5f);

                if (t >= 1f)
                {
                    Choose(_paths[_chosenIndex]);
                }

                return;
            }

            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            float pulse = 0.5f + 0.5f * Mathf.Sin(now * 6f);

            for (int i = 0; i < _cardRects.Count; i++)
            {
                bool hover = RectTransformUtility.RectangleContainsScreenPoint(_cardRects[i], pointer, null);
                _cardImages[i].color = hover ? CARD_HOVER_COLOR : CARD_COLOR;
                _outlines[i].effectDistance = hover ? new Vector2(4f, -4f) : new Vector2(2f, -2f);
                _outlines[i].effectColor = hover ? Color.Lerp(GOLD, Color.white, pulse * 0.6f) : GOLD;
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

        private void Choose(HeroPathDefinition path)
        {
            Close();

            if (_hero.SelectPath(path))
            {
                PathChosen?.Invoke(path);
            }
        }

        private void AnimateCard(int index, float targetScale, float step)
        {
            Vector3 target = Vector3.one * targetScale;
            _cardRects[index].localScale = Vector3.Lerp(_cardRects[index].localScale, target, step);
        }

        private void BuildCards(IReadOnlyList<HeroPathDefinition> paths)
        {
            foreach (RectTransform card in _cardRects)
            {
                Destroy(card.gameObject);
            }

            _cardRects.Clear();
            _cardImages.Clear();
            _glows.Clear();
            _outlines.Clear();
            _footers.Clear();
            _paths.Clear();

            foreach (HeroPathDefinition path in paths)
            {
                if (path != null)
                {
                    _paths.Add(path);
                }
            }

            float totalWidth = _paths.Count * CARD_SIZE.x + (_paths.Count - 1) * CARD_SPACING;

            for (int i = 0; i < _paths.Count; i++)
            {
                float x = -totalWidth * 0.5f + CARD_SIZE.x * 0.5f + i * (CARD_SIZE.x + CARD_SPACING);
                BuildCard(_paths[i], x);
            }
        }

        private void BuildCard(HeroPathDefinition path, float x)
        {
            RectTransform card = HudGraphics.CreateRect($"Card_{path.HeroId}", _cardRoot);
            Place(card, new Vector2(0.5f, 0.5f), new Vector2(x, -20f), CARD_SIZE);

            RectTransform glow = HudGraphics.CreateRect("Glow", card);
            glow.anchorMin = Vector2.zero;
            glow.anchorMax = Vector2.one;
            glow.offsetMin = new Vector2(-GLOW_PADDING, -GLOW_PADDING);
            glow.offsetMax = new Vector2(GLOW_PADDING, GLOW_PADDING);
            Image glowImage = glow.gameObject.AddComponent<Image>();
            glowImage.sprite = HudGraphics.WhiteSprite;
            glowImage.color = Color.clear;
            glowImage.raycastTarget = false;

            RectTransform face = HudGraphics.CreateRect("Face", card);
            HudGraphics.Stretch(face);
            Image image = face.gameObject.AddComponent<Image>();
            image.sprite = HudGraphics.WhiteSprite;
            image.color = CARD_COLOR;
            image.raycastTarget = false;
            Outline outline = face.gameObject.AddComponent<Outline>();
            outline.effectColor = GOLD;
            outline.effectDistance = new Vector2(2f, -2f);

            Text title = CreateText("Title", card, 22, SUBTLE, FontStyle.Italic);
            title.text = path.Title;
            Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -36f), new Vector2(CARD_SIZE.x, 30f));

            Text name = CreateText("Name", card, 44, GOLD, FontStyle.Bold);
            name.text = path.DisplayName;
            Place(name.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -82f), new Vector2(CARD_SIZE.x, 56f));

            Text mechanic = CreateText("Mechanic", card, 24, Color.white, FontStyle.Bold);
            mechanic.text = string.IsNullOrEmpty(path.MechanicName) ? "" : $"고유 메커니즘: {path.MechanicName}";
            Place(mechanic.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -140f), new Vector2(CARD_SIZE.x, 30f));

            Text description = CreateText("Description", card, 20, SUBTLE, FontStyle.Normal);
            description.text = path.Description;
            description.horizontalOverflow = HorizontalWrapMode.Wrap;
            Place(description.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -40f),
                new Vector2(CARD_SIZE.x - 40f, 150f));

            Text footer = CreateText("Footer", card, 22, GOLD, FontStyle.Bold);
            footer.text = "클릭하여 선택";
            Place(footer.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 36f), new Vector2(CARD_SIZE.x, 30f));

            _cardRects.Add(card);
            _cardImages.Add(image);
            _glows.Add(glowImage);
            _outlines.Add(outline);
            _footers.Add(footer);
        }

        private static Text CreateText(string textName, Transform parent, int fontSize, Color color, FontStyle style)
        {
            Text text = HudGraphics.CreateRect(textName, parent).gameObject.AddComponent<Text>();
            text.font = HudGraphics.Font;
            text.fontSize = fontSize;
            text.color = color;
            text.fontStyle = style;
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
