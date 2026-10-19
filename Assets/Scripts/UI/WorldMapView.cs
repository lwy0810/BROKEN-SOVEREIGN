using BrokenSovereign.Core;
using BrokenSovereign.Map;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 지도 화면 (M 열기·닫기, Esc 닫기). 표시 방식은 GameSettings.MapMode (추후 시스템 설정에서 변경, 지금은 지도 화면 버튼).
    /// - 전체 화면: 불투명 바탕으로 화면 전체에 지도. 게임 입력 차단, 마우스 휠 확대·축소, 왼쪽 끌기로 이동
    /// - 오버레이: 게임 화면 위에 반투명 지도. 계속 이동 가능, 휠 확대·축소, 플레이어를 따라감
    /// 처음 열 때 확대 단계: 마을 안 = 마을 전체가 보이는 폭, 마을 밖(전투 지역) = 주변을 크게 확대한 폭.
    /// 아이콘은 확대 단계별로 표시 (상점 등 작은 표시는 축소하면 숨김, MapIconSet.MaxViewWidth).
    /// 위쪽 = 카메라가 바라보는 방향 (미니맵과 같음). HudView가 생성합니다.
    /// </summary>
    public class WorldMapView : MonoBehaviour
    {
        private const float TOWN_VIEW_WIDTH = 190f;     // 마을 안에서 처음 보이는 폭 (m)
        private const float FIELD_VIEW_WIDTH = 260f;    // 전투 지역에서 처음 보이는 폭 (m)
        private const float MIN_VIEW_WIDTH = 80f;
        private const float ZOOM_STEP = 1.15f;          // 휠 한 칸 배율
        private const float ICON_SIZE = 34f;
        private const float OVERLAY_ALPHA = 0.7f;

        private static readonly Color BACKGROUND = new Color(0.1f, 0.09f, 0.08f, 1f);
        private static readonly Color GOLD = new Color(0.95f, 0.8f, 0.45f);

        private Transform _player;
        private RectTransform _root;
        private GameObject _panel;
        private Image _background;
        private MapLayer _layer;
        private Text _title;
        private RectTransform _modeButton;
        private Image _modeButtonImage;
        private Text _modeButtonText;

        private Vector2 _viewCenter;
        private float _viewWidth;
        private bool _isFullscreen;
        private bool _blocking;
        private bool _dragging;
        private Vector2 _lastPointer;

        public bool IsOpen => _panel.activeSelf;

        public void Initialize(Transform player)
        {
            _player = player;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);

            _panel = HudGraphics.CreateRect("Panel", _root).gameObject;
            RectTransform panel = (RectTransform)_panel.transform;
            HudGraphics.Stretch(panel);
            _background = panel.gameObject.AddComponent<Image>();
            _background.sprite = HudGraphics.WhiteSprite;
            _background.color = BACKGROUND;
            _background.raycastTarget = false;
            panel.gameObject.AddComponent<RectMask2D>();

            _layer = new MapLayer(panel, ICON_SIZE);

            _title = CreateText(panel, "Title", 28, new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 40f));
            _modeButton = CreateButton(panel, out _modeButtonImage, out _modeButtonText);

            _panel.SetActive(false);
        }

        private void OnDestroy()
        {
            ReleaseInput();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard == null)
            {
                return;
            }

            if (keyboard.mKey.wasPressedThisFrame && LevelMap.Current != null)
            {
                if (IsOpen)
                {
                    Close();
                }
                else if (!GameplayInput.IsBlocked)
                {
                    Open();
                }
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (IsOpen && keyboard.tabKey.wasPressedThisFrame)
            {
                // 표시 방식 전환 (임시 단축키, 추후 시스템 설정 화면으로 이동)
                GameSettings.MapMode = _isFullscreen ? MapDisplayMode.Overlay : MapDisplayMode.Fullscreen;
                ApplyMode(GameSettings.MapMode);
            }
        }

        private void Open()
        {
            LevelMap level = LevelMap.Current;
            Vector3 p = _player != null ? _player.position : Vector3.zero;
            _viewCenter = new Vector2(p.x, p.z);
            _viewWidth = level.IsInTown(_viewCenter) ? TOWN_VIEW_WIDTH : FIELD_VIEW_WIDTH;
            _panel.SetActive(true);
            ApplyMode(GameSettings.MapMode);
        }

        private void Close()
        {
            _panel.SetActive(false);
            _dragging = false;
            ReleaseInput();
        }

        private void ApplyMode(MapDisplayMode mode)
        {
            _isFullscreen = mode == MapDisplayMode.Fullscreen;
            _background.enabled = _isFullscreen;
            _layer.SetAlpha(_isFullscreen ? 1f : OVERLAY_ALPHA);
            _modeButton.gameObject.SetActive(_isFullscreen);
            _modeButtonText.text = "오버레이로 보기 (Tab)";
            _title.text = _isFullscreen ? "지도   휠: 확대·축소  끌기: 이동  M·Esc: 닫기" : "지도   휠: 확대·축소  Tab: 전체 화면  M: 닫기";

            // 전체 화면일 때만 게임 입력 차단 (오버레이는 계속 이동 가능)
            if (_isFullscreen && !_blocking)
            {
                _blocking = true;
                GameplayInput.IsBlocked = true;
            }
            else if (!_isFullscreen)
            {
                ReleaseInput();
            }
        }

        private void ReleaseInput()
        {
            if (_blocking)
            {
                _blocking = false;
                GameplayInput.IsBlocked = false;
            }
        }

        private void LateUpdate()
        {
            LevelMap level = LevelMap.Current;

            if (!IsOpen || level == null)
            {
                return;
            }

            float yaw = MapLayer.CameraYaw();
            float screenWidth = _root.rect.width;
            float maxWidth = level.WorldSize * 1.2f;

            HandleZoom(maxWidth);

            if (_isFullscreen)
            {
                HandleDrag(yaw, screenWidth);
                HandleModeButton();
            }
            else if (_player != null)
            {
                Vector3 p = _player.position;
                _viewCenter = new Vector2(p.x, p.z);
            }

            float pixelsPerMeter = screenWidth / _viewWidth;
            _layer.Update(level, _viewCenter, pixelsPerMeter, yaw, _player, 0f, _viewWidth, false);
        }

        private void HandleZoom(float maxWidth)
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            float scroll = mouse.scroll.ReadValue().y;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                _viewWidth *= scroll > 0f ? 1f / ZOOM_STEP : ZOOM_STEP;
                _viewWidth = Mathf.Clamp(_viewWidth, MIN_VIEW_WIDTH, maxWidth);
            }
        }

        // 왼쪽 버튼으로 끌면 지도 이동 (화면 이동량 → 월드 이동량)
        private void HandleDrag(float yaw, float screenWidth)
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();

            if (mouse.leftButton.wasPressedThisFrame && !RectTransformUtility.RectangleContainsScreenPoint(_modeButton, pointer, null))
            {
                _dragging = true;
                _lastPointer = pointer;
            }
            else if (!mouse.leftButton.isPressed)
            {
                _dragging = false;
            }

            if (!_dragging)
            {
                return;
            }

            Vector2 delta = pointer - _lastPointer;
            _lastPointer = pointer;
            float canvasScale = _root.lossyScale.x > 0f ? _root.lossyScale.x : 1f;
            float metersPerScreenPixel = _viewWidth / (screenWidth * canvasScale);
            _viewCenter -= MapLayer.ScreenToWorldDirection(delta, yaw) * metersPerScreenPixel;
        }

        private void HandleModeButton()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            bool hover = RectTransformUtility.RectangleContainsScreenPoint(_modeButton, mouse.position.ReadValue(), null);
            _modeButtonImage.color = hover ? new Color(0.32f, 0.25f, 0.12f, 0.98f) : new Color(0.16f, 0.14f, 0.12f, 0.95f);

            if (hover && mouse.leftButton.wasPressedThisFrame)
            {
                GameSettings.MapMode = MapDisplayMode.Overlay;
                ApplyMode(MapDisplayMode.Overlay);
            }
        }

        private static Text CreateText(Transform parent, string textName, int size, Vector2 anchor, Vector2 position, Vector2 box)
        {
            Text text = HudGraphics.CreateRect(textName, parent).gameObject.AddComponent<Text>();
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = anchor;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = box;
            text.font = HudGraphics.Font;
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = GOLD;
            text.raycastTarget = false;
            Outline outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(0f, 0f, 0f, 0.9f);
            return text;
        }

        private static RectTransform CreateButton(Transform parent, out Image image, out Text label)
        {
            RectTransform button = HudGraphics.CreateRect("ModeButton", parent);
            button.anchorMin = button.anchorMax = new Vector2(1f, 1f);
            button.pivot = new Vector2(1f, 1f);
            button.anchoredPosition = new Vector2(-24f, -24f);
            button.sizeDelta = new Vector2(330f, 42f);
            image = button.gameObject.AddComponent<Image>();
            image.sprite = HudGraphics.WhiteSprite;
            image.raycastTarget = false;
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = new Color(GOLD.r, GOLD.g, GOLD.b, 0.7f);

            label = HudGraphics.CreateRect("Label", button).gameObject.AddComponent<Text>();
            HudGraphics.Stretch(label.rectTransform);
            label.font = HudGraphics.Font;
            label.fontSize = 20;
            label.fontStyle = FontStyle.Bold;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = GOLD;
            label.raycastTarget = false;
            return button;
        }
    }
}
