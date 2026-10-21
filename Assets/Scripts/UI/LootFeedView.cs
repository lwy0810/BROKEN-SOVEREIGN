using System.Collections.Generic;
using BrokenSovereign.Item;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 획득 알림 (10/21): 적 처치로 가방에 들어온 아이템·골드를 화면 오른쪽에 한 줄씩 띄우고 잠시 뒤 흐려 지웁니다.
    /// 최근 것이 아래, 최대 6줄.
    /// </summary>
    public class LootFeedView : MonoBehaviour
    {
        private const int MAX_LINES = 6;
        private const float LINE_HEIGHT = 26f;
        private const float LIFETIME = 3.5f;
        private const float FADE = 0.8f;

        private sealed class Line
        {
            public Text Text;
            public float Time;
        }

        private readonly List<Line> _lines = new List<Line>();
        private RectTransform _root;
        private Inventory _inventory;

        public void Initialize(Inventory inventory)
        {
            _inventory = inventory;
            _root = (RectTransform)transform;
            _root.anchorMin = _root.anchorMax = new Vector2(1f, 0.5f);
            _root.pivot = new Vector2(1f, 0f);
            _root.sizeDelta = new Vector2(360f, LINE_HEIGHT * MAX_LINES);
            _root.anchoredPosition = new Vector2(-28f, -60f);

            if (_inventory != null)
            {
                _inventory.ItemAcquired += OnItem;
                _inventory.GoldAcquired += OnGold;
            }
        }

        private void OnDestroy()
        {
            if (_inventory != null)
            {
                _inventory.ItemAcquired -= OnItem;
                _inventory.GoldAcquired -= OnGold;
            }
        }

        private void OnItem(ItemDefinition item, int count)
        {
            string text = count > 1 ? $"{item.DisplayName} ×{count}" : item.DisplayName;
            Color color = item.Rarity == ItemRarity.Common ? UiTheme.P.Text : ItemVisuals.RarityColor(item.Rarity);
            Push("획득  " + text, color);
        }

        private void OnGold(int amount)
        {
            Push($"골드 +{amount}", UiTheme.P.Title);
        }

        private void Push(string message, Color color)
        {
            if (_lines.Count >= MAX_LINES)
            {
                Destroy(_lines[0].Text.gameObject);
                _lines.RemoveAt(0);
            }

            Text text = UiKit.SceneLabel("Loot", _root, 17, color, TextAnchor.MiddleRight);
            text.text = message;
            _lines.Add(new Line { Text = text, Time = Time.unscaledTime });
            Layout();
        }

        private void Layout()
        {
            for (int i = 0; i < _lines.Count; i++)
            {
                RectTransform rect = _lines[i].Text.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                rect.pivot = new Vector2(1f, 0f);
                rect.sizeDelta = new Vector2(360f, LINE_HEIGHT);
                rect.anchoredPosition = new Vector2(0f, (_lines.Count - 1 - i) * LINE_HEIGHT);
            }
        }

        private void Update()
        {
            bool removed = false;

            for (int i = _lines.Count - 1; i >= 0; i--)
            {
                float age = Time.unscaledTime - _lines[i].Time;

                if (age > LIFETIME)
                {
                    Destroy(_lines[i].Text.gameObject);
                    _lines.RemoveAt(i);
                    removed = true;
                    continue;
                }

                Color c = _lines[i].Text.color;
                c.a = Mathf.Clamp01((LIFETIME - age) / FADE);
                _lines[i].Text.color = c;
            }

            if (removed)
            {
                Layout();
            }
        }
    }
}
