using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Achievement;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 업적 완료 팝업 (10/26): 화면 왼쪽 중하단에 잠시 미끄러져 나왔다가 사라짐.
    /// 마름모 표식 · "업적 달성" · 업적 이름 · 보상(업적 포인트 · 스탯/칭호). 여러 개면 차례로 표시.
    /// HudView가 생성하고 AchievementTracker.Completed에 연결합니다.
    /// </summary>
    public class AchievementToastView : MonoBehaviour
    {
        private const float WIDTH = 380f;
        private const float HEIGHT = 92f;
        private const float SLIDE = 0.3f;
        private const float HOLD = 3f;
        private const float FADE = 0.5f;
        private const float LEFT = 24f;

        private readonly Queue<AchievementDefinition> _queue = new Queue<AchievementDefinition>();
        private RectTransform _panel;
        private CanvasGroup _group;
        private Text _name;
        private Text _reward;
        private Image _glow;
        private float _start = float.NegativeInfinity;
        private bool _showing;
        private AchievementTracker _tracker;
        private Action _onClick;

        /// <param name="onClick">팝업을 클릭하면 호출 (업적 창 열기)</param>
        public void Initialize(AchievementTracker tracker, Action onClick = null)
        {
            _onClick = onClick;
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);
            UiPalette p = UiTheme.P;

            _panel = UiKit.Plate("AchievementToast", root).rectTransform;
            // 왼쪽 중하단 (화면 높이 32% 지점)
            UiKit.Place(_panel, new Vector2(0f, 0.32f), new Vector2(0f, 0.5f), new Vector2(LEFT, 0f), new Vector2(WIDTH, HEIGHT));
            _panel.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.9f);
            _group = _panel.gameObject.AddComponent<CanvasGroup>();
            _group.blocksRaycasts = false;
            _group.interactable = false;

            _glow = UiKit.Box("Band", _panel, UiTheme.WithAlpha(p.Accent, 0.16f));
            UiKit.PlaceTopLeft(_glow.rectTransform, new Vector2(3f, 3f), new Vector2(WIDTH - 6f, HEIGHT - 6f));

            Image outer = UiKit.Box("Diamond", _panel, p.Title);
            UiKit.Place(outer.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(44f, 0f), new Vector2(30f, 30f));
            outer.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 45f);
            Image inner = UiKit.Box("Inner", outer.rectTransform, p.Accent);
            UiKit.Place(inner.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));

            Text title = UiKit.Label("Title", _panel, 13, p.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(title.rectTransform, new Vector2(80f, 12f), new Vector2(WIDTH - 96f, 18f));
            title.text = "업적 달성";
            // 클릭 안내 (오른쪽 위)
            Text more = UiKit.Label("More", _panel, 12, p.TextDim, TextAnchor.MiddleRight);
            UiKit.PlaceTopLeft(more.rectTransform, new Vector2(WIDTH - 136f, 12f), new Vector2(120f, 18f));
            more.text = "클릭: 업적 창";
            _name = UiKit.Title("Name", _panel, 21, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_name.rectTransform, new Vector2(80f, 32f), new Vector2(WIDTH - 96f, 28f));
            // 보조 글: 이름과 10px 띄움
            _reward = UiKit.Label("Reward", _panel, 13, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_reward.rectTransform, new Vector2(80f, 66f), new Vector2(WIDTH - 96f, 18f));

            _panel.gameObject.SetActive(false);

            _tracker = tracker;

            if (tracker != null)
            {
                tracker.Completed += Enqueue;
            }
        }

        private void OnDisable()
        {
            GameplayInput.PointerOverToast = false;
        }

        private void OnDestroy()
        {
            if (_tracker != null)
            {
                _tracker.Completed -= Enqueue;
            }
        }

        public void Enqueue(AchievementDefinition definition)
        {
            if (definition != null)
            {
                _queue.Enqueue(definition);
            }
        }

        private void Update()
        {
            if (!_showing)
            {
                if (_queue.Count == 0)
                {
                    GameplayInput.PointerOverToast = false;
                    return;
                }

                Show(_queue.Dequeue());
            }

            float t = Time.unscaledTime - _start;
            float total = SLIDE + HOLD + FADE;

            if (t >= total)
            {
                Finish();
                return;
            }

            // 클릭하면 업적 창을 열고 팝업을 닫음 (다른 창이 열려 있을 땐 무시)
            bool hover = _onClick != null && !GameplayInput.IsBlocked && _group.alpha > 0.2f && UiKit.IsHover(_panel);
            GameplayInput.PointerOverToast = hover;

            if (hover && UiKit.Clicked)
            {
                Finish();
                _queue.Clear();
                _onClick();
                return;
            }

            // 왼쪽에서 미끄러져 나옴 → 유지 → 서서히 사라짐
            float slide = Mathf.Clamp01(t / SLIDE);
            float ease = 1f - (1f - slide) * (1f - slide);
            _panel.anchoredPosition = new Vector2(Mathf.Lerp(-WIDTH, LEFT, ease), 0f);
            _group.alpha = t < SLIDE + HOLD ? 1f : 1f - (t - SLIDE - HOLD) / FADE;
            float flash = 0.1f * Mathf.Max(0f, 1f - t / 0.8f);
            _glow.color = UiTheme.WithAlpha(UiTheme.P.Accent, (hover ? 0.24f : 0.12f) + flash);
        }

        private void Finish()
        {
            _showing = false;
            _panel.gameObject.SetActive(false);
            GameplayInput.PointerOverToast = false;
        }

        private void Show(AchievementDefinition definition)
        {
            _showing = true;
            _start = Time.unscaledTime;
            _name.text = definition.Name;
            string reward = !string.IsNullOrEmpty(definition.Title) ? $"칭호 「{definition.Title}」"
                : string.IsNullOrEmpty(definition.RewardText) ? string.Empty : definition.RewardText;
            _reward.text = reward.Length > 0 ? $"업적 포인트 +{definition.Points}  ·  {reward}" : $"업적 포인트 +{definition.Points}";
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
        }
    }
}
