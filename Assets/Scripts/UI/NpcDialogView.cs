using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 마을 NPC 상호작용 UI: NPC 머리 위 [F] 안내 + 화면 아래 대화 창 (이름·대사·기능 버튼·떠나기).
    /// NpcInteractor 이벤트를 받아 표시만 하고, 버튼은 NpcInteractor에 요청합니다.
    /// HudView가 생성합니다. 다른 선택 화면과 같은 방식으로 마우스 위치를 직접 판정합니다.
    /// </summary>
    public class NpcDialogView : MonoBehaviour
    {
        private static readonly Vector2 PANEL_SIZE = new Vector2(780f, 230f);
        private const float PANEL_BOTTOM = 230f;
        private static readonly Vector2 BUTTON_SIZE = new Vector2(190f, 46f);
        private static readonly Vector2 PROMPT_SIZE = new Vector2(320f, 40f);

        private static Color PANEL_COLOR => Color.white;
        private static Color BUTTON_COLOR => Color.white;
        private static Color BUTTON_HOVER_COLOR => new Color(1f, 0.9f, 0.75f);
        private static Color PROMPT_COLOR => Color.white;
        private static Color GOLD => UiTheme.P.Title;
        private static Color BODY => UiTheme.P.Text;

        private NpcInteractor _interactor;
        private RectTransform _root;
        private RectTransform _prompt;
        private Text _promptText;
        private RectTransform _panel;
        private Text _nameText;
        private Text _lineText;
        private RectTransform _serviceButton;
        private Image _serviceImage;
        private Text _serviceText;
        private RectTransform _leaveButton;
        private Image _leaveImage;

        public void Initialize(NpcInteractor interactor)
        {
            _interactor = interactor;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);

            BuildPrompt();
            BuildPanel();

            _interactor.TalkStarted += OnTalkStarted;
            _interactor.TalkEnded += OnTalkEnded;
            _interactor.ServiceUsed += OnServiceUsed;
        }

        private void OnDestroy()
        {
            if (_interactor != null)
            {
                _interactor.TalkStarted -= OnTalkStarted;
                _interactor.TalkEnded -= OnTalkEnded;
                _interactor.ServiceUsed -= OnServiceUsed;
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
                UpdateButtons();
            }
        }

        // ------------------------------------------------------------------
        // 머리 위 안내
        // ------------------------------------------------------------------

        private void UpdatePrompt()
        {
            TownNpc focus = _interactor.Focus;
            Camera camera = Camera.main;

            if (focus == null || camera == null)
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

        // ------------------------------------------------------------------
        // 대화 창
        // ------------------------------------------------------------------

        private void OnTalkStarted(TownNpc npc)
        {
            _nameText.text = $"{npc.Title}  {npc.DisplayName}";
            _lineText.text = npc.NextLine();

            bool hasService = npc.Service != null;
            _serviceButton.gameObject.SetActive(hasService);

            if (hasService)
            {
                _serviceText.text = npc.Service.ActionLabel;
            }

            _panel.gameObject.SetActive(true);
        }

        private void OnTalkEnded()
        {
            _panel.gameObject.SetActive(false);
        }

        private void OnServiceUsed(string line)
        {
            _lineText.text = line;
        }

        private void UpdateButtons()
        {
            Mouse mouse = Mouse.current;
            Vector2 pointer = mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            bool click = mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (_serviceButton.gameObject.activeSelf && Hover(_serviceButton, _serviceImage, pointer) && click)
            {
                _interactor.UseService();
            }

            if (Hover(_leaveButton, _leaveImage, pointer) && click)
            {
                _interactor.EndTalk();
            }
        }

        private static bool Hover(RectTransform button, Image image, Vector2 pointer)
        {
            bool hover = RectTransformUtility.RectangleContainsScreenPoint(button, pointer, null);
            image.color = hover ? BUTTON_HOVER_COLOR : BUTTON_COLOR;
            return hover;
        }

        // ------------------------------------------------------------------
        // 생성
        // ------------------------------------------------------------------

        private void BuildPrompt()
        {
            _prompt = HudGraphics.CreateRect("NpcPrompt", _root);
            _prompt.anchorMin = _prompt.anchorMax = new Vector2(0.5f, 0.5f);
            _prompt.pivot = new Vector2(0.5f, 0f);
            _prompt.sizeDelta = PROMPT_SIZE;
            Image background = _prompt.gameObject.AddComponent<Image>();
            background.sprite = UiSprites.Button;
            background.type = Image.Type.Sliced;
            background.color = PROMPT_COLOR;
            background.raycastTarget = false;

            _promptText = CreateText("Label", _prompt, 22, GOLD, FontStyle.Bold, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(_promptText.rectTransform);
            _prompt.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            _panel = HudGraphics.CreateRect("NpcDialog", _root);
            _panel.anchorMin = _panel.anchorMax = new Vector2(0.5f, 0f);
            _panel.pivot = new Vector2(0.5f, 0f);
            _panel.anchoredPosition = new Vector2(0f, PANEL_BOTTOM);
            _panel.sizeDelta = PANEL_SIZE;

            Image background = _panel.gameObject.AddComponent<Image>();
            background.sprite = UiSprites.Plate;
            background.type = Image.Type.Sliced;
            background.color = PANEL_COLOR;
            background.raycastTarget = false;

            _nameText = CreateText("Name", _panel, 28, GOLD, FontStyle.Normal, TextAnchor.UpperLeft);
            UiTheme.ApplyTitle(_nameText);
            PlaceTopLeft(_nameText.rectTransform, new Vector2(28f, -20f), new Vector2(PANEL_SIZE.x - 56f, 36f));

            _lineText = CreateText("Line", _panel, 22, BODY, FontStyle.Normal, TextAnchor.UpperLeft);
            _lineText.horizontalOverflow = HorizontalWrapMode.Wrap;
            PlaceTopLeft(_lineText.rectTransform, new Vector2(28f, -66f), new Vector2(PANEL_SIZE.x - 56f, 90f));

            _leaveButton = CreateButton("Leave", "떠나기 [F]", new Vector2(-24f, 20f), out _leaveImage, out _);
            _serviceButton = CreateButton("Service", string.Empty,
                new Vector2(-24f - BUTTON_SIZE.x - 14f, 20f), out _serviceImage, out _serviceText);

            _panel.gameObject.SetActive(false);
        }

        private RectTransform CreateButton(string buttonName, string label, Vector2 position, out Image image, out Text text)
        {
            RectTransform button = HudGraphics.CreateRect(buttonName, _panel);
            button.anchorMin = button.anchorMax = new Vector2(1f, 0f);
            button.pivot = new Vector2(1f, 0f);
            button.anchoredPosition = position;
            button.sizeDelta = BUTTON_SIZE;

            image = button.gameObject.AddComponent<Image>();
            image.sprite = UiSprites.Button;
            image.type = Image.Type.Sliced;
            image.color = BUTTON_COLOR;
            image.raycastTarget = false;

            text = CreateText("Label", button, 22, GOLD, FontStyle.Bold, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        private static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
        }

        private static Text CreateText(string textName, Transform parent, int fontSize, Color color, FontStyle style,
            TextAnchor alignment)
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

            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            return text;
        }
    }
}
