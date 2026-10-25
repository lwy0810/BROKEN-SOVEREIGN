using System.Collections.Generic;
using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 웨이포인트 UI: 웨이포인트 위 [F] 안내, 목록 창(발견한 곳만 이동 가능, 현재 위치 표시), 새 웨이포인트 발견 알림.
    /// WaypointTraveler 이벤트를 받아 표시만 하고, 이동·닫기는 WaypointTraveler에 요청합니다. HudView가 생성합니다.
    /// </summary>
    public class WaypointWindowView : MonoBehaviour
    {
        private const float PANEL_WIDTH = 460f;
        private const float ROW_HEIGHT = 56f;
        private const float ROW_GAP = 6f;
        private const float TOP = 70f;
        private const float PADDING = 24f;
        private const float TOAST_SECONDS = 2.5f;
        private static readonly Vector2 PROMPT_SIZE = new Vector2(260f, 40f);

        private WaypointTraveler _traveler;
        private RectTransform _root;
        private RectTransform _prompt;
        private Text _promptText;
        private RectTransform _panel;
        private Image _close;
        private Text _toast;
        private float _toastUntil;
        private readonly List<Row> _rows = new List<Row>();

        private sealed class Row
        {
            public RectTransform Rect;
            public Image Background;
            public Text Name;
            public Text State;
            public Waypoint Target;
        }

        public void Initialize(WaypointTraveler traveler)
        {
            _traveler = traveler;
            _root = (RectTransform)transform;
            HudGraphics.Stretch(_root);

            BuildPrompt();
            BuildPanel();
            BuildToast();

            _traveler.Opened += OnOpened;
            _traveler.Closed += OnClosed;
            Waypoint.AnyDiscovered += OnDiscovered;
        }

        private void OnDestroy()
        {
            if (_traveler != null)
            {
                _traveler.Opened -= OnOpened;
                _traveler.Closed -= OnClosed;
            }

            Waypoint.AnyDiscovered -= OnDiscovered;
        }

        private void LateUpdate()
        {
            if (_traveler == null)
            {
                return;
            }

            UpdatePrompt();
            _toast.gameObject.SetActive(Time.unscaledTime < _toastUntil);

            if (_panel.gameObject.activeSelf)
            {
                UpdateRows();
            }
        }

        private void UpdatePrompt()
        {
            Waypoint focus = _traveler.Focus;
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
            _promptText.text = $"[F] 웨이포인트 · {focus.DisplayName}";
            _prompt.gameObject.SetActive(true);
        }

        private void OnOpened(Waypoint current)
        {
            Rebuild(current);
            _panel.gameObject.SetActive(true);
        }

        private void OnClosed()
        {
            _panel.gameObject.SetActive(false);
        }

        private void OnDiscovered(Waypoint waypoint)
        {
            _toast.text = $"웨이포인트 발견: {waypoint.DisplayName}";
            _toastUntil = Time.unscaledTime + TOAST_SECONDS;
        }

        private void UpdateRows()
        {
            if (UiKit.IsCloseClicked(_close))
            {
                _traveler.Close();
                return;
            }

            foreach (Row row in _rows)
            {
                bool usable = row.Target.IsDiscovered && row.Target != _traveler.Current;
                bool hover = usable && UiKit.IsHover(row.Rect);
                row.Background.color = hover ? UiTheme.P.Hover : UiTheme.P.RowA;

                if (hover && UiKit.Clicked)
                {
                    _traveler.TravelTo(row.Target);
                    return;
                }
            }
        }

        // 열 때마다 목록 다시 만듦 (웨이포인트 수가 적음)
        private void Rebuild(Waypoint current)
        {
            foreach (Row row in _rows)
            {
                Destroy(row.Rect.gameObject);
            }

            _rows.Clear();
            IReadOnlyList<Waypoint> all = Waypoint.All;

            for (int i = 0; i < all.Count; i++)
            {
                Waypoint waypoint = all[i];
                var row = new Row { Target = waypoint };
                row.Background = UiKit.Box($"Row{i}", _panel, UiTheme.P.RowA);
                row.Rect = row.Background.rectTransform;
                UiKit.PlaceTopLeft(row.Rect, new Vector2(PADDING, TOP + i * (ROW_HEIGHT + ROW_GAP)), new Vector2(PANEL_WIDTH - PADDING * 2f, ROW_HEIGHT));

                bool discovered = waypoint.IsDiscovered;
                row.Name = UiKit.Label("Name", row.Rect, 20, discovered ? UiTheme.P.Text : UiTheme.P.TextDisabled, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Place(row.Name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(18f, 0f), new Vector2(260f, ROW_HEIGHT));
                row.Name.text = discovered ? waypoint.DisplayName : "???";

                string state = waypoint == current ? "현재 위치" : discovered ? "이동" : "미발견";
                Color stateColor = waypoint == current ? UiTheme.P.Title : discovered ? UiTheme.P.Accent : UiTheme.P.TextDisabled;
                row.State = UiKit.Label("State", row.Rect, 16, stateColor, TextAnchor.MiddleRight);
                UiKit.Place(row.State.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-18f, 0f), new Vector2(120f, ROW_HEIGHT));
                row.State.text = state;
                _rows.Add(row);
            }

            float height = TOP + all.Count * (ROW_HEIGHT + ROW_GAP) + PADDING;
            _panel.sizeDelta = new Vector2(PANEL_WIDTH, height);
        }

        private void BuildPrompt()
        {
            Image background = UiKit.Sliced("WaypointPrompt", _root, UiSprites.Button);
            _prompt = background.rectTransform;
            _prompt.anchorMin = _prompt.anchorMax = new Vector2(0.5f, 0.5f);
            _prompt.pivot = new Vector2(0.5f, 0f);
            _prompt.sizeDelta = PROMPT_SIZE;

            _promptText = UiKit.Label("Label", _prompt, 20, UiTheme.P.Title, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_promptText.rectTransform);
            _prompt.gameObject.SetActive(false);
        }

        private void BuildPanel()
        {
            Image plate = UiKit.Plate("WaypointWindow", _root);
            _panel = plate.rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PANEL_WIDTH, 300f));
            UiKit.TitleTab(_panel, "웨이포인트");
            _close = UiKit.CloseButton(_panel);
            _panel.gameObject.SetActive(false);
        }

        private void BuildToast()
        {
            _toast = UiKit.SceneLabel("WaypointToast", _root, 24, UiTheme.P.Title, TextAnchor.MiddleCenter);
            UiKit.Place(_toast.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(600f, 40f));
            _toast.gameObject.SetActive(false);
        }
    }
}
