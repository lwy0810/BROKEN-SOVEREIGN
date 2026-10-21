using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 코드로 만드는 UI 공용 함수: 글자·금속판·막대·버튼 생성, 배치, 마우스 판정.
    /// 색·서체는 UiTheme(B안 기본), 판·막대 모양은 UiSprites. 다른 화면과 같이 EventSystem 없이 마우스 위치를 직접 판정합니다.
    /// </summary>
    public static class UiKit
    {
        // 이전 이름 호환 (테마 색으로 연결)
        public static Color GOLD => UiTheme.P.Title;
        public static Color SUBTLE => UiTheme.P.Text;
        public static Color DIM_TEXT => UiTheme.P.TextDim;
        public static Color ROW => UiTheme.P.RowA;
        public static Color ROW_HOVER => UiTheme.P.Hover;

        /// <summary>본문 글자 (Bold면 굵은 서체)</summary>
        public static Text Label(string name, Transform parent, int size, Color color, TextAnchor anchor,
            FontStyle style = FontStyle.Normal)
        {
            Text text = HudGraphics.CreateRect(name, parent).gameObject.AddComponent<Text>();
            UiTheme.ApplyBody(text, style == FontStyle.Bold);
            text.fontSize = size;
            text.color = color;
            text.alignment = anchor;
            text.raycastTarget = false;
            text.supportRichText = true;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            Shadow shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = UiTheme.P.CrackGlow ? new Color(0f, 0f, 0f, 0.85f) : new Color(1f, 1f, 1f, 0.35f);
            shadow.effectDistance = new Vector2(1.5f, -1.5f);
            return text;
        }

        /// <summary>제목·이름 글자 (세리프)</summary>
        public static Text Title(string name, Transform parent, int size, Color color, TextAnchor anchor)
        {
            Text text = Label(name, parent, size, color, anchor);
            UiTheme.ApplyTitle(text);
            return text;
        }

        /// <summary>장면 위에 바로 띄우는 글자 (피해 숫자·효과 이름 등). 테마의 장면 그림자</summary>
        public static Text SceneLabel(string name, Transform parent, int size, Color color, TextAnchor anchor, bool bold = true)
        {
            Text text = Label(name, parent, size, color, anchor, bold ? FontStyle.Bold : FontStyle.Normal);
            Shadow shadow = text.GetComponent<Shadow>();
            shadow.effectColor = UiTheme.P.SceneShadow;
            text.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(UiTheme.P.SceneShadow, UiTheme.P.SceneShadow.a * 0.6f);
            return text;
        }

        /// <summary>흰 그림에 색을 입힌 사각형</summary>
        public static Image Box(string name, Transform parent, Color color)
        {
            Image image = HudGraphics.CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = HudGraphics.WhiteSprite;
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>둥근 사각형 (행·강조)</summary>
        public static Image Round(string name, Transform parent, Color color)
        {
            Image image = Sliced(name, parent, UiSprites.Round);
            image.color = color;
            return image;
        }

        /// <summary>금속판 (테두리·안쪽 선·세로 그라데이션 포함). 그림자 포함</summary>
        public static Image Plate(string name, Transform parent)
        {
            Image plate = Sliced(name, parent, UiSprites.Plate);
            Shadow shadow = plate.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0f, 0f, 0f, UiTheme.P.CrackGlow ? 0.55f : 0.35f);
            shadow.effectDistance = new Vector2(0f, -5f);
            return plate;
        }

        /// <summary>이전 이름 호환</summary>
        public static Image Panel(string name, Transform parent)
        {
            return Plate(name, parent);
        }

        /// <summary>창 위쪽 가운데 제목 탭 (알약형 판 + 세리프 제목)</summary>
        public static Text TitleTab(RectTransform panel, string title, float width = 220f)
        {
            Image tab = Sliced("TitleTab", panel, UiSprites.Button);
            Place(tab.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 0.5f), new Vector2(0f, -8f), new Vector2(width, 52f));
            Text text = Title("Title", tab.transform, 26, UiTheme.P.Title, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(text.rectTransform);
            text.text = title;
            return text;
        }

        public static Image Sliced(string name, Transform parent, Sprite sprite)
        {
            Image image = HudGraphics.CreateRect(name, parent).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>버튼 (알약형). 글자는 반환값의 자식 "Label"</summary>
        public static Image Button(string name, Transform parent, string label, int fontSize = 15)
        {
            Image button = Sliced(name, parent, UiSprites.Button);
            Text text = Label("Label", button.transform, fontSize, UiTheme.P.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        /// <summary>창 오른쪽 위 닫기 버튼(×). 클릭 판정은 IsCloseClicked로</summary>
        public static Image CloseButton(RectTransform panel)
        {
            Image button = Sliced("Close", panel, UiSprites.Button);
            Place(button.rectTransform, new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(-16f, -16f), new Vector2(28f, 28f));

            // 글꼴 기준선 탓에 ×가 어긋나 보이므로 선 두 개로 정중앙에 그림
            for (int i = 0; i < 2; i++)
            {
                Image line = Box("X", button.transform, UiTheme.P.Text);
                Place(line.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(2f, 14f));
                line.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i == 0 ? 45f : -45f);
            }

            return button;
        }

        /// <summary>닫기 버튼 마우스 올림 강조 + 이번 프레임 클릭 여부</summary>
        public static bool IsCloseClicked(Image button)
        {
            bool hover = IsHover(button.rectTransform);
            button.color = hover ? new Color(1f, 0.8f, 0.65f) : Color.white;
            return hover && Clicked;
        }

        /// <summary>막대 (알약형 바탕 + 채움). 바깥 크기는 반환값.Rect로 배치</summary>
        public static UiBar Bar(string name, Transform parent, Color fill, Color fillDark)
        {
            return new UiBar(name, parent, fill, fillDark);
        }

        /// <summary>이전 이름 호환: 가로로 채워지는 사각 막대 (BossHud 등 단순 표시용)</summary>
        public static Image Fill(string name, Transform parent, Color color)
        {
            Image fill = Box(name, parent, color);
            HudGraphics.Stretch(fill.rectTransform);
            fill.type = Image.Type.Filled;
            fill.fillMethod = Image.FillMethod.Horizontal;
            fill.fillOrigin = (int)Image.OriginHorizontal.Left;
            return fill;
        }

        /// <summary>부모 기준 anchor 지점에 pivot을 맞춰 배치</summary>
        public static void Place(RectTransform rect, Vector2 anchor, Vector2 pivot, Vector2 position, Vector2 size)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
        }

        /// <summary>부모 왼쪽 위 기준 배치 (목록 행 배치용)</summary>
        public static void PlaceTopLeft(RectTransform rect, Vector2 position, Vector2 size)
        {
            Place(rect, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(position.x, -position.y), size);
        }

        /// <summary>부모를 offset만큼 바깥으로 넓혀 채움 (판을 내용보다 크게)</summary>
        public static void Surround(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(-left, -bottom);
            rect.offsetMax = new Vector2(right, top);
        }

        public static Vector2 Pointer
        {
            get
            {
                Mouse mouse = Mouse.current;
                return mouse != null ? mouse.position.ReadValue() : new Vector2(-1f, -1f);
            }
        }

        public static bool Clicked => Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame;

        public static bool IsHover(RectTransform rect)
        {
            return rect != null && rect.gameObject.activeInHierarchy &&
                RectTransformUtility.RectangleContainsScreenPoint(rect, Pointer, null);
        }
    }

    /// <summary>
    /// 창 끌어 옮기기: 손잡이(제목 탭)를 누른 채 끌면 창이 따라 움직임. 화면 밖으로는 나가지 않음.
    /// 위치는 닫았다 열어도 유지됩니다.
    /// </summary>
    public sealed class WindowDrag
    {
        private const float TAB_OVERHANG = 22f;

        private readonly RectTransform _panel;
        private readonly RectTransform _handle;
        private readonly Image _handleImage;
        private bool _dragging;
        private Vector2 _grab;

        public bool IsDragging => _dragging;

        public WindowDrag(RectTransform panel, RectTransform handle)
        {
            _panel = panel;
            _handle = handle;
            _handleImage = handle.GetComponent<Image>();
        }

        public void Tick()
        {
            Mouse mouse = Mouse.current;
            RectTransform parent = _panel.parent as RectTransform;

            if (mouse == null || parent == null)
            {
                return;
            }

            bool hover = UiKit.IsHover(_handle);

            if (_handleImage != null)
            {
                _handleImage.color = hover || _dragging ? new Color(1f, 0.9f, 0.78f) : Color.white;
            }

            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(parent, UiKit.Pointer, null, out Vector2 local))
            {
                return;
            }

            if (mouse.leftButton.wasPressedThisFrame && hover)
            {
                _dragging = true;
                _grab = local - _panel.anchoredPosition;
            }
            else if (!mouse.leftButton.isPressed)
            {
                _dragging = false;
            }

            if (_dragging)
            {
                _panel.anchoredPosition = Clamp(local - _grab, parent);
            }
        }

        // 창 전체(위로 튀어나온 제목 탭 포함)가 화면 안에 머물도록
        private Vector2 Clamp(Vector2 position, RectTransform parent)
        {
            Rect area = parent.rect;
            Vector2 half = _panel.rect.size * 0.5f;
            float minX = area.xMin + half.x, maxX = area.xMax - half.x;
            float minY = area.yMin + half.y, maxY = area.yMax - half.y - TAB_OVERHANG;
            position.x = minX < maxX ? Mathf.Clamp(position.x, minX, maxX) : area.center.x;
            position.y = minY < maxY ? Mathf.Clamp(position.y, minY, maxY) : area.center.y;
            return position;
        }
    }

    /// <summary>
    /// 알약형 막대: 바탕(움푹) + 채움(어두운→밝은 색, 위쪽 반사광) + 선택적 흔적(깎인 만큼 늦게 따라옴)·보호막(오른쪽부터).
    /// 채움은 너비를 비율만큼 줄여 둥근 끝을 유지합니다.
    /// </summary>
    public sealed class UiBar
    {
        private readonly RectTransform _area;
        private readonly Image _fill;
        private readonly Image _shine;
        private Image _trail;
        private Image _shield;
        private Text _text;

        public RectTransform Rect { get; }
        public Image Frame { get; }
        public Image FillImage => _fill;

        public UiBar(string name, Transform parent, Color fill, Color fillDark)
        {
            Frame = UiKit.Sliced(name, parent, UiSprites.BarFrame);
            Rect = Frame.rectTransform;

            _area = HudGraphics.CreateRect("Area", Rect);
            HudGraphics.Stretch(_area);
            _area.offsetMin = new Vector2(2f, 2f);
            _area.offsetMax = new Vector2(-2f, -2f);

            _fill = UiKit.Sliced("Fill", _area, UiSprites.Pill);
            _shine = UiKit.Sliced("Shine", _fill.transform, UiSprites.Pill);
            _shine.rectTransform.anchorMin = new Vector2(0f, 0.55f);
            _shine.rectTransform.anchorMax = Vector2.one;
            _shine.rectTransform.offsetMin = new Vector2(2f, 0f);
            _shine.rectTransform.offsetMax = new Vector2(-2f, -1f);
            _shine.color = new Color(1f, 1f, 1f, 0.22f);
            SetColor(fill, fillDark);
            SetRatio(1f);
        }

        /// <summary>깎인 흔적 막대 (채움 뒤)</summary>
        public void EnableTrail(Color color)
        {
            _trail = UiKit.Sliced("Trail", _area, UiSprites.Pill);
            _trail.color = color;
            _trail.transform.SetAsFirstSibling();
            SetBarWidth(_trail.rectTransform, 0f, 1f);
        }

        /// <summary>보호막 막대 (오른쪽 끝부터)</summary>
        public void EnableShield(Color color)
        {
            _shield = UiKit.Sliced("Shield", _area, UiSprites.Pill);
            _shield.color = color;
            SetBarWidth(_shield.rectTransform, 1f, 1f);
        }

        /// <summary>막대 가운데 글자</summary>
        public Text EnableText(int size)
        {
            _text = UiKit.Label("Text", Rect, size, Color.white, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_text.rectTransform);
            _text.GetComponent<Shadow>().effectColor = new Color(0f, 0f, 0f, 0.9f);
            return _text;
        }

        public Text Text => _text;

        public void SetColor(Color fill, Color fillDark)
        {
            // 위쪽 밝게: 채움은 단색, 아래쪽 어둡게 보이도록 바탕 띠 추가 대신 채움색을 두 색의 중간으로
            _fill.color = Color.Lerp(fillDark, fill, 0.8f);
        }

        public void SetRatio(float ratio)
        {
            SetBarWidth(_fill.rectTransform, 0f, Mathf.Clamp01(ratio));
            _fill.enabled = ratio > 0.001f;
        }

        public void SetTrail(float ratio)
        {
            if (_trail != null)
            {
                SetBarWidth(_trail.rectTransform, 0f, Mathf.Clamp01(ratio));
                _trail.enabled = ratio > 0.001f;
            }
        }

        public void SetShield(float ratio)
        {
            if (_shield != null)
            {
                SetBarWidth(_shield.rectTransform, 1f - Mathf.Clamp01(ratio), 1f);
                _shield.enabled = ratio > 0.001f;
            }
        }

        private static void SetBarWidth(RectTransform rect, float from, float to)
        {
            rect.anchorMin = new Vector2(from, 0f);
            rect.anchorMax = new Vector2(Mathf.Max(from, to), 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
