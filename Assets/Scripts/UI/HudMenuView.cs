using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// HUD 메뉴 버튼 + 메뉴 창 (10/26, Figma 16-A/B). 기술 바 오른쪽 위의 ☰ 버튼을 누르면 위로 펼쳐짐.
    /// - 항목 클릭: 해당 창을 열고 메뉴는 닫힘. 준비 중 항목은 흐리게, 클릭 무시
    /// - 메뉴 밖 클릭 · Esc: 닫힘. 다른 창이 열려 있지 않을 때 Esc는 메뉴를 엶 (창이 열려 있으면 그 창이 먼저 닫힘)
    /// - 메뉴가 열려 있거나 버튼 위에 마우스가 있으면 클릭 이동·공격을 막음 (GameplayInput.PointerOverMenu)
    /// 다른 화면과 같이 EventSystem 없이 마우스 위치로 판정 (UiKit).
    /// </summary>
    public class HudMenuView : MonoBehaviour
    {
        public const float WIDTH = 203f;
        private const float BUTTON = 52f;
        private const float HEADER = 46f;
        private const float ROW_H = 40f;
        private const float ROW_PITCH = 46f;
        private const float PAD = 12f;
        private const float GAP = 10f;

        /// <summary>메뉴 항목</summary>
        public readonly struct Item
        {
            public readonly string Label;
            public readonly string Key;
            public readonly Action Action;
            public readonly bool Enabled;

            public Item(string label, string key, Action action, bool enabled = true)
            {
                Label = label;
                Key = key;
                Action = action;
                Enabled = enabled && action != null;
            }
        }

        private sealed class Row
        {
            public Item Item;
            public RectTransform Rect;
            public Image Background;
            public Outline Outline;
            public Image Dot;
            public Text Label;
        }

        private RectTransform _root;
        private RectTransform _button;
        private Image _buttonImage;
        private RectTransform _panel;
        private readonly List<Row> _rows = new List<Row>();
        private Func<bool> _isOtherWindowOpen;
        private bool _otherOpenLastFrame;
        private Action _pending;
        private bool _closedThisFrame;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>메뉴를 만들고 반환. isOtherWindowOpen: 다른 창(인벤토리·기술 등)이 열려 있는지 (Esc 판정용)</summary>
        public static HudMenuView Create(RectTransform parent, IReadOnlyList<Item> items, Func<bool> isOtherWindowOpen)
        {
            RectTransform root = HudGraphics.CreateRect("HudMenu", parent);
            UiKit.Place(root, new Vector2(0.5f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(WIDTH, BUTTON));
            HudMenuView view = root.gameObject.AddComponent<HudMenuView>();
            view._isOtherWindowOpen = isOtherWindowOpen;
            view.Build(root, items);
            return view;
        }

        /// <summary>버튼 위치: 화면 아래 가운데 기준, 버튼 오른쪽 끝 x · 아래쪽 y</summary>
        public void SetPosition(float right, float bottom)
        {
            _root.anchoredPosition = new Vector2(right, bottom);
        }

        public void Open()
        {
            _panel.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Close()
        {
            _closedThisFrame |= IsOpen;
            _panel.gameObject.SetActive(false);
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else
            {
                Open();
            }
        }

        // ------------------------------------------------------------------

        private void Build(RectTransform root, IReadOnlyList<Item> items)
        {
            _root = root;
            UiPalette p = UiTheme.P;

            // ☰ 버튼 (오른쪽 정렬)
            _buttonImage = UiKit.Sliced("MenuButton", root, UiSprites.Button);
            _button = _buttonImage.rectTransform;
            UiKit.Place(_button, new Vector2(1f, 0f), new Vector2(1f, 0f), Vector2.zero, new Vector2(BUTTON, BUTTON));
            _buttonImage.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.7f);

            for (int i = 0; i < 3; i++)
            {
                Image line = UiKit.Box("Line", _button, p.Accent);
                UiKit.Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                    new Vector2(0f, 9f - i * 9f), new Vector2(24f, 3f));
            }

            // 메뉴 창: 버튼 위로 펼쳐짐, 오른쪽 끝 맞춤
            float height = HEADER + items.Count * ROW_PITCH + PAD - (ROW_PITCH - ROW_H);
            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(0f, BUTTON + GAP), new Vector2(WIDTH, height));

            Image tab = UiKit.Sliced("TitleTab", _panel, UiSprites.Button);
            UiKit.Place(tab.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(88f, 38f));
            tab.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.8f);
            Text title = UiKit.Title("Title", tab.transform, 20, p.Title, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(title.rectTransform);
            title.text = "메 뉴";

            for (int i = 0; i < items.Count; i++)
            {
                _rows.Add(BuildRow(items[i], HEADER + i * ROW_PITCH));
            }

            _panel.gameObject.SetActive(false);
        }

        private Row BuildRow(Item item, float y)
        {
            UiPalette p = UiTheme.P;
            var row = new Row { Item = item };

            row.Background = UiKit.Round("Row", _panel, UiTheme.WithAlpha(p.Inset, 0.85f));
            row.Rect = row.Background.rectTransform;
            UiKit.PlaceTopLeft(row.Rect, new Vector2(PAD, y), new Vector2(WIDTH - PAD * 2f, ROW_H));
            row.Outline = row.Background.gameObject.AddComponent<Outline>();
            row.Outline.effectColor = Color.clear;

            row.Dot = UiKit.Box("Dot", row.Rect, item.Enabled ? p.Accent : p.TextDisabled);
            row.Dot.sprite = HudGraphics.CircleSprite;
            UiKit.Place(row.Dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(18f, 0f), new Vector2(9f, 9f));

            row.Label = UiKit.Label("Label", row.Rect, 18, item.Enabled ? p.Text : p.TextDisabled, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(row.Label.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(100f, ROW_H));
            row.Label.text = item.Label;

            // 오른쪽: 단축키 칩 또는 "준비 중"
            if (!item.Enabled)
            {
                Image chip = UiKit.Round("Soon", row.Rect, UiTheme.WithAlpha(p.Inset, 0.6f));
                UiKit.Place(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(54f, 22f));
                chip.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.TextDim, 0.8f);
                Text soon = UiKit.Label("Text", chip.transform, 12, p.TextDim, TextAnchor.MiddleCenter);
                HudGraphics.Stretch(soon.rectTransform);
                soon.text = "준비 중";
            }
            else if (!string.IsNullOrEmpty(item.Key))
            {
                Image chip = UiKit.Round("Key", row.Rect, new Color(0.04f, 0.04f, 0.05f, 0.95f));
                UiKit.Place(chip.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-10f, 0f), new Vector2(24f, 22f));
                Text key = UiKit.Label("Text", chip.transform, 14, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                HudGraphics.Stretch(key.rectTransform);
                key.text = item.Key;
            }

            return row;
        }

        // ------------------------------------------------------------------

        private void Update()
        {
            _closedThisFrame = false;

            // 항목 동작은 다음 프레임에 (같은 클릭이 새로 연 창에 다시 들어가지 않도록)
            if (_pending != null)
            {
                Action action = _pending;
                _pending = null;
                action();
            }

            Keyboard keyboard = Keyboard.current;
            bool escape = keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
            bool buttonHover = UiKit.IsHover(_button);
            _buttonImage.color = buttonHover || IsOpen ? new Color(1f, 0.88f, 0.72f) : Color.white;

            if (escape)
            {
                if (IsOpen)
                {
                    Close();
                }
                else if (!_otherOpenLastFrame && !GameplayInput.IsBlocked)
                {
                    Open();
                }
            }
            else if (UiKit.Clicked && buttonHover)
            {
                if (IsOpen || !GameplayInput.IsBlocked)
                {
                    Toggle();
                }
            }
            else if (IsOpen)
            {
                UpdateRows();
            }

            // 닫은 프레임도 막음 (메뉴를 닫은 클릭으로 캐릭터가 움직이지 않도록)
            GameplayInput.PointerOverMenu = buttonHover || IsOpen || _closedThisFrame;
        }

        // 다른 창이 이번 프레임에 Esc로 닫혀도 메뉴가 바로 열리지 않도록, 지난 프레임 상태로 판정
        private void LateUpdate()
        {
            _otherOpenLastFrame = _isOtherWindowOpen != null && _isOtherWindowOpen();
        }

        private void OnDisable()
        {
            GameplayInput.PointerOverMenu = false;
        }

        private void UpdateRows()
        {
            UiPalette p = UiTheme.P;
            Row clicked = null;

            foreach (Row row in _rows)
            {
                bool hover = row.Item.Enabled && UiKit.IsHover(row.Rect);
                row.Background.color = hover ? UiTheme.WithAlpha(p.Accent, 0.28f) : UiTheme.WithAlpha(p.Inset, 0.85f);
                row.Outline.effectColor = hover ? p.Accent : Color.clear;
                row.Label.color = !row.Item.Enabled ? p.TextDisabled : hover ? p.Title : p.Text;

                if (hover && UiKit.Clicked)
                {
                    clicked = row;
                }
            }

            if (clicked != null)
            {
                Close();
                _pending = clicked.Item.Action;
            }
            else if (UiKit.Clicked && !UiKit.IsHover(_panel))
            {
                // 메뉴 밖 클릭: 닫기만 (이번 클릭은 이동으로 쓰지 않음)
                Close();
            }
        }
    }
}
