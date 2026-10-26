using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Item;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 바닥 아이템·테스트 상자 이름표 (10/26). 희귀도 색 글자 + 어두운 바탕, 마우스를 올리면 밝아짐.
    /// 클릭하면 ItemPicker가 걸어가서 줍기/열기. 이름표가 겹치면 위로 밀어 올림.
    /// 이름표 위에서는 클릭 이동·공격을 막음 (GameplayInput.PointerOverItem). HudView가 생성합니다.
    /// </summary>
    public class GroundItemLabelView : MonoBehaviour
    {
        private const float MAX_DISTANCE = 22f;
        private const float PAD_X = 10f;
        private const float HEIGHT = 24f;

        private sealed class Label
        {
            public RectTransform Rect;
            public Image Background;
            public Outline Outline;
            public Text Text;
        }

        private readonly Dictionary<WorldInteractable, Label> _labels = new Dictionary<WorldInteractable, Label>();
        private readonly List<WorldInteractable> _removed = new List<WorldInteractable>();
        private readonly List<(WorldInteractable target, Label label, float distance)> _visible = new List<(WorldInteractable, Label, float)>();
        private readonly List<Rect> _placed = new List<Rect>();
        private RectTransform _root;
        private Camera _camera;
        private Transform _player;
        private ItemPicker _picker;

        public void Initialize(GameObject player)
        {
            _player = player.transform;
            _picker = player.GetComponent<ItemPicker>();
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);
        }

        private void OnDisable()
        {
            GameplayInput.PointerOverItem = false;
        }

        private void LateUpdate()
        {
            if (_camera == null)
            {
                _camera = Camera.main;
            }

            Canvas canvas = _root.GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            bool hidden = _camera == null || _player == null || GameplayInput.IsBlocked;
            _visible.Clear();

            // 사라진 대상 정리
            foreach (KeyValuePair<WorldInteractable, Label> pair in _labels)
            {
                if (pair.Key == null)
                {
                    _removed.Add(pair.Key);
                }
            }

            foreach (WorldInteractable key in _removed)
            {
                if (_labels[key].Rect != null)
                {
                    Destroy(_labels[key].Rect.gameObject);
                }

                _labels.Remove(key);
            }

            _removed.Clear();

            foreach (WorldInteractable target in WorldInteractable.All)
            {
                if (!_labels.TryGetValue(target, out Label label))
                {
                    label = CreateLabel();
                    _labels[target] = label;
                }

                float distance = _player != null ? Vector3.Distance(_player.position, target.transform.position) : 0f;
                bool show = !hidden && target.ShowLabel && distance <= MAX_DISTANCE;
                label.Rect.gameObject.SetActive(show);

                if (show)
                {
                    _visible.Add((target, label, distance));
                }
            }

            // 가까운 것부터 자리 잡기, 겹치면 위로 밀기
            _visible.Sort((a, b) => a.distance.CompareTo(b.distance));
            _placed.Clear();
            WorldInteractable hovered = null;

            foreach (var (target, label, _) in _visible)
            {
                Vector3 screen = _camera.WorldToScreenPoint(target.transform.position + Vector3.up * target.LabelHeight);

                if (screen.z <= 0f)
                {
                    label.Rect.gameObject.SetActive(false);
                    continue;
                }

                if (label.Text.text != target.LabelText)
                {
                    label.Text.text = target.LabelText;
                }

                float width = label.Text.preferredWidth + PAD_X * 2f;
                Vector2 position = new Vector2(screen.x, screen.y) / scale;
                Rect rect = new Rect(position.x - width * 0.5f, position.y, width, HEIGHT);

                for (int guard = 0; guard < 12 && Overlaps(rect); guard++)
                {
                    rect.y += HEIGHT + 2f;
                }

                _placed.Add(rect);
                label.Rect.sizeDelta = new Vector2(width, HEIGHT);
                label.Rect.anchoredPosition = new Vector2(rect.x + width * 0.5f, rect.y);

                bool hover = hovered == null && UiKit.IsHover(label.Rect);

                if (hover)
                {
                    hovered = target;
                }

                Color color = target.LabelColor;
                label.Text.color = hover ? Color.Lerp(color, Color.white, 0.35f) : color;
                label.Background.color = hover ? new Color(0.16f, 0.15f, 0.12f, 0.92f) : new Color(0.03f, 0.03f, 0.04f, 0.72f);
                label.Outline.effectColor = hover ? UiTheme.WithAlpha(color, 0.9f) : Color.clear;
            }

            GameplayInput.PointerOverItem = hovered != null;

            if (hovered != null && UiKit.Clicked && _picker != null)
            {
                _picker.Go(hovered);
            }
        }

        private bool Overlaps(Rect rect)
        {
            foreach (Rect placed in _placed)
            {
                if (placed.Overlaps(rect))
                {
                    return true;
                }
            }

            return false;
        }

        private Label CreateLabel()
        {
            var label = new Label();
            label.Background = UiKit.Round("ItemLabel", _root, new Color(0f, 0f, 0f, 0.7f));
            label.Rect = label.Background.rectTransform;
            label.Rect.anchorMin = label.Rect.anchorMax = Vector2.zero;
            label.Rect.pivot = new Vector2(0.5f, 0f);
            label.Outline = label.Background.gameObject.AddComponent<Outline>();
            label.Outline.effectColor = Color.clear;
            label.Text = UiKit.Label("Text", label.Rect, 15, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(label.Text.rectTransform);
            return label;
        }
    }
}
