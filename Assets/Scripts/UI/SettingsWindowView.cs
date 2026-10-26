using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 설정 창 (10/26, Figma 14-A). 타이틀 [설정]·HUD 메뉴 [설정]에서 열림.
    /// 값을 바꾸면 창 안에서만 바뀌고(초안), [적용] → "변경 사항을 적용하시겠습니까?" 확인 후 GameSettings에 저장·적용.
    /// 적용하지 않은 변경이 있을 때 닫으면(× · Esc · [닫기]) "변경 사항이 있습니다. 적용 하시겠습니까?" → [적용] 저장 후 닫기 / [취소] 버리고 닫기.
    /// - 사운드: 전체 음량 · 효과음 · 배경 음악 (슬라이더)
    /// - 화면: 전체 화면(토글) · 해상도(3단) · 그래픽 품질(낮음/높음)
    /// - 게임: 피해 숫자 표시(토글) · 마우스 감도 50~200% (휠 줌·지도 이동)
    /// - [기본값](창에 기본값 표시, [적용]해야 저장) · [적용] · [닫기] · × · Esc
    /// 다른 화면과 같이 EventSystem 없이 마우스 위치로 판정 (UiKit). 슬라이더: 클릭·드래그, 마우스를 올리고 ←→ 5%.
    /// </summary>
    public class SettingsWindowView : MonoBehaviour
    {
        private const float WIDTH = 760f;
        private const float HEIGHT = 716f;
        private const float BUTTON_Y = HEIGHT - 80f;
        private const float BUTTON_H = 52f;
        private const float SIDE_BUTTON_W = 140f;
        private const float MAIN_BUTTON_W = 160f;

        private const string APPLY_MESSAGE = "변경 사항을 적용하시겠습니까?";
        private const string CLOSE_MESSAGE = "변경 사항이 있습니다. 적용 하시겠습니까?";

        /// <summary>창 안에서 바꾸는 값 (적용 전 초안)</summary>
        private struct Values
        {
            public float Master, Sfx, Music, Sensitivity;
            public bool Fullscreen, HighQuality, DamageNumbers;
            public int Resolution;

            public static Values Load() => new Values
            {
                Master = GameSettings.MasterVolume,
                Sfx = GameSettings.SfxVolume,
                Music = GameSettings.MusicVolume,
                Fullscreen = GameSettings.Fullscreen,
                Resolution = GameSettings.ResolutionIndex,
                HighQuality = GameSettings.HighQuality,
                DamageNumbers = GameSettings.ShowDamageNumbers,
                Sensitivity = GameSettings.MouseSensitivity
            };

            public static Values Defaults() => new Values
            {
                Master = GameSettings.DEFAULT_MASTER,
                Sfx = GameSettings.DEFAULT_SFX,
                Music = GameSettings.DEFAULT_MUSIC,
                Fullscreen = true,
                Resolution = GameSettings.RESOLUTIONS.Length - 1,
                HighQuality = true,
                DamageNumbers = true,
                Sensitivity = 1f
            };

            public bool SameAs(Values o) =>
                Mathf.Approximately(Master, o.Master) && Mathf.Approximately(Sfx, o.Sfx) && Mathf.Approximately(Music, o.Music) &&
                Mathf.Approximately(Sensitivity, o.Sensitivity) && Fullscreen == o.Fullscreen && Resolution == o.Resolution &&
                HighQuality == o.HighQuality && DamageNumbers == o.DamageNumbers;

            // 바뀐 값만 저장 (화면·품질은 바뀔 때만 다시 적용)
            public void Save(Values saved)
            {
                if (!Mathf.Approximately(Master, saved.Master)) GameSettings.MasterVolume = Master;
                if (!Mathf.Approximately(Sfx, saved.Sfx)) GameSettings.SfxVolume = Sfx;
                if (!Mathf.Approximately(Music, saved.Music)) GameSettings.MusicVolume = Music;
                if (Fullscreen != saved.Fullscreen) GameSettings.Fullscreen = Fullscreen;
                if (Resolution != saved.Resolution) GameSettings.ResolutionIndex = Resolution;
                if (HighQuality != saved.HighQuality) GameSettings.HighQuality = HighQuality;
                if (DamageNumbers != saved.DamageNumbers) GameSettings.ShowDamageNumbers = DamageNumbers;
                if (!Mathf.Approximately(Sensitivity, saved.Sensitivity)) GameSettings.MouseSensitivity = Sensitivity;
            }
        }

        private enum ConfirmMode { None, Apply, Close }
        private const float LABEL_X = 56f;
        private const float CONTROL_X = 240f;
        private const float VALUE_X = 640f;
        private const float TRACK_W = 360f;
        private const float ROW = 48f;
        private const float KEY_STEP = 0.05f;

        private sealed class SliderRow
        {
            public RectTransform Hit;
            public RectTransform Fill;
            public RectTransform Knob;
            public Text Value;
            public Func<float> Get;      // 0~1
            public Action<float> Set;
            public Func<float, string> Format;
        }

        private sealed class ToggleRow
        {
            public RectTransform Hit;
            public Image Track;
            public RectTransform Knob;
            public Text State;
            public Func<bool> Get;
            public Action<bool> Set;
        }

        private sealed class ChoiceRow
        {
            public List<(Image box, Text text)> Chips = new List<(Image, Text)>();
            public Func<int> Get;
            public Action<int> Set;
        }

        private RectTransform _root;
        private RectTransform _panel;
        private Image _close;
        private Image _resetButton;
        private Image _applyButton;
        private Image _doneButton;
        private Values _draft;
        private ConfirmMode _confirm;
        private GameObject _confirmRoot;
        private Text _confirmText;
        private Image _confirmYes;
        private Image _confirmNo;
        private readonly List<SliderRow> _sliders = new List<SliderRow>();
        private readonly List<ToggleRow> _toggles = new List<ToggleRow>();
        private readonly List<ChoiceRow> _choices = new List<ChoiceRow>();
        private SliderRow _dragging;

        public bool IsOpen => _root != null && _root.gameObject.activeSelf;

        /// <summary>닫혔을 때</summary>
        public event Action Closed;

        public static SettingsWindowView Create(RectTransform parent)
        {
            RectTransform root = HudGraphics.CreateRect("SettingsWindow", parent);
            HudGraphics.Stretch(root);
            SettingsWindowView view = root.gameObject.AddComponent<SettingsWindowView>();
            view.Build(root);
            root.gameObject.SetActive(false);
            return view;
        }

        private Text _hint;

        /// <summary>타이틀 화면용 (10/26): 아래 "Esc  닫기" 안내를 숨김</summary>
        public void HideEscHint()
        {
            if (_hint != null)
            {
                _hint.gameObject.SetActive(false);
            }
        }

        private bool IsDirty => !_draft.SameAs(Values.Load());

        public void Open()
        {
            _draft = Values.Load();
            _confirm = ConfirmMode.None;
            _confirmRoot.SetActive(false);
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Refresh();
        }

        public void Close()
        {
            _dragging = null;
            _confirm = ConfirmMode.None;
            _confirmRoot.SetActive(false);
            _root.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        // ------------------------------------------------------------------
        // 구성 (Figma 14-A: 760×688, 위에서 아래로)
        // ------------------------------------------------------------------

        private void Build(RectTransform root)
        {
            _root = root;
            UiPalette p = UiTheme.P;

            Image dim = UiKit.Box("Dim", root, new Color(0f, 0f, 0f, 0.62f));
            HudGraphics.Stretch(dim.rectTransform);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, -10f), new Vector2(WIDTH, HEIGHT));

            // 이름 탭 (왼쪽 위, 테두리에 걸침)
            Image tab = UiKit.Sliced("TitleTab", _panel, UiSprites.Button);
            UiKit.Place(tab.rectTransform, new Vector2(0f, 1f), new Vector2(0f, 0.5f), new Vector2(32f, 0f), new Vector2(150f, 48f));
            Text title = UiKit.Title("Title", tab.transform, 24, p.Title, TextAnchor.MiddleCenter);
            HudGraphics.Stretch(title.rectTransform);
            title.text = "설  정";

            _close = UiKit.CloseButton(_panel);
            _close.rectTransform.sizeDelta = new Vector2(40f, 40f);
            _close.rectTransform.anchoredPosition = new Vector2(-18f, -18f);

            float y = 58f;
            y = Section("사운드", y);
            y = Slider("전체 음량", y, () => _draft.Master, v => _draft.Master = v, Percent);
            y = Slider("효과음", y, () => _draft.Sfx, v => _draft.Sfx = v, Percent);
            y = Slider("배경 음악", y, () => _draft.Music, v => _draft.Music = v, Percent);

            y = Section("화면", y + 6f);
            y = Toggle("전체 화면", y, () => _draft.Fullscreen, v => _draft.Fullscreen = v);
            var resolutions = new string[GameSettings.RESOLUTIONS.Length];

            for (int i = 0; i < resolutions.Length; i++)
            {
                resolutions[i] = $"{GameSettings.RESOLUTIONS[i].x}×{GameSettings.RESOLUTIONS[i].y}";
            }

            y = Choice("해상도", y, resolutions, () => _draft.Resolution, i => _draft.Resolution = i);
            y = Choice("그래픽 품질", y, new[] { "낮음", "높음" }, () => _draft.HighQuality ? 1 : 0, i => _draft.HighQuality = i == 1);

            y = Section("게임", y + 6f);
            y = Toggle("피해 숫자 표시", y, () => _draft.DamageNumbers, v => _draft.DamageNumbers = v);

            // 마우스 감도: 0~1 ↔ 50~200%
            float range = GameSettings.MAX_SENSITIVITY - GameSettings.MIN_SENSITIVITY;
            float sensitivityY = y;
            Slider("마우스 감도", y,
                () => (_draft.Sensitivity - GameSettings.MIN_SENSITIVITY) / range,
                v => _draft.Sensitivity = GameSettings.MIN_SENSITIVITY + Mathf.Round(v * range * 20f) / 20f,
                v => $"{Mathf.RoundToInt((GameSettings.MIN_SENSITIVITY + v * range) * 100f)}%",
                labelOffset: -3f);
            // 보조 글: 이름과 10px 띄움 (10/26 여백 규칙)
            Text sub = UiKit.Label("Sub", _panel, 13, p.TextDim, TextAnchor.UpperLeft);
            UiKit.PlaceTopLeft(sub.rectTransform, new Vector2(LABEL_X, sensitivityY + 36f), new Vector2(200f, 18f));
            sub.text = "휠 줌 · 지도 이동 (50~200%)";

            // 아래 버튼 (10/26): [기본값] · Esc 안내 · [적용] · [닫기]. 모두 같은 색, 마우스를 올릴 때만 강조
            float doneX = WIDTH - 40f - MAIN_BUTTON_W;
            float applyX = doneX - 16f - MAIN_BUTTON_W;
            _resetButton = BottomButton("기본값", new Vector2(40f, BUTTON_Y), SIDE_BUTTON_W);
            _applyButton = BottomButton("적용", new Vector2(applyX, BUTTON_Y), MAIN_BUTTON_W);
            _doneButton = BottomButton("닫기", new Vector2(doneX, BUTTON_Y), MAIN_BUTTON_W);
            Text hint = UiKit.Label("Hint", _panel, 15, p.TextDim, TextAnchor.MiddleCenter);
            float hintX = 40f + SIDE_BUTTON_W;
            UiKit.PlaceTopLeft(hint.rectTransform, new Vector2(hintX, BUTTON_Y + 14f), new Vector2(applyX - hintX, 24f));
            hint.text = "Esc  닫기";
            _hint = hint;

            BuildConfirm();
        }

        // 확인 팝업: 창 위를 어둡게 덮고 가운데 작은 판
        private void BuildConfirm()
        {
            UiPalette p = UiTheme.P;
            Image cover = UiKit.Box("ConfirmCover", _panel, new Color(0f, 0f, 0f, 0.6f));
            HudGraphics.Stretch(cover.rectTransform);
            _confirmRoot = cover.gameObject;

            RectTransform box = UiKit.Plate("ConfirmBox", cover.transform).rectTransform;
            UiKit.Place(box, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 210f));
            box.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.Accent, 0.8f);

            _confirmText = UiKit.Label("Message", box, 20, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_confirmText.rectTransform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(420f, 50f));

            _confirmYes = BottomButton("적용", Vector2.zero, 150f, box);
            UiKit.Place(_confirmYes.rectTransform, new Vector2(0.5f, 0f), new Vector2(1f, 0f), new Vector2(-8f, 32f), new Vector2(150f, 48f));
            _confirmNo = BottomButton("취소", Vector2.zero, 150f, box);
            UiKit.Place(_confirmNo.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 0f), new Vector2(8f, 32f), new Vector2(150f, 48f));

            _confirmRoot.SetActive(false);
        }

        private void ShowConfirm(ConfirmMode mode)
        {
            _confirm = mode;
            _dragging = null;
            _confirmText.text = mode == ConfirmMode.Apply ? APPLY_MESSAGE : CLOSE_MESSAGE;
            _confirmRoot.SetActive(true);
        }

        // 저장 후 초안을 저장된 값으로 맞춤
        private void ApplyDraft()
        {
            _draft.Save(Values.Load());
            _draft = Values.Load();
            Refresh();
        }

        // × · Esc · [닫기]: 적용하지 않은 변경이 있으면 확인
        private void RequestClose()
        {
            if (IsDirty)
            {
                ShowConfirm(ConfirmMode.Close);
            }
            else
            {
                Close();
            }
        }

        // 확인 팝업 입력. 팝업이 떠 있으면 true (창의 다른 입력은 막음)
        private bool UpdateConfirm(Keyboard keyboard)
        {
            if (_confirm == ConfirmMode.None)
            {
                return false;
            }

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                // Esc: 팝업만 닫고 창으로 돌아감
                _confirm = ConfirmMode.None;
                _confirmRoot.SetActive(false);
                return true;
            }

            bool yes = ButtonClicked(_confirmYes) || (keyboard != null && keyboard.enterKey.wasPressedThisFrame);
            bool no = ButtonClicked(_confirmNo);
            ConfirmMode mode = _confirm;

            if (yes)
            {
                ApplyDraft();
            }

            if (yes || no)
            {
                _confirm = ConfirmMode.None;
                _confirmRoot.SetActive(false);

                if (mode == ConfirmMode.Close)
                {
                    // 취소: 적용하지 않은 변경은 버리고 닫음
                    Close();
                }
            }

            return true;
        }

        private float Section(string name, float y)
        {
            UiPalette p = UiTheme.P;
            Text text = UiKit.Label("Section", _panel, 18, p.Accent, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(text.rectTransform, new Vector2(40f, y), new Vector2(300f, 26f));
            text.text = name;
            Image line = UiKit.Box("Line", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.6f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(40f, y + 30f), new Vector2(WIDTH - 80f, 1f));
            return y + 46f;
        }

        private Text RowLabel(string label, float y, float offset = 0f)
        {
            Text text = UiKit.Label("Label", _panel, 18, UiTheme.P.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(text.rectTransform, new Vector2(LABEL_X, y + 4f + offset), new Vector2(180f, 28f));
            text.text = label;
            return text;
        }

        private float Slider(string label, float y, Func<float> get, Action<float> set, Func<float, string> format, float labelOffset = 0f)
        {
            UiPalette p = UiTheme.P;
            RowLabel(label, y, labelOffset);

            RectTransform hit = HudGraphics.CreateRect("SliderHit", _panel);
            UiKit.PlaceTopLeft(hit, new Vector2(CONTROL_X - 12f, y), new Vector2(TRACK_W + 24f, 36f));

            Image track = UiKit.Box("Track", _panel, p.Inset);
            UiKit.PlaceTopLeft(track.rectTransform, new Vector2(CONTROL_X, y + 13f), new Vector2(TRACK_W, 10f));
            track.gameObject.AddComponent<Outline>().effectColor = UiTheme.WithAlpha(p.PlateStroke, 0.8f);

            Image fill = HudGraphics.CreateRect("Fill", track.rectTransform).gameObject.AddComponent<Image>();
            fill.sprite = GradientSprite();
            fill.raycastTarget = false;
            UiKit.Place(fill.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(0f, 10f));

            Image knob = HudGraphics.CreateRect("Knob", track.rectTransform).gameObject.AddComponent<Image>();
            knob.sprite = HudGraphics.CircleSprite;
            knob.color = p.CrackInner;
            knob.raycastTarget = false;
            UiKit.Place(knob.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            knob.gameObject.AddComponent<Outline>().effectColor = new Color(0.69f, 0.48f, 0.18f);

            Text value = UiKit.Label("Value", _panel, 18, p.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(value.rectTransform, new Vector2(VALUE_X, y + 4f), new Vector2(90f, 28f));

            _sliders.Add(new SliderRow { Hit = hit, Fill = fill.rectTransform, Knob = knob.rectTransform, Value = value, Get = get, Set = set, Format = format });
            return y + ROW;
        }

        private float Toggle(string label, float y, Func<bool> get, Action<bool> set)
        {
            UiPalette p = UiTheme.P;
            RowLabel(label, y);
            Image track = UiKit.Sliced("Toggle", _panel, UiSprites.Pill);
            UiKit.PlaceTopLeft(track.rectTransform, new Vector2(CONTROL_X, y + 4f), new Vector2(56f, 28f));
            Image knob = HudGraphics.CreateRect("Knob", track.rectTransform).gameObject.AddComponent<Image>();
            knob.sprite = HudGraphics.CircleSprite;
            knob.raycastTarget = false;
            UiKit.Place(knob.rectTransform, new Vector2(0f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(22f, 22f));
            Text state = UiKit.Label("State", _panel, 16, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(state.rectTransform, new Vector2(CONTROL_X + 70f, y + 4f), new Vector2(80f, 28f));

            RectTransform hit = HudGraphics.CreateRect("ToggleHit", _panel);
            UiKit.PlaceTopLeft(hit, new Vector2(CONTROL_X - 6f, y), new Vector2(140f, 36f));
            _toggles.Add(new ToggleRow { Hit = hit, Track = track, Knob = knob.rectTransform, State = state, Get = get, Set = set });
            return y + ROW;
        }

        private float Choice(string label, float y, string[] options, Func<int> get, Action<int> set)
        {
            RowLabel(label, y);
            var row = new ChoiceRow { Get = get, Set = set };
            float x = CONTROL_X;

            foreach (string option in options)
            {
                float width = 28f + option.Length * 13f;
                Image box = UiKit.Box("Chip", _panel, UiTheme.P.Inset);
                UiKit.PlaceTopLeft(box.rectTransform, new Vector2(x, y + 3f), new Vector2(width, 32f));
                box.gameObject.AddComponent<Outline>().effectDistance = new Vector2(1.5f, -1.5f);
                Text text = UiKit.Label("Text", box.rectTransform, 16, UiTheme.P.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
                HudGraphics.Stretch(text.rectTransform);
                text.text = option;
                row.Chips.Add((box, text));
                x += width + 6f;
            }

            _choices.Add(row);
            return y + ROW;
        }

        private Image BottomButton(string label, Vector2 topLeft, float width, Transform parent = null)
        {
            UiPalette p = UiTheme.P;
            Image button = UiKit.Box(label, parent != null ? parent : _panel, p.PlateBottom);
            UiKit.PlaceTopLeft(button.rectTransform, topLeft, new Vector2(width, BUTTON_H));
            Outline outline = button.gameObject.AddComponent<Outline>();
            outline.effectColor = p.PlateStroke;
            outline.effectDistance = new Vector2(1.5f, -1.5f);
            Text text = UiKit.Label("Label", button.rectTransform, 19, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(text.rectTransform);
            text.text = label;
            return button;
        }

        // ------------------------------------------------------------------
        // 입력·갱신
        // ------------------------------------------------------------------

        private void Update()
        {
            Mouse mouse = Mouse.current;
            Keyboard keyboard = Keyboard.current;

            if (UpdateConfirm(keyboard))
            {
                return;
            }

            bool dirty = IsDirty;

            if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                RequestClose();
                return;
            }

            if (UiKit.IsCloseClicked(_close) || ButtonClicked(_doneButton))
            {
                RequestClose();
                return;
            }

            // [적용]: 바뀐 값이 있을 때만 (없으면 흐리게)
            if (ButtonClicked(_applyButton, dirty))
            {
                ShowConfirm(ConfirmMode.Apply);
                return;
            }

            // [기본값]: 창에만 기본값 표시 ([적용]해야 저장)
            if (ButtonClicked(_resetButton))
            {
                _draft = Values.Defaults();
                Refresh();
                return;
            }

            // 슬라이더: 누르면 잡고, 누르는 동안 따라감
            if (UiKit.Clicked)
            {
                _dragging = _sliders.Find(s => UiKit.IsHover(s.Hit));
            }

            if (_dragging != null)
            {
                if (mouse == null || !mouse.leftButton.isPressed)
                {
                    _dragging = null;
                }
                else
                {
                    _dragging.Set(PointerRatio(_dragging));
                    RefreshSlider(_dragging);
                }
            }

            // 마우스를 올린 슬라이더: ←→ 5%
            if (keyboard != null)
            {
                int dir = keyboard.rightArrowKey.wasPressedThisFrame ? 1 : keyboard.leftArrowKey.wasPressedThisFrame ? -1 : 0;
                SliderRow hovered = dir != 0 ? _sliders.Find(s => UiKit.IsHover(s.Hit)) : null;

                if (hovered != null)
                {
                    hovered.Set(Mathf.Clamp01(hovered.Get() + dir * KEY_STEP));
                    RefreshSlider(hovered);
                }
            }

            foreach (ToggleRow toggle in _toggles)
            {
                if (UiKit.Clicked && UiKit.IsHover(toggle.Hit))
                {
                    toggle.Set(!toggle.Get());
                    RefreshToggle(toggle);
                }
            }

            foreach (ChoiceRow choice in _choices)
            {
                for (int i = 0; i < choice.Chips.Count; i++)
                {
                    bool hover = UiKit.IsHover(choice.Chips[i].box.rectTransform);
                    choice.Chips[i].box.GetComponent<Outline>().effectColor = hover || i == choice.Get() ? UiTheme.P.Title : UiTheme.P.PlateStroke;

                    if (hover && UiKit.Clicked && i != choice.Get())
                    {
                        choice.Set(i);
                        RefreshChoice(choice);
                    }
                }
            }
        }

        // 아래 버튼 공통: 평소 어두운 판, 마우스를 올리면 금색 강조. 꺼진 버튼은 글자를 흐리게
        private static bool ButtonClicked(Image button, bool enabled = true)
        {
            UiPalette p = UiTheme.P;
            bool hover = enabled && UiKit.IsHover(button.rectTransform);
            button.color = hover ? p.Accent : p.PlateBottom;
            button.GetComponent<Outline>().effectColor = hover ? p.Title : p.PlateStroke;
            Text label = button.transform.Find("Label").GetComponent<Text>();
            label.color = !enabled ? p.TextDisabled : hover ? p.PlateBottom : p.Text;
            label.GetComponent<Shadow>().enabled = !hover;
            return hover && UiKit.Clicked;
        }

        private static float PointerRatio(SliderRow slider)
        {
            RectTransform track = (RectTransform)slider.Fill.parent;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(track, UiKit.Pointer, null, out Vector2 local);
            return Mathf.Clamp01((local.x - track.rect.xMin) / track.rect.width);
        }

        private void Refresh()
        {
            _sliders.ForEach(RefreshSlider);
            _toggles.ForEach(RefreshToggle);
            _choices.ForEach(RefreshChoice);
        }

        private static void RefreshSlider(SliderRow slider)
        {
            float v = Mathf.Clamp01(slider.Get());
            slider.Fill.sizeDelta = new Vector2(TRACK_W * v, 10f);
            slider.Knob.anchoredPosition = new Vector2(TRACK_W * v, 0f);
            slider.Value.text = slider.Format(v);
        }

        private static void RefreshToggle(ToggleRow toggle)
        {
            bool on = toggle.Get();
            UiPalette p = UiTheme.P;
            toggle.Track.color = on ? p.Accent : p.Inset;
            toggle.Knob.anchoredPosition = new Vector2(on ? 42f : 14f, 0f);
            toggle.Knob.GetComponent<Image>().color = on ? p.PlateBottom : p.TextDim;
            toggle.State.text = on ? "켜짐" : "꺼짐";
        }

        private static void RefreshChoice(ChoiceRow choice)
        {
            UiPalette p = UiTheme.P;
            int selected = choice.Get();

            for (int i = 0; i < choice.Chips.Count; i++)
            {
                bool on = i == selected;
                choice.Chips[i].box.color = on ? p.Accent : p.Inset;
                choice.Chips[i].text.color = on ? p.PlateBottom : p.Text;
                choice.Chips[i].text.GetComponent<Shadow>().enabled = !on;
            }
        }

        private static string Percent(float v) => $"{Mathf.RoundToInt(v * 100f)}%";

        private static Sprite _gradient;

        private static Sprite GradientSprite()
        {
            if (_gradient != null)
            {
                return _gradient;
            }

            const int w = 64;
            var texture = new Texture2D(w, 1, TextureFormat.RGBA32, false) { name = "SettingsSlider", wrapMode = TextureWrapMode.Clamp };

            for (int x = 0; x < w; x++)
            {
                texture.SetPixel(x, 0, Color.Lerp(new Color(0.69f, 0.48f, 0.18f), new Color(1f, 0.835f, 0.54f), x / (w - 1f)));
            }

            texture.Apply();
            _gradient = Sprite.Create(texture, new Rect(0f, 0f, w, 1f), new Vector2(0.5f, 0.5f));
            return _gradient;
        }
    }
}
