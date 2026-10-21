using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using BrokenSovereign.Item;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 캐릭터 창 (C, GDD 10장): 왼쪽 직업·격·레벨·경험치·다음 격 돌파·영웅의 길 / 가운데 캐릭터 모델(장착 모습) + 장착 장비 6칸 /
    /// 오른쪽 능력치 5분류(StatListView, 휠 스크롤). 프로토타입은 스탯 분배 대신 레벨업 자동 분배라 읽기 전용.
    /// 장비 칸은 PlayerEquipment 표시 (장착·해제는 인벤토리 창). 열려 있는 동안 전투 입력 차단. 닫기: C·Esc·오른쪽 위 ×.
    /// HudView가 생성합니다.
    /// </summary>
    public class CharacterWindowView : MonoBehaviour
    {
        private const float WIDTH = 1100f;
        private const float HEIGHT = 720f;
        private const float HEADER = 36f;
        private const float TOP = 86f;
        private const float SLOT = 44f;

        private RectTransform _panel;
        private Image _close;
        private WindowDrag _drag;
        private Text _jobText;
        private Text _tierText;
        private UiBar _expBar;
        private Text _expText;
        private Text _hintText;
        private Text _heroText;
        private CharacterPreview _preview;
        private StatListView _stats;
        private readonly ItemSlotView[] _slots = new ItemSlotView[PlayerEquipment.SLOTS.Length];
        private ItemTooltipView _tooltip;
        private GameObject _player;
        private PlayerProgression _progression;
        private PlayerJob _job;
        private HeroPathController _hero;
        private PlayerEquipment _equipment;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        public void Initialize(GameObject player, RectTransform hudRoot)
        {
            _player = player;
            _progression = player.GetComponent<PlayerProgression>();
            _job = player.GetComponent<PlayerJob>();
            _hero = player.GetComponent<HeroPathController>();
            _equipment = player.GetComponent<PlayerEquipment>();
            UiPalette p = UiTheme.P;

            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0f, 20f),
                new Vector2(WIDTH, HEIGHT));
            _drag = new WindowDrag(_panel, (RectTransform)UiKit.TitleTab(_panel, "캐릭터").transform.parent);
            _close = UiKit.CloseButton(_panel);

            // 왼쪽: 성장
            float left = 36f, colWidth = 320f;
            _jobText = UiKit.Title("Job", _panel, 34, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_jobText.rectTransform, new Vector2(left, TOP), new Vector2(colWidth, 46f));
            _tierText = UiKit.Title("Tier", _panel, 19, p.Text, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_tierText.rectTransform, new Vector2(left, TOP + 50f), new Vector2(colWidth, 28f));

            _expBar = UiKit.Bar("Exp", _panel, p.Xp, p.Xp * 0.5f);
            UiKit.PlaceTopLeft(_expBar.Rect, new Vector2(left, TOP + 90f), new Vector2(colWidth, 10f));
            _expText = UiKit.Label("ExpText", _panel, 13, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_expText.rectTransform, new Vector2(left, TOP + 106f), new Vector2(colWidth, 20f));

            Header("다음 격 돌파", left, TOP + 150f, colWidth);
            _hintText = UiKit.Label("Hint", _panel, 15, p.Text, TextAnchor.UpperLeft);
            _hintText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.PlaceTopLeft(_hintText.rectTransform, new Vector2(left, TOP + 150f + HEADER), new Vector2(colWidth, 46f));

            Header("영웅의 길", left, TOP + 250f, colWidth);
            _heroText = UiKit.Label("Hero", _panel, 15, p.Text, TextAnchor.UpperLeft);
            _heroText.horizontalOverflow = HorizontalWrapMode.Wrap;
            UiKit.PlaceTopLeft(_heroText.rectTransform, new Vector2(left, TOP + 250f + HEADER), new Vector2(colWidth, 70f));

            Text note = UiKit.Label("Note", _panel, 13, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.Place(note.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(left, 22f), new Vector2(colWidth, 20f));
            note.text = "능력치는 레벨업 시 자동 분배 (프로토타입)";

            Divider(386f);
            Divider(738f);

            BuildPreview(410f, 304f);
            _stats = new StatListView(_panel, new Vector2(762f, TOP), new Vector2(290f, HEIGHT - 40f - TOP), player);
            _tooltip = new ItemTooltipView(hudRoot);

            _panel.gameObject.SetActive(false);
        }

        // 가운데: 캐릭터 모델 (장비 착용 모습 확인용) + 장착 장비 칸
        private void BuildPreview(float x, float width)
        {
            UiPalette p = UiTheme.P;
            Header("장착 모습", x, TOP, width);

            float top = TOP + HEADER;
            float slotY = HEIGHT - 40f - SLOT - 18f;
            Vector2 size = new Vector2(width, slotY - 14f - top);
            Image frame = UiKit.Sliced("PreviewFrame", _panel, UiSprites.Tile);
            UiKit.PlaceTopLeft(frame.rectTransform, new Vector2(x, top), size);

            _preview = new CharacterPreview(frame.transform, size - new Vector2(8f, 8f));
            HudGraphics.Stretch(_preview.Rect);
            _preview.Rect.offsetMin = new Vector2(4f, 4f);
            _preview.Rect.offsetMax = new Vector2(-4f, -4f);

            Text hint = UiKit.Label("RotateHint", frame.transform, 12, p.TextDim, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(width, 18f));
            hint.text = "끌어서 회전";

            EquipSlot[] slots = PlayerEquipment.SLOTS;
            float gap = (width - SLOT * slots.Length) / (slots.Length - 1);

            for (int i = 0; i < slots.Length; i++)
            {
                float sx = x + i * (SLOT + gap);
                _slots[i] = new ItemSlotView("Slot", _panel);
                UiKit.PlaceTopLeft(_slots[i].Rect, new Vector2(sx, slotY), new Vector2(SLOT, SLOT));
                Text name = UiKit.Label("SlotName", _panel, 11, p.TextDim, TextAnchor.MiddleCenter);
                UiKit.PlaceTopLeft(name.rectTransform, new Vector2(sx - 6f, slotY + SLOT + 2f), new Vector2(SLOT + 12f, 16f));
                name.text = ItemVisuals.SlotName(slots[i]);
            }
        }

        private void Header(string title, float x, float y, float width)
        {
            Text text = UiKit.Title(title, _panel, 17, UiTheme.P.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(text.rectTransform, new Vector2(x, y), new Vector2(width, 28f));
            text.text = title;
            Image line = UiKit.Box("Line", _panel, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.45f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, y + 29f), new Vector2(width, 1f));
        }

        private void Divider(float x)
        {
            Image divider = UiKit.Box("Divider", _panel, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.35f));
            UiKit.PlaceTopLeft(divider.rectTransform, new Vector2(x, TOP), new Vector2(1f, HEIGHT - TOP - 40f));
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }

            _preview?.Release();
        }

        public void Toggle()
        {
            if (IsOpen)
            {
                Close();
            }
            else if (!GameplayInput.IsBlocked)
            {
                _panel.gameObject.SetActive(true);
                GameplayInput.IsBlocked = true;
                _stats.ResetScroll();
                _preview.Open(_player);
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            _tooltip.Hide();
            _panel.gameObject.SetActive(false);
            GameplayInput.IsBlocked = false;
            _preview.Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.cKey.wasPressedThisFrame)
            {
                Toggle();
            }
            else if (IsOpen && keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
            {
                Close();
            }
            else if (IsOpen && UiKit.IsCloseClicked(_close))
            {
                Close();
            }

            if (IsOpen)
            {
                _drag.Tick();
                _preview.Tick();
                _stats.Tick();
                Refresh();
                RefreshEquipment();
            }
        }

        private void RefreshEquipment()
        {
            ItemDefinition hovered = null;

            for (int i = 0; i < _slots.Length; i++)
            {
                ItemDefinition item = _equipment != null ? _equipment.Get(PlayerEquipment.SLOTS[i]) : null;
                bool hover = UiKit.IsHover(_slots[i].Rect);
                _slots[i].SetHighlight(hover && item != null);
                _slots[i].Set(item, 1);

                if (hover)
                {
                    hovered = item;
                }
            }

            if (hovered != null)
            {
                _tooltip.Show(hovered, null, "장착·해제: 인벤토리(I)");
            }
            else
            {
                _tooltip.Hide();
            }
        }

        private void Refresh()
        {
            UiPalette p = UiTheme.P;
            _jobText.text = _job != null && _job.Current != null ? _job.Current.DisplayName : "무직";

            if (_progression != null)
            {
                _tierText.text = $"격  {AscensionModel.DisplayName(_progression.Tier)}      레벨  {_progression.Level}";
                _expBar.SetRatio(_progression.IsMaxLevel ? 1f : _progression.Progress);
                _expText.text = _progression.IsMaxLevel
                    ? "경험치  최대 레벨"
                    : $"경험치  {_progression.Experience} / {_progression.ExperienceToNext}  ({_progression.Progress * 100f:0}%)";
                _hintText.text = _progression.NextAscensionHint;
            }

            if (_hero != null && _hero.HasPath)
            {
                HeroPathDefinition path = _hero.Current;
                _heroText.color = p.Text;
                _heroText.text = $"{path.DisplayName}  ·  {path.Title}\n고유 메커니즘: {path.MechanicName}";
            }
            else
            {
                _heroText.color = p.TextDisabled;
                _heroText.text = "반신 돌파 후 선택";
            }
        }
    }
}
