using BrokenSovereign.Core;
using BrokenSovereign.Map;
using BrokenSovereign.Player;
using BrokenSovereign.Town;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 지도 화면 (10/25: M = 오버레이 열기·닫기, Tab = 전체 화면 열기·닫기, Esc = 공통 닫기).
    /// - 전체 화면: 불투명 바탕으로 화면 전체에 지도. 게임 입력 차단, 마우스 휠 확대·축소, 휠 버튼(또는 왼쪽) 끌기로 이동,
    ///   우클릭 메뉴 [자동 이동]·[핀 포인트 설정]·[핀 해제], Space = 내 위치로. 아래쪽에 조작 안내 (10/21)
    /// - 오버레이: 게임 화면 위에 반투명 지도. 계속 이동 가능, 휠 확대·축소, 플레이어를 따라감.
    ///   휠 버튼 끌기로 지도 이동 (이동하면 따라가기 중지, 다시 열면 복귀). 조작 안내 문구 없음
    /// 처음 열 때 확대 단계: 마을 안 = 마을 전체가 보이는 폭, 마을 밖(전투 지역) = 주변을 크게 확대한 폭.
    /// 아이콘은 확대 단계별로 표시 (상점 등 작은 표시는 축소하면 숨김, MapIconSet.MaxViewWidth).
    /// 위쪽 = 카메라가 바라보는 방향 (미니맵과 같음). HudView가 생성합니다.
    /// 10/25: 오버레이는 화면 가운데 절반 크기 영역에만 표시 (같은 축척, 보이는 범위 절반).
    ///        웨이포인트에서 F → 전체 화면 지도 + 왼쪽 위 웨이포인트 목록 (마우스 올리면 지도가 그 웨이포인트로 이동, 클릭하면 순간 이동)
    /// </summary>
    public class WorldMapView : MonoBehaviour
    {
        private const float TOWN_VIEW_WIDTH = 270f;     // 마을 안에서 처음 보이는 폭 (m, 10/21 확대 줄임)
        private const float FIELD_VIEW_WIDTH = 360f;    // 전투 지역에서 처음 보이는 폭 (m)
        private const float MENU_WIDTH = 190f;
        private const float MENU_BUTTON = 40f;
        private const float MIN_VIEW_WIDTH = 80f;
        private const float MAX_ZOOM_OUT = 2.2f;          // 10/25: 최대 축소 = 맵 크기 × 2.2 (1.2 → 2.2)
        private const float WAYPOINT_FIT_MARGIN = 1.35f;   // 웨이포인트 전체 보기 여유
        private const float ZOOM_STEP = 1.15f;          // 휠 한 칸 배율
        private const float ICON_SIZE = 34f;
        private const float OVERLAY_ALPHA = 0.4f;    // 10/25 투명도 높임 (0.7 → 0.55 → 0.4)
        private const float OVERLAY_SIZE = 0.5f;          // 오버레이 영역 = 화면의 이 비율 (가운데)
        private const float WAYPOINT_LIST_WIDTH = 320f;
        private const float WAYPOINT_ROW = 46f;

        private static readonly Color BACKGROUND = new Color(0.12f, 0.135f, 0.15f, 1f);
        private static Color GOLD => UiTheme.P.Title;

        private Transform _player;
        private RectTransform _root;
        private GameObject _panel;
        private Image _background;
        private MapLayer _layer;
        private Text _title;
        private RectTransform _titleTab;
        private RectTransform _modeButton;
        private Image _modeButtonImage;
        private Text _modeButtonText;
        private PlayerController _controller;
        private RectTransform _legend;
        private RectTransform _menu;
        private Image _menuMove;
        private Image _menuPin;
        private Image _menuUnpin;
        private Vector2 _menuWorld;
        private Text _message;
        private float _messageEndTime;

        private WaypointTraveler _traveler;
        private RectTransform _waypointPanel;
        private static readonly Color CURRENT_ROW = new Color(0.32f, 0.33f, 0.36f, 0.9f);   // 10/26: 현재 웨이포인트 (선택 불가)
        private readonly List<(RectTransform rect, Image background, Text name, Waypoint waypoint)> _waypointRows =
            new List<(RectTransform, Image, Text, Waypoint)>();
        private bool _waypointMode;

        private Vector2 _viewCenter;
        private float _viewWidth;
        private bool _isFullscreen;
        private bool _blocking;
        private bool _dragging;
        private bool _overlayPanned;   // 오버레이에서 휠 버튼으로 옮긴 뒤에는 플레이어를 따라가지 않음
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

            // 10/25: 제목은 다른 창(캐릭터 등)과 같은 알약형 제목 탭
            _title = UiKit.TitleTab(panel, "지도");
            _titleTab = (RectTransform)_title.transform.parent;
            _titleTab.anchoredPosition = new Vector2(0f, -40f);
            _modeButton = CreateButton(panel, out _modeButtonImage, out _modeButtonText);
            _controller = player != null ? player.GetComponent<PlayerController>() : null;
            BuildLegend(panel);
            BuildMenu(panel);
            BuildWaypointPanel(panel);

            _message = UiKit.Label("Message", panel, 20, UiTheme.P.Warning, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 96f), new Vector2(800f, 30f));
            _message.enabled = false;

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

            bool canToggle = LevelMap.Current != null && !_waypointMode;

            if (canToggle && keyboard.mKey.wasPressedThisFrame)
            {
                // M: 오버레이 (전체 화면에서 누르면 오버레이로 전환)
                if (IsOpen && !_isFullscreen)
                {
                    Close();
                }
                else if (IsOpen)
                {
                    ApplyMode(MapDisplayMode.Overlay);
                }
                else if (!GameplayInput.IsBlocked)
                {
                    Open(MapDisplayMode.Overlay);
                }
            }
            else if (canToggle && keyboard.tabKey.wasPressedThisFrame)
            {
                // Tab: 전체 화면 (오버레이에서 누르면 전체 화면으로 전환)
                if (IsOpen && _isFullscreen)
                {
                    Close();
                }
                else if (!GameplayInput.IsBlocked)
                {
                    if (IsOpen)
                    {
                        ApplyMode(MapDisplayMode.Fullscreen);
                    }
                    else
                    {
                        Open(MapDisplayMode.Fullscreen);
                    }
                }
            }
            else if (IsOpen && keyboard.escapeKey.wasPressedThisFrame)
            {
                if (_menu.gameObject.activeSelf)
                {
                    HideMenu();
                }
                else if (_isFullscreen || !GameplayInput.IsBlocked)
                {
                    // 오버레이 위에 다른 창이 열려 있으면 그 창이 먼저 닫힘
                    Close();
                }
            }
            else if (IsOpen && _isFullscreen && keyboard.spaceKey.wasPressedThisFrame && _player != null)
            {
                // 내 위치로
                Vector3 p = _player.position;
                _viewCenter = new Vector2(p.x, p.z);
            }
        }

        private void Open(MapDisplayMode mode)
        {
            LevelMap level = LevelMap.Current;
            Vector3 p = _player != null ? _player.position : Vector3.zero;
            _viewCenter = new Vector2(p.x, p.z);
            _viewWidth = level.IsInTown(_viewCenter) ? TOWN_VIEW_WIDTH : FIELD_VIEW_WIDTH;
            _panel.SetActive(true);
            ApplyMode(mode);
        }

        private void Close()
        {
            _panel.SetActive(false);
            _dragging = false;
            _layer.SetFocus(null);
            HideMenu();
            ReleaseInput();

            if (_waypointMode)
            {
                _waypointMode = false;
                _waypointPanel.gameObject.SetActive(false);
                WaypointTraveler traveler = _traveler;
                _traveler = null;

                if (traveler != null)
                {
                    traveler.Close();
                }
            }
        }

        // ---------- 웨이포인트 목록 (10/25)

        /// <summary>웨이포인트 진입: 전체 화면 지도 + 왼쪽 위 목록 (조작 안내 없음). current = 지금 서 있는 웨이포인트</summary>
        public void ShowWaypoints(WaypointTraveler traveler, Waypoint current)
        {
            if (LevelMap.Current == null || traveler == null)
            {
                return;
            }

            _traveler = traveler;
            _waypointMode = true;

            if (!IsOpen)
            {
                Open(MapDisplayMode.Fullscreen);
            }
            else
            {
                ApplyMode(MapDisplayMode.Fullscreen);
            }
            RebuildWaypointRows(current);
            _waypointPanel.gameObject.SetActive(true);
            FitAllWaypoints();
        }

        // 10/25: 웨이포인트 전체(+ 플레이어)가 한 화면에 들어오도록 가운데·축소 (화면 세로 기준, 회전과 무관하게 원으로 계산)
        private void FitAllWaypoints()
        {
            IReadOnlyList<Waypoint> all = Waypoint.All;

            if (all.Count == 0)
            {
                return;
            }

            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);

            foreach (Waypoint waypoint in all)
            {
                Vector3 p = waypoint.transform.position;
                min = Vector2.Min(min, new Vector2(p.x, p.z));
                max = Vector2.Max(max, new Vector2(p.x, p.z));
            }

            Vector2 center = (min + max) * 0.5f;
            float radius = 0f;

            foreach (Waypoint waypoint in all)
            {
                Vector3 p = waypoint.transform.position;
                radius = Mathf.Max(radius, Vector2.Distance(center, new Vector2(p.x, p.z)));
            }

            Rect screen = _root.rect;
            float aspect = screen.height > 0f ? screen.width / screen.height : 16f / 9f;
            LevelMap level = LevelMap.Current;
            float maxWidth = level != null ? level.WorldSize * MAX_ZOOM_OUT : float.MaxValue;
            _viewCenter = center;
            _viewWidth = Mathf.Clamp(radius * 2f * WAYPOINT_FIT_MARGIN * aspect, MIN_VIEW_WIDTH, maxWidth);
        }

        /// <summary>웨이포인트 창이 다른 경로(F·이동)로 닫혔을 때</summary>
        public void HideWaypoints()
        {
            if (_waypointMode && IsOpen)
            {
                _traveler = null;   // Close가 다시 traveler.Close를 부르지 않게
                Close();
            }
        }

        private void BuildWaypointPanel(RectTransform panel)
        {
            _waypointPanel = UiKit.Plate("WaypointList", panel).rectTransform;
            UiKit.Place(_waypointPanel, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(28f, -20f),   // 10/25: 거의 위쪽 끝
                new Vector2(WAYPOINT_LIST_WIDTH, 120f));
            Text title = UiKit.Title("Title", _waypointPanel, 22, UiTheme.P.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(title.rectTransform, new Vector2(20f, 14f), new Vector2(WAYPOINT_LIST_WIDTH - 40f, 30f));
            title.text = "웨이포인트";
            _waypointPanel.gameObject.SetActive(false);
        }

        private void RebuildWaypointRows(Waypoint current)
        {
            foreach (var row in _waypointRows)
            {
                Destroy(row.rect.gameObject);
            }

            _waypointRows.Clear();
            IReadOnlyList<Waypoint> all = Waypoint.All;

            for (int i = 0; i < all.Count; i++)
            {
                Waypoint waypoint = all[i];
                bool discovered = waypoint.IsDiscovered;
                Image background = UiKit.Box($"Row{i}", _waypointPanel, UiTheme.P.RowA);
                RectTransform rect = background.rectTransform;
                UiKit.PlaceTopLeft(rect, new Vector2(14f, 52f + i * (WAYPOINT_ROW + 4f)), new Vector2(WAYPOINT_LIST_WIDTH - 28f, WAYPOINT_ROW));

                // 10/26: 화살표 제거. 지금 서 있는 웨이포인트는 회색으로 막음 (선택 불가)
                bool isCurrent = waypoint == current;
                Text name = UiKit.Label("Name", rect, 19, discovered && !isCurrent ? UiTheme.P.Text : UiTheme.P.TextDisabled, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(14f, 0f), new Vector2(WAYPOINT_LIST_WIDTH - 56f, WAYPOINT_ROW));
                name.text = waypoint.DisplayName;
                background.color = isCurrent ? CURRENT_ROW : UiTheme.P.RowA;
                _waypointRows.Add((rect, background, name, waypoint));
            }

            _waypointPanel.sizeDelta = new Vector2(WAYPOINT_LIST_WIDTH, 52f + all.Count * (WAYPOINT_ROW + 4f) + 12f);
        }

        // 10/25: 마우스를 올리면 그 웨이포인트 아이콘 위에 역삼각형 표식 (지도 시점은 그대로), 기본 = 플레이어 위치. 클릭하면 순간 이동
        private void HandleWaypointRows()
        {
            Vector2? focus = null;

            if (_player != null)
            {
                Vector3 me = _player.position;
                focus = new Vector2(me.x, me.z);
            }

            foreach (var row in _waypointRows)
            {
                bool hover = UiKit.IsHover(row.rect);
                bool isCurrent = _traveler != null && row.waypoint == _traveler.Current;
                row.background.color = isCurrent ? CURRENT_ROW : hover ? UiTheme.P.Hover : UiTheme.P.RowA;

                if (!hover || row.waypoint == null)
                {
                    continue;
                }

                Vector3 p = row.waypoint.transform.position;
                focus = new Vector2(p.x, p.z);
                _layer.SetFocus(focus);

                if (UiKit.Clicked && row.waypoint.IsDiscovered && _traveler != null && row.waypoint != _traveler.Current)
                {
                    _traveler.TravelTo(row.waypoint);   // 이동 후 traveler가 닫힘 → HideWaypoints
                    return;
                }
            }

            _layer.SetFocus(focus);
        }

        private void ApplyMode(MapDisplayMode mode)
        {
            _isFullscreen = mode == MapDisplayMode.Fullscreen;
            _background.enabled = _isFullscreen;

            // 오버레이: 화면 가운데 절반 크기 (10/25 "오버레이가 너무 넓다")
            RectTransform panelRect = (RectTransform)_panel.transform;
            float inset = _isFullscreen ? 0f : (1f - OVERLAY_SIZE) * 0.5f;
            panelRect.anchorMin = new Vector2(inset, inset);
            panelRect.anchorMax = new Vector2(1f - inset, 1f - inset);
            panelRect.offsetMin = panelRect.offsetMax = Vector2.zero;
            _layer.SetAlpha(_isFullscreen ? 1f : OVERLAY_ALPHA);
            _overlayPanned = false;
            _dragging = false;

            // 조작 안내(제목·버튼·아래 안내)는 일반 전체 화면 지도에서만. 오버레이·웨이포인트 목록에서는 숨김 (10/25)
            bool showHelp = _isFullscreen && !_waypointMode;
            _modeButton.gameObject.SetActive(showHelp);
            _legend.gameObject.SetActive(showHelp);
            _titleTab.gameObject.SetActive(showHelp);
            HideMenu();
            _modeButtonText.text = "오버레이로 보기 (M)";
            _title.text = "지도";

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
            float maxWidth = level.WorldSize * MAX_ZOOM_OUT;

            HandleZoom(maxWidth);
            _message.enabled = Time.unscaledTime < _messageEndTime;

            if (_isFullscreen)
            {
                if (_waypointMode)
                {
                    HandleWaypointRows();

                    if (!_waypointMode)
                    {
                        return;   // 이동으로 지도가 닫힘
                    }
                }

                HandleMenu(yaw, screenWidth);
                HandleDrag(yaw, screenWidth);
                HandleModeButton();
            }
            else
            {
                HandleDrag(yaw, screenWidth);

                if (!_overlayPanned && _player != null)
                {
                    Vector3 p = _player.position;
                    _viewCenter = new Vector2(p.x, p.z);
                }
            }

            // 오버레이는 가운데 절반 영역: 축척은 그대로, 보이는 폭만 영역 비율만큼
            float panelRatio = _isFullscreen ? 1f : OVERLAY_SIZE;
            float pixelsPerMeter = screenWidth / _viewWidth;
            _layer.Update(level, _viewCenter, pixelsPerMeter, yaw, _player, 0f, _viewWidth * panelRatio, false);
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

            // 오버레이: 지도 영역 위에서 휠 버튼만 (왼쪽은 캐릭터 이동). 전체 화면: 휠 버튼 또는 왼쪽 버튼
            bool pressed;
            bool held;

            if (_isFullscreen)
            {
                bool overUi = RectTransformUtility.RectangleContainsScreenPoint(_modeButton, pointer, null)
                    || (_menu.gameObject.activeSelf && UiKit.IsHover(_menu))
                    || (_waypointMode && UiKit.IsHover(_waypointPanel));
                pressed = (mouse.middleButton.wasPressedThisFrame || mouse.leftButton.wasPressedThisFrame) && !overUi;
                held = mouse.leftButton.isPressed || mouse.middleButton.isPressed;
            }
            else
            {
                pressed = mouse.middleButton.wasPressedThisFrame && UiKit.IsHover((RectTransform)_panel.transform);
                held = mouse.middleButton.isPressed;
            }

            if (pressed)
            {
                _dragging = true;
                _lastPointer = pointer;
            }
            else if (!held)
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

            if (!_isFullscreen && delta.sqrMagnitude > 0f)
            {
                _overlayPanned = true;
            }
        }

        private void HandleModeButton()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            bool hover = RectTransformUtility.RectangleContainsScreenPoint(_modeButton, mouse.position.ReadValue(), null);
            _modeButtonImage.color = hover ? new Color(1f, 0.9f, 0.75f) : Color.white;

            if (hover && mouse.leftButton.wasPressedThisFrame)
            {
                ApplyMode(MapDisplayMode.Overlay);
            }
        }

        // ---------- 우클릭 메뉴: 자동 이동 · 핀 포인트

        private void HandleMenu(float yaw, float screenWidth)
        {
            Mouse mouse = Mouse.current;

            if (mouse == null)
            {
                return;
            }

            Vector2 pointer = mouse.position.ReadValue();

            if (_menu.gameObject.activeSelf)
            {
                if (UiKit.IsCloseClicked(_menuMove))
                {
                    AutoMove();
                    return;
                }

                if (UiKit.IsCloseClicked(_menuPin))
                {
                    MapPin.Set(_menuWorld);
                    HideMenu();
                    return;
                }

                if (_menuUnpin.gameObject.activeSelf && UiKit.IsCloseClicked(_menuUnpin))
                {
                    MapPin.Clear();
                    HideMenu();
                    return;
                }

                if (mouse.leftButton.wasPressedThisFrame && !UiKit.IsHover(_menu))
                {
                    HideMenu();
                }
            }

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _menuWorld = ScreenToWorld(pointer, yaw, screenWidth);
                ShowMenu(pointer);
            }
        }

        // 화면 좌표 → 월드 (x, z) (끌기와 같은 변환)
        private Vector2 ScreenToWorld(Vector2 pointer, float yaw, float screenWidth)
        {
            Vector2 center = RectTransformUtility.WorldToScreenPoint(null, _root.TransformPoint(_root.rect.center));
            float canvasScale = _root.lossyScale.x > 0f ? _root.lossyScale.x : 1f;
            float metersPerScreenPixel = _viewWidth / (screenWidth * canvasScale);
            return _viewCenter + MapLayer.ScreenToWorldDirection(pointer - center, yaw) * metersPerScreenPixel;
        }

        private void ShowMenu(Vector2 pointer)
        {
            bool hasPin = MapPin.Has;
            _menuUnpin.gameObject.SetActive(hasPin);
            float height = 16f + MENU_BUTTON * (hasPin ? 3f : 2f) + 6f * (hasPin ? 2f : 1f);
            _menu.sizeDelta = new Vector2(MENU_WIDTH, height);

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_root, pointer, null, out Vector2 local);
            Rect area = _root.rect;
            local.x = Mathf.Clamp(local.x + 8f, area.xMin + 8f, area.xMax - MENU_WIDTH - 8f);
            local.y = Mathf.Clamp(local.y - 8f, area.yMin + height + 8f, area.yMax - 8f);
            _menu.anchoredPosition = local;
            _menu.gameObject.SetActive(true);
            _menu.SetAsLastSibling();
        }

        private void HideMenu()
        {
            if (_menu != null)
            {
                _menu.gameObject.SetActive(false);
            }
        }

        private void AutoMove()
        {
            HideMenu();

            if (_controller == null || !TryGetGround(_menuWorld, out Vector3 target))
            {
                ShowMessage("이동할 수 없는 곳입니다");
                return;
            }

            if (_controller.AutoMoveTo(target))
            {
                Close();
            }
            else
            {
                ShowMessage("지금은 이동할 수 없습니다");
            }
        }

        // 지도 지점의 실제 바닥 위치 (위에서 아래로 쏜 뒤 길찾기 영역에 맞춤)
        private bool TryGetGround(Vector2 world, out Vector3 point)
        {
            float y = _player != null ? _player.position.y : 0f;
            var origin = new Vector3(world.x, y + 500f, world.y);

            if (Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1000f, ~0, QueryTriggerInteraction.Ignore))
            {
                y = hit.point.y;
            }

            if (NavMesh.SamplePosition(new Vector3(world.x, y, world.y), out NavMeshHit navHit, 12f, NavMesh.AllAreas))
            {
                point = navHit.position;
                return true;
            }

            point = default;
            return false;
        }

        private void ShowMessage(string message)
        {
            _message.text = message;
            _messageEndTime = Time.unscaledTime + 1.6f;
        }

        private void BuildMenu(RectTransform panel)
        {
            _menu = UiKit.Plate("ContextMenu", panel).rectTransform;
            _menu.anchorMin = _menu.anchorMax = new Vector2(0.5f, 0.5f);
            _menu.pivot = new Vector2(0f, 1f);
            _menuMove = MenuButton("자동 이동", 0);
            _menuPin = MenuButton("핀 포인트 설정", 1);
            _menuUnpin = MenuButton("핀 해제", 2);
            _menu.gameObject.SetActive(false);
        }

        private Image MenuButton(string label, int index)
        {
            Image button = UiKit.Button(label, _menu, label, 16);
            UiKit.Place(button.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(0f, -8f - index * (MENU_BUTTON + 6f)), new Vector2(MENU_WIDTH - 16f, MENU_BUTTON));
            return button;
        }

        // ---------- 아래쪽 조작 안내: [휠 버튼]+ 드래그 : 지도 이동, [우클릭] : 핀 포인트 및 자동 이동, [Space] : 내 위치로 이동

        private void BuildLegend(RectTransform panel)
        {
            Image plate = UiKit.Plate("Legend", panel);
            _legend = plate.rectTransform;
            UiKit.Place(_legend, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(100f, 56f));
            var layout = _legend.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(24, 24, 8, 8);
            layout.spacing = 10f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = _legend.gameObject.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;

            LegendIcon(UiSprites.MouseWheel, 40f, 40f);
            LegendText("+ 드래그 : 지도 이동", UiTheme.P.Text);
            LegendGap();
            LegendIcon(UiSprites.MouseRight, 40f, 40f);
            LegendText(": 핀 포인트 · 자동 이동", UiTheme.P.Text);
            LegendGap();
            Image space = LegendIcon(UiSprites.Button, 78f, 30f);
            space.type = Image.Type.Sliced;
            Text spaceText = UiKit.Label("Key", space.transform, 13, UiTheme.P.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(spaceText.rectTransform);
            spaceText.text = "SPACE";
            LegendText(": 내 위치로 이동", UiTheme.P.Text);
            LegendGap();
            LegendText("휠 : 확대·축소    Tab · Esc : 닫기", UiTheme.P.TextDim);
        }

        private Image LegendIcon(Sprite sprite, float width, float height)
        {
            Image icon = HudGraphics.CreateRect("Icon", _legend).gameObject.AddComponent<Image>();
            icon.sprite = sprite;
            icon.raycastTarget = false;
            var element = icon.gameObject.AddComponent<LayoutElement>();
            element.preferredWidth = width;
            element.preferredHeight = height;
            return icon;
        }

        private void LegendText(string text, Color color)
        {
            Text label = UiKit.Label("Text", _legend, 17, color, TextAnchor.MiddleLeft);
            label.text = text;
        }

        private void LegendGap()
        {
            var gap = HudGraphics.CreateRect("Gap", _legend).gameObject.AddComponent<LayoutElement>();
            gap.preferredWidth = 22f;
        }

        private static Text CreateText(Transform parent, string textName, int size, Vector2 anchor, Vector2 position, Vector2 box)
        {
            Text text = HudGraphics.CreateRect(textName, parent).gameObject.AddComponent<Text>();
            text.rectTransform.anchorMin = text.rectTransform.anchorMax = anchor;
            text.rectTransform.anchoredPosition = position;
            text.rectTransform.sizeDelta = box;
            UiTheme.ApplyTitle(text);
            text.fontSize = size;
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
            image.sprite = UiSprites.Button;
            image.type = Image.Type.Sliced;
            image.raycastTarget = false;

            label = HudGraphics.CreateRect("Label", button).gameObject.AddComponent<Text>();
            HudGraphics.Stretch(label.rectTransform);
            UiTheme.ApplyBody(label, true);
            label.fontSize = 20;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = GOLD;
            label.raycastTarget = false;
            return button;
        }
    }
}
