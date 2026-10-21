using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Item;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 인벤토리 창 (I, 10/21 Figma 시안 v3).
    /// 왼쪽: 캐릭터 모델 + 장비 6칸 + 장비 효과 합계 + [상세 정보] 버튼 (창 왼쪽에 능력치 상세 정보창을 펼침)
    /// 오른쪽: 분류 탭(장비·소모품·재료, 분류마다 따로 가방)·정렬·가방 12×8 / 아래 골드·물약.
    /// 조작: 우클릭 = 장착·사용·해제, 끌어 놓기 = 칸 이동·장착·해제, 마우스 올림 = 툴팁(착용 장비 비교).
    /// 제목 탭 끌기로 창 이동, ×·I·Esc로 닫기. 열려 있는 동안 전투 입력 차단. HudView가 생성합니다.
    /// </summary>
    public class InventoryWindowView : MonoBehaviour
    {
        private const float WIDTH = 1124f;
        private const float HEIGHT = 720f;
        private const float TOP = 78f;
        private const float HEADER = 36f;
        private const float EQUIP = 48f;
        private const float CELL = 52f;
        private const float GAP = 5f;
        private const int COLUMNS = 12;
        private const float DETAIL_WIDTH = 380f;
        private const float DRAG_THRESHOLD = 6f;
        private const int SUMMARY_ROWS = 5;

        private static readonly string[] TABS = { "장비", "소모품", "재료" };

        private static readonly StatType[] SUMMARY_ORDER =
        {
            StatType.AttackPower, StatType.Armor, StatType.MaxHealth, StatType.AttackSpeed, StatType.CritChance,
            StatType.CritDamage, StatType.DamageBonus, StatType.DamageReduction, StatType.ElementalResistance,
            StatType.DodgeChance, StatType.MoveSpeed, StatType.CooldownReduction, StatType.ResourceCostReduction,
            StatType.LifeRegen
        };

        private RectTransform _panel;
        private Image _close;
        private WindowDrag _drag;
        private CharacterPreview _preview;
        private readonly ItemSlotView[] _bag = new ItemSlotView[Inventory.SIZE];
        private readonly Dictionary<EquipSlot, ItemSlotView> _equip = new Dictionary<EquipSlot, ItemSlotView>();
        private readonly List<Text[]> _summary = new List<Text[]>();
        private Text _summaryEmpty;
        private readonly Image[] _tabs = new Image[TABS.Length];
        private readonly Text[] _tabTexts = new Text[TABS.Length];
        private Image _sort;
        private Text _capacity;
        private Text _gold;
        private Text _potion;
        private Image _detailButton;
        private Text _detailButtonText;
        private RectTransform _detail;
        private StatListView _detailStats;
        private ItemTooltipView _tooltip;
        private Image _ghost;

        private GameObject _player;
        private Inventory _inventory;
        private PlayerEquipment _equipment;
        private PotionUser _potionUser;
        private int _tab;
        private bool _dirty = true;

        // 끌기 상태: 가방 칸 번호 또는 장비 부위
        private int _pressBag = -1;
        private EquipSlot? _pressEquip;
        private Vector2 _pressPosition;
        private bool _dragging;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        // 탭 = 분류별 가방 (장비·소모품·재료)
        private ItemKind Kind => (ItemKind)_tab;

        public void Initialize(GameObject player, RectTransform hudRoot)
        {
            _player = player;
            _inventory = player.GetComponent<Inventory>();
            _equipment = player.GetComponent<PlayerEquipment>();
            _potionUser = player.GetComponent<PotionUser>();
            UiPalette p = UiTheme.P;

            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);

            _panel = UiKit.Plate("Panel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(DETAIL_WIDTH * 0.25f, 20f),
                new Vector2(WIDTH, HEIGHT));
            _drag = new WindowDrag(_panel, (RectTransform)UiKit.TitleTab(_panel, "인벤토리", 240f).transform.parent);
            _close = UiKit.CloseButton(_panel);

            BuildEquipment(28f, 344f);
            Divider(388f);
            BuildBag(410f);
            BuildDetail();

            _tooltip = new ItemTooltipView(hudRoot);
            _ghost = HudGraphics.CreateRect("DragGhost", hudRoot).gameObject.AddComponent<Image>();
            _ghost.raycastTarget = false;
            _ghost.rectTransform.sizeDelta = new Vector2(44f, 44f);
            _ghost.gameObject.SetActive(false);

            if (_inventory != null)
            {
                _inventory.Changed += MarkDirty;
            }

            if (_equipment != null)
            {
                _equipment.Changed += MarkDirty;
            }

            _panel.gameObject.SetActive(false);
        }

        private void MarkDirty()
        {
            _dirty = true;
        }

        // 왼쪽: 캐릭터 모델 + 장비 6칸 (왼쪽 투구·갑옷·장갑 / 오른쪽 무기·신발·장신구) + 효과 합계 + 상세 정보 버튼
        private void BuildEquipment(float x, float width)
        {
            UiPalette p = UiTheme.P;
            Header(_panel, "장착 장비", x, TOP, width);

            Vector2 modelSize = new Vector2(216f, 372f);
            float modelX = x + (width - modelSize.x) * 0.5f;
            float modelY = 122f;
            Image frame = UiKit.Sliced("ModelFrame", _panel, UiSprites.Tile);
            UiKit.PlaceTopLeft(frame.rectTransform, new Vector2(modelX, modelY), modelSize);
            _preview = new CharacterPreview(frame.transform, modelSize - new Vector2(8f, 8f));
            HudGraphics.Stretch(_preview.Rect);
            _preview.Rect.offsetMin = new Vector2(4f, 4f);
            _preview.Rect.offsetMax = new Vector2(-4f, -4f);
            Text hint = UiKit.Label("RotateHint", frame.transform, 11, p.TextDim, TextAnchor.MiddleCenter);
            UiKit.Place(hint.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 6f), new Vector2(modelSize.x, 16f));
            hint.text = "끌어서 회전";

            EquipSlot[] left = { EquipSlot.Helmet, EquipSlot.Armor, EquipSlot.Gloves };
            EquipSlot[] right = { EquipSlot.Weapon, EquipSlot.Boots, EquipSlot.Accessory };
            float step = (modelSize.y - EQUIP - 40f) * 0.5f;

            for (int i = 0; i < 3; i++)
            {
                float y = modelY + 16f + i * step;
                AddEquipSlot(left[i], new Vector2(modelX - 12f - EQUIP, y));
                AddEquipSlot(right[i], new Vector2(modelX + modelSize.x + 12f, y));
            }

            Header(_panel, "장비 효과 합계", x, 504f, width);

            for (int i = 0; i < SUMMARY_ROWS; i++)
            {
                Text name = UiKit.Label("SumName", _panel, 14, p.TextDim, TextAnchor.MiddleLeft);
                UiKit.PlaceTopLeft(name.rectTransform, new Vector2(x + 8f, 540f + i * 23f), new Vector2(width - 140f, 23f));
                Text value = UiKit.Label("SumValue", _panel, 15, p.Text, TextAnchor.MiddleRight, FontStyle.Bold);
                UiKit.PlaceTopLeft(value.rectTransform, new Vector2(x + width - 128f, 540f + i * 23f), new Vector2(120f, 23f));
                _summary.Add(new[] { name, value });
            }

            _summaryEmpty = UiKit.Label("SumEmpty", _panel, 14, UiTheme.WithAlpha(p.TextDim, 0.7f), TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_summaryEmpty.rectTransform, new Vector2(x + 8f, 540f), new Vector2(width, 23f));
            _summaryEmpty.text = "장착한 장비 없음";

            _detailButton = UiKit.Sliced("DetailButton", _panel, UiSprites.Button);
            UiKit.PlaceTopLeft(_detailButton.rectTransform, new Vector2(x, HEIGHT - 24f - 38f), new Vector2(width, 38f));
            _detailButtonText = UiKit.Label("Text", _detailButton.transform, 15, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(_detailButtonText.rectTransform);
        }

        private void AddEquipSlot(EquipSlot slot, Vector2 position)
        {
            var view = new ItemSlotView("Equip" + slot, _panel, ItemVisuals.SlotName(slot));
            UiKit.PlaceTopLeft(view.Rect, position, new Vector2(EQUIP, EQUIP));
            Text label = UiKit.Label("EquipName", _panel, 11, UiTheme.P.TextDim, TextAnchor.MiddleCenter);
            UiKit.PlaceTopLeft(label.rectTransform, position + new Vector2(-8f, EQUIP + 2f), new Vector2(EQUIP + 16f, 16f));
            label.text = ItemVisuals.SlotName(slot);
            _equip[slot] = view;
        }

        // 오른쪽: 탭·정렬·가방 격자·골드·물약
        private void BuildBag(float x)
        {
            UiPalette p = UiTheme.P;
            float gridWidth = COLUMNS * CELL + (COLUMNS - 1) * GAP;

            for (int i = 0; i < TABS.Length; i++)
            {
                _tabs[i] = UiKit.Sliced("Tab", _panel, UiSprites.Button);
                UiKit.PlaceTopLeft(_tabs[i].rectTransform, new Vector2(x + i * 92f, TOP), new Vector2(84f, 32f));
                _tabTexts[i] = UiKit.Label("Text", _tabs[i].transform, 14, p.TextDim, TextAnchor.MiddleCenter, FontStyle.Bold);
                HudGraphics.Stretch(_tabTexts[i].rectTransform);
                _tabTexts[i].text = TABS[i];
            }

            _sort = UiKit.Sliced("Sort", _panel, UiSprites.Button);
            UiKit.PlaceTopLeft(_sort.rectTransform, new Vector2(x + gridWidth - 88f, TOP), new Vector2(88f, 32f));
            Text sortText = UiKit.Label("Text", _sort.transform, 14, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            HudGraphics.Stretch(sortText.rectTransform);
            sortText.text = "정렬";

            _capacity = UiKit.Label("Capacity", _panel, 13, p.TextDim, TextAnchor.MiddleRight);
            UiKit.PlaceTopLeft(_capacity.rectTransform, new Vector2(x + gridWidth - 88f - 14f - 140f, TOP), new Vector2(140f, 32f));

            float gridTop = 124f;

            for (int i = 0; i < Inventory.SIZE; i++)
            {
                int column = i % COLUMNS, row = i / COLUMNS;
                _bag[i] = new ItemSlotView("Bag" + i, _panel);
                UiKit.PlaceTopLeft(_bag[i].Rect, new Vector2(x + column * (CELL + GAP), gridTop + row * (CELL + GAP)), new Vector2(CELL, CELL));
            }

            float bottom = gridTop + (Inventory.SIZE / COLUMNS) * (CELL + GAP) + 3f;
            Image line = UiKit.Box("Line", _panel, UiTheme.WithAlpha(p.PlateStroke, 0.45f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, bottom), new Vector2(gridWidth, 1f));

            Image coin = UiKit.Sliced("Coin", _panel, UiSprites.Pill);
            coin.color = p.Title;
            UiKit.PlaceTopLeft(coin.rectTransform, new Vector2(x, bottom + 22f), new Vector2(18f, 18f));
            _gold = UiKit.Label("Gold", _panel, 18, p.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(_gold.rectTransform, new Vector2(x + 26f, bottom + 17f), new Vector2(120f, 28f));

            Image potion = HudGraphics.CreateRect("PotionIcon", _panel).gameObject.AddComponent<Image>();
            potion.raycastTarget = false;
            potion.sprite = ItemVisuals.Icon(ItemIcon.Potion);
            potion.color = new Color(0.85f, 0.25f, 0.2f);
            UiKit.PlaceTopLeft(potion.rectTransform, new Vector2(x + 150f, bottom + 18f), new Vector2(24f, 24f));
            _potion = UiKit.Label("Potion", _panel, 15, p.Text, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(_potion.rectTransform, new Vector2(x + 180f, bottom + 17f), new Vector2(140f, 28f));
        }

        // 상세 정보창: 창 왼쪽에 붙는 능력치 목록 (장비 보너스 초록 표시)
        private void BuildDetail()
        {
            UiPalette p = UiTheme.P;
            _detail = UiKit.Plate("Detail", _panel).rectTransform;
            UiKit.PlaceTopLeft(_detail, new Vector2(-DETAIL_WIDTH - 10f, 0f), new Vector2(DETAIL_WIDTH, HEIGHT));
            Text title = UiKit.Title("Title", _detail, 20, p.Title, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(title.rectTransform, new Vector2(24f, 18f), new Vector2(240f, 30f));
            title.text = "상세 정보";
            Text sub = UiKit.Label("Sub", _detail, 12, p.TextDim, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(sub.rectTransform, new Vector2(24f, 50f), new Vector2(240f, 18f));
            sub.text = "장비 포함 최종 능력치  ·  (괄호) = 장비 증감";

            _detailStats = new StatListView(_detail, new Vector2(24f, 84f), new Vector2(DETAIL_WIDTH - 24f - 30f, HEIGHT - 84f - 24f),
                _player, stat => _equipment != null ? _equipment.GetBonus(stat) : 0f);
            _detail.gameObject.SetActive(false);
        }

        private void Header(RectTransform parent, string title, float x, float y, float width)
        {
            Text text = UiKit.Title(title, parent, 17, UiTheme.P.Accent, TextAnchor.MiddleLeft);
            UiKit.PlaceTopLeft(text.rectTransform, new Vector2(x, y), new Vector2(width, 28f));
            text.text = title;
            Image line = UiKit.Box("Line", parent, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.45f));
            UiKit.PlaceTopLeft(line.rectTransform, new Vector2(x, y + 29f), new Vector2(width, 1f));
        }

        private void Divider(float x)
        {
            Image divider = UiKit.Box("Divider", _panel, UiTheme.WithAlpha(UiTheme.P.PlateStroke, 0.35f));
            UiKit.PlaceTopLeft(divider.rectTransform, new Vector2(x, TOP), new Vector2(1f, HEIGHT - TOP - 36f));
        }

        private void OnDestroy()
        {
            if (IsOpen)
            {
                GameplayInput.IsBlocked = false;
            }

            if (_inventory != null)
            {
                _inventory.Changed -= MarkDirty;
            }

            if (_equipment != null)
            {
                _equipment.Changed -= MarkDirty;
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
                _dirty = true;
                _detailStats.ResetScroll();
                _preview.Open(_player);
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            CancelDrag();
            _tooltip.Hide();
            _panel.gameObject.SetActive(false);
            GameplayInput.IsBlocked = false;
            _preview.Close();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;

            if (keyboard != null && keyboard.iKey.wasPressedThisFrame)
            {
                Toggle();
                return;
            }

            if (!IsOpen)
            {
                return;
            }

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || UiKit.IsCloseClicked(_close))
            {
                Close();
                return;
            }

            _drag.Tick();
            _preview.Tick();
            HandleButtons();
            HandleSlots();

            if (_dirty)
            {
                _dirty = false;
                RefreshSlots();
            }

            RefreshTexts();

            if (_detail.gameObject.activeSelf)
            {
                _detailStats.Tick();
            }
        }

        private void HandleButtons()
        {
            for (int i = 0; i < _tabs.Length; i++)
            {
                bool hover = UiKit.IsHover(_tabs[i].rectTransform);

                if (hover && UiKit.Clicked && _tab != i)
                {
                    _tab = i;
                    _dirty = true;
                    CancelDrag();
                }

                bool on = _tab == i;
                _tabs[i].sprite = on ? UiSprites.ButtonEquip : UiSprites.Button;
                _tabs[i].color = hover && !on ? new Color(1f, 0.92f, 0.8f) : Color.white;
                _tabTexts[i].color = on ? UiTheme.P.Title : UiTheme.P.TextDim;
            }

            bool sortHover = UiKit.IsHover(_sort.rectTransform);
            _sort.color = sortHover ? new Color(1f, 0.92f, 0.8f) : Color.white;

            if (sortHover && UiKit.Clicked && _inventory != null)
            {
                _inventory.Sort(Kind);
            }

            bool detailOn = _detail.gameObject.activeSelf;
            bool detailHover = UiKit.IsHover(_detailButton.rectTransform);

            if (detailHover && UiKit.Clicked)
            {
                detailOn = !detailOn;
                _detail.gameObject.SetActive(detailOn);
                _detailStats.ResetScroll();
            }

            _detailButton.sprite = detailOn ? UiSprites.ButtonEquip : UiSprites.Button;
            _detailButton.color = detailHover && !detailOn ? new Color(1f, 0.92f, 0.8f) : Color.white;
            _detailButtonText.text = detailOn ? "◀  상세 정보" : "상세 정보";
            _detailButtonText.color = detailOn ? UiTheme.P.Title : UiTheme.P.Text;
        }

        // 마우스 올림·툴팁·우클릭·끌어 놓기
        private void HandleSlots()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null || _inventory == null)
            {
                return;
            }

            int hoverBag = -1;
            EquipSlot? hoverEquip = null;

            for (int i = 0; i < _bag.Length; i++)
            {
                if (UiKit.IsHover(_bag[i].Rect))
                {
                    hoverBag = i;
                    break;
                }
            }

            if (hoverBag < 0)
            {
                foreach (KeyValuePair<EquipSlot, ItemSlotView> pair in _equip)
                {
                    if (UiKit.IsHover(pair.Value.Rect))
                    {
                        hoverEquip = pair.Key;
                        break;
                    }
                }
            }

            for (int i = 0; i < _bag.Length; i++)
            {
                _bag[i].SetHighlight(i == hoverBag);
            }

            foreach (KeyValuePair<EquipSlot, ItemSlotView> pair in _equip)
            {
                bool target = hoverEquip == pair.Key || (_dragging && DraggedItem() is ItemDefinition dragged &&
                    dragged.IsEquipment && dragged.Slot == pair.Key && _pressBag >= 0);
                pair.Value.SetHighlight(target);
            }

            // 우클릭: 장착·사용·해제
            if (!_dragging && mouse.rightButton.wasPressedThisFrame)
            {
                if (hoverBag >= 0)
                {
                    UseBagItem(hoverBag);
                }
                else if (hoverEquip.HasValue && _equipment != null)
                {
                    _equipment.Unequip(hoverEquip.Value);
                }
            }

            // 끌기 시작
            if (mouse.leftButton.wasPressedThisFrame)
            {
                if (hoverBag >= 0 && _inventory.Get(Kind, hoverBag) != null)
                {
                    _pressBag = hoverBag;
                    _pressPosition = UiKit.Pointer;
                }
                else if (hoverEquip.HasValue && _equipment != null && _equipment.Get(hoverEquip.Value) != null)
                {
                    _pressEquip = hoverEquip;
                    _pressPosition = UiKit.Pointer;
                }
            }

            bool pressing = _pressBag >= 0 || _pressEquip.HasValue;

            if (pressing && mouse.leftButton.isPressed && !_dragging &&
                Vector2.Distance(_pressPosition, UiKit.Pointer) > DRAG_THRESHOLD)
            {
                BeginDrag();
            }

            if (_dragging)
            {
                MoveGhost();
            }

            if (pressing && !mouse.leftButton.isPressed)
            {
                if (_dragging)
                {
                    Drop(hoverBag, hoverEquip);
                }

                CancelDrag();
            }

            // 툴팁
            ItemDefinition hovered = hoverBag >= 0 ? _inventory.Get(Kind, hoverBag)?.Item
                : hoverEquip.HasValue && _equipment != null ? _equipment.Get(hoverEquip.Value) : null;

            if (_dragging || hovered == null)
            {
                _tooltip.Hide();
            }
            else
            {
                ItemDefinition compare = hovered.IsEquipment && hoverBag >= 0 && _equipment != null ? _equipment.Get(hovered.Slot) : null;
                string footer = hoverEquip.HasValue ? "우클릭: 해제"
                    : hovered.IsEquipment ? "우클릭: 장착"
                    : hovered.Kind == ItemKind.Consumable ? "우클릭: 사용" : string.Empty;
                _tooltip.Show(hovered, compare, footer);
            }
        }

        private void UseBagItem(int index)
        {
            ItemStack stack = _inventory.Get(Kind, index);

            if (stack == null)
            {
                return;
            }

            if (stack.Item.IsEquipment)
            {
                _equipment?.EquipFromInventory(index);
            }
            else if (stack.Item.Id == ItemCatalog.POTION_ID && _potionUser != null && _potionUser.AddCharges(1) > 0)
            {
                _inventory.Remove(Kind, index);
            }
        }

        private ItemDefinition DraggedItem()
        {
            if (_pressBag >= 0)
            {
                return _inventory.Get(Kind, _pressBag)?.Item;
            }

            return _pressEquip.HasValue && _equipment != null ? _equipment.Get(_pressEquip.Value) : null;
        }

        private void BeginDrag()
        {
            ItemDefinition item = DraggedItem();

            if (item == null)
            {
                CancelDrag();
                return;
            }

            _dragging = true;
            _ghost.gameObject.SetActive(true);
            _ghost.transform.SetAsLastSibling();
            _ghost.sprite = ItemVisuals.Icon(item.Icon);
            _ghost.color = UiTheme.WithAlpha(item.IconColor, 0.85f);
            MoveGhost();
        }

        private void MoveGhost()
        {
            RectTransform root = (RectTransform)_ghost.transform.parent;

            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, UiKit.Pointer, null, out Vector2 local))
            {
                _ghost.rectTransform.anchorMin = _ghost.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                _ghost.rectTransform.anchoredPosition = local;
            }
        }

        private void Drop(int targetBag, EquipSlot? targetEquip)
        {
            if (_pressBag >= 0)
            {
                if (targetBag >= 0)
                {
                    _inventory.Move(Kind, _pressBag, targetBag);
                }
                else if (targetEquip.HasValue && _equipment != null)
                {
                    ItemDefinition item = _inventory.Get(Kind, _pressBag)?.Item;

                    if (item != null && item.IsEquipment && item.Slot == targetEquip.Value)
                    {
                        _equipment.EquipFromInventory(_pressBag);
                    }
                }
            }
            else if (_pressEquip.HasValue && targetBag >= 0 && _equipment != null)
            {
                // 장비 가방이 보일 때만 놓은 칸에, 아니면 장비 가방 빈 칸에
                _equipment.Unequip(_pressEquip.Value, Kind == ItemKind.Equipment ? targetBag : -1);
            }
        }

        private void CancelDrag()
        {
            _dragging = false;
            _pressBag = -1;
            _pressEquip = null;

            if (_ghost != null)
            {
                _ghost.gameObject.SetActive(false);
            }
        }

        private void RefreshSlots()
        {
            for (int i = 0; i < _bag.Length; i++)
            {
                ItemStack stack = _inventory != null ? _inventory.Get(Kind, i) : null;
                _bag[i].Set(stack?.Item, stack?.Count ?? 0);
            }

            foreach (KeyValuePair<EquipSlot, ItemSlotView> pair in _equip)
            {
                pair.Value.Set(_equipment != null ? _equipment.Get(pair.Key) : null, 1);
            }

            RefreshSummary();
        }

        private void RefreshSummary()
        {
            int row = 0;

            foreach (StatType stat in SUMMARY_ORDER)
            {
                float bonus = _equipment != null ? _equipment.GetBonus(stat) : 0f;

                if (Mathf.Abs(bonus) < 0.0001f || row >= SUMMARY_ROWS)
                {
                    continue;
                }

                _summary[row][0].text = StatText.Name(stat);
                _summary[row][1].text = StatText.Signed(stat, bonus);
                row++;
            }

            for (int i = 0; i < _summary.Count; i++)
            {
                _summary[i][0].gameObject.SetActive(i < row);
                _summary[i][1].gameObject.SetActive(i < row);
            }

            _summaryEmpty.gameObject.SetActive(row == 0);
        }

        private void RefreshTexts()
        {
            if (_inventory != null)
            {
                _capacity.text = $"가방 {_inventory.UsedCount(Kind)} / {Inventory.SIZE}";
                _gold.text = $"{_inventory.Gold:N0}";
            }

            _potion.text = _potionUser != null ? $"물약 {_potionUser.Charges} / {_potionUser.MaxCharges}" : "물약 -";
            _tooltip.Follow();
        }
    }
}
