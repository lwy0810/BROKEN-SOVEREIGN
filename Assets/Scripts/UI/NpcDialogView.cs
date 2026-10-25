using System;
using System.Collections.Generic;
using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 마을 NPC 대화 창 (10/25 개편, Figma 11-A·11-B): 화면 아래 1400×250 대화창.
    /// 위쪽 알약형 이름 탭(이름 + 역할) · 대사 2줄 + 보조 설명 · 오른쪽 선택지 (10/25: 초상·× 제거).
    /// 선택지: [거래](상점) · [치료 등 NPC 기능] · [이야기를 듣는다] · [떠나기] — 클릭 또는 숫자 키, Esc·F = 떠나기.
    /// NPC 머리 위 [F] 안내도 표시. 상점 창이 열리면 대화창은 숨김. NpcInteractor에 요청만 하고 상태는 그쪽이 가짐.
    /// </summary>
    public class NpcDialogView : MonoBehaviour
    {
        private static readonly Vector2 PANEL_SIZE = new Vector2(1400f, 250f);
        private const float PANEL_BOTTOM = 160f;
        private const float OPTION_WIDTH = 330f;
        private const float OPTION_HEIGHT = 44f;
        private const float OPTION_GAP = 8f;
        private static readonly Vector2 PROMPT_SIZE = new Vector2(320f, 40f);

        private sealed class Option
        {
            public RectTransform Rect;
            public Image Background;
            public Image Highlight;   // 10/26: 오버·키 입력 시 밝은 금색 덧칠
            public Action Run;
        }

        private NpcInteractor _interactor;
        private RectTransform _root;
        private RectTransform _prompt;
        private Text _promptText;
        private RectTransform _panel;
        private Text _nameText;
        private Text _roleText;
        private RectTransform _nameTab;
        private Text _lineText;
        private Text _subText;
        private RectTransform _options;
        private readonly List<Option> _optionRows = new List<Option>();

        public void Initialize(NpcInteractor interactor)
        {
            _interactor = interactor;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);

            BuildPrompt();
            BuildPanel();

            _interactor.TalkStarted += OnTalkStarted;
            _interactor.TalkEnded += OnTalkEnded;
            _interactor.ServiceUsed += OnLine;
            _interactor.ShopOpened += OnShopOpened;
            _interactor.ShopClosed += OnShopClosed;
        }

        private void OnDestroy()
        {
            if (_interactor != null)
            {
                _interactor.TalkStarted -= OnTalkStarted;
                _interactor.TalkEnded -= OnTalkEnded;
                _interactor.ServiceUsed -= OnLine;
                _interactor.ShopOpened -= OnShopOpened;
                _interactor.ShopClosed -= OnShopClosed;
            }
        }

        private void LateUpdate()
        {
            if (_interactor == null)
            {
                return;
            }

            UpdatePrompt();

            if (_panel.gameObject.activeSelf)
            {
                UpdateOptions();
            }
        }

        // ---------- 머리 위 안내

        private void UpdatePrompt()
        {
            TownNpc focus = _interactor.Focus;
            Camera camera = Camera.main;

            if (focus == null || camera == null || _interactor.Talking != null)
            {
                _prompt.gameObject.SetActive(false);
                return;
            }

            Vector3 screen = camera.WorldToScreenPoint(focus.PromptPosition);

            if (screen.z <= 0f)
            {
                _prompt.gameObject.SetActive(false);
                return;
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, screen, null, out Vector2 local);
            _prompt.anchoredPosition = local;
            _promptText.text = $"[F] {focus.Title} {focus.DisplayName}";
            _prompt.gameObject.SetActive(true);
        }

        // ---------- 대화 창

        private void OnTalkStarted(TownNpc npc)
        {
            ShopType shop = ShopCatalog.TypeOf(npc);
            _nameText.text = npc.DisplayName;
            _roleText.text = npc.Title;
            float nameWidth = _nameText.preferredWidth + _roleText.preferredWidth + 60f;
            _nameTab.sizeDelta = new Vector2(Mathf.Max(200f, nameWidth), 44f);
            _roleText.rectTransform.anchoredPosition = new Vector2(_nameText.preferredWidth + 34f, 0f);
            _lineText.text = npc.NextLine();
            _subText.text = ShopCatalog.Describe(shop, npc.Service);

            ClearOptions();

            if (npc.Service != null)
            {
                AddOption(npc.Service.ActionLabel, _interactor.UseService);
            }

            if (shop != ShopType.None)
            {
                AddOption("거래", _interactor.OpenShop);
            }

            AddOption("이야기를 듣는다", _interactor.Talk);
            AddOption("떠나기", _interactor.EndTalk, "Esc");
            _panel.gameObject.SetActive(true);
        }

        private void OnTalkEnded()
        {
            _panel.gameObject.SetActive(false);
        }

        private void OnLine(string line)
        {
            _lineText.text = line;
        }

        private void OnShopOpened(TownNpc npc, ShopType type)
        {
            _panel.gameObject.SetActive(false);
        }

        private void OnShopClosed()
        {
            if (_interactor.Talking != null)
            {
                _panel.gameObject.SetActive(true);
            }
        }

        // 10/26: 숫자 키로 고르면 마우스 오버와 같은 강조를 잠깐 보여 준 뒤 실행 (키 입력 확인용)
        private const float KEY_FLASH_TIME = 0.15f;
        private Option _flashing;
        private float _flashUntil;

        private void UpdateOptions()
        {
            if (_flashing != null)
            {
                if (Time.unscaledTime < _flashUntil)
                {
                    return;
                }

                Option chosen = _flashing;
                _flashing = null;
                SetHighlight(chosen, false);
                chosen.Run?.Invoke();
                return;
            }

            Keyboard keyboard = Keyboard.current;

            for (int i = 0; i < _optionRows.Count; i++)
            {
                Option option = _optionRows[i];
                bool hover = UiKit.IsHover(option.Rect);
                SetHighlight(option, hover);
                bool key = keyboard != null && i < 9 && i < _optionRows.Count - 1 && keyboard[(Key)((int)Key.Digit1 + i)].wasPressedThisFrame;

                if (hover && UiKit.Clicked)
                {
                    option.Run?.Invoke();
                    return;
                }

                if (key)
                {
                    SetHighlight(option, true);
                    _flashing = option;
                    _flashUntil = Time.unscaledTime + KEY_FLASH_TIME;
                    return;
                }
            }
        }

        private static void SetHighlight(Option option, bool on)
        {
            option.Highlight.enabled = on;
            option.Background.color = on ? Color.white : new Color(0.85f, 0.85f, 0.88f);
        }

        private void ClearOptions()
        {
            _flashing = null;

            foreach (Option option in _optionRows)
            {
                Destroy(option.Rect.gameObject);
            }

            _optionRows.Clear();
        }

        // 선택지 버튼: 금색 점 · 이름 · 단축키 칸 (Figma 'NPC 대화 / 선택지')
        private void AddOption(string label, Action run, string key = null)
        {
            UiPalette p = UiTheme.P;
            int index = _optionRows.Count;
            Image background = UiKit.Sliced($"Option{index}", _options, UiSprites.Button);
            RectTransform rect = background.rectTransform;
            UiKit.PlaceTopLeft(rect, new Vector2(0f, index * (OPTION_HEIGHT + OPTION_GAP)), new Vector2(OPTION_WIDTH, OPTION_HEIGHT));

            // 오버 강조: 판 위에 밝은 금색 덧칠 + 금색 테두리 (예전 색 곱하기는 어두운 판에서 잘 안 보였음)
            Image highlight = UiKit.Box("Highlight", rect, UiTheme.WithAlpha(p.Title, 0.28f));
            HudGraphics.Stretch(highlight.rectTransform);
            highlight.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Title, 0.9f);
            highlight.GetComponent<Outline>().effectDistance = new Vector2(2f, -2f);
            highlight.enabled = false;

            Image dot = UiKit.Round("Dot", rect, p.Accent);
            UiKit.Place(dot.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(10f, 10f));

            Text text = UiKit.Label("Label", rect, 17, p.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(text.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(OPTION_WIDTH - 110f, OPTION_HEIGHT));
            text.text = label;

            Image keyBox = UiKit.Box("Key", rect, p.Inset);
            UiKit.Place(keyBox.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-14f, 0f), new Vector2(key != null ? 40f : 26f, 24f));
            Text keyText = UiKit.Label("KeyText", keyBox.rectTransform, 13, p.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(keyText.rectTransform);
            keyText.text = key ?? (index + 1).ToString();

            _optionRows.Add(new Option { Rect = rect, Background = background, Highlight = highlight, Run = run });
        }

        private void BuildPrompt()
        {
            Image background = UiKit.Sliced("Prompt", _root, UiSprites.Button);
            _prompt = background.rectTransform;
            _prompt.anchorMin = _prompt.anchorMax = new Vector2(0.5f, 0.5f);
            _prompt.pivot = new Vector2(0.5f, 0f);
            _prompt.sizeDelta = PROMPT_SIZE;
            _promptText = UiKit.Label("Text", _prompt, 18, UiTheme.P.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_promptText.rectTransform);
            _prompt.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            UiPalette p = UiTheme.P;
            _panel = UiKit.Plate("DialogPanel", _root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, PANEL_BOTTOM), PANEL_SIZE);

            // 이름 탭: 대화창 위 테두리에 걸침
            Image tab = UiKit.Sliced("NameTab", _panel, UiSprites.Button);
            _nameTab = tab.rectTransform;
            UiKit.Place(_nameTab, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(30f, 0f), new Vector2(240f, 44f));
            _nameText = UiKit.Title("Name", _nameTab, 22, p.Title, TextAnchor.MiddleLeft);
            UiKit.Place(_nameText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(22f, 0f), new Vector2(240f, 40f));
            _nameText.horizontalOverflow = HorizontalWrapMode.Overflow;
            _roleText = UiKit.Label("Role", _nameTab, 14, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.Place(_roleText.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(110f, 0f), new Vector2(160f, 30f));
            _roleText.horizontalOverflow = HorizontalWrapMode.Overflow;

            // 대사 · 보조 설명
            _lineText = UiKit.Label("Line", _panel, 20, p.Text, TextAnchor.UpperLeft);
            UiKit.PlaceTopLeft(_lineText.rectTransform, new Vector2(40f, 44f), new Vector2(960f, 100f));
            _lineText.lineSpacing = 1.25f;
            _subText = UiKit.Label("Sub", _panel, 16, p.TextDim, TextAnchor.UpperLeft);
            UiKit.PlaceTopLeft(_subText.rectTransform, new Vector2(40f, 160f), new Vector2(960f, 50f));

            // 구분선 + 선택지
            Image divider = UiKit.Box("Divider", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.5f));
            UiKit.PlaceTopLeft(divider.rectTransform, new Vector2(1030f, 24f), new Vector2(1f, 202f));
            _options = HudGraphics.CreateRect("Options", _panel);
            UiKit.PlaceTopLeft(_options, new Vector2(1046f, 24f), new Vector2(OPTION_WIDTH, 202f));

            _panel.gameObject.SetActive(false);
        }
    }
}
