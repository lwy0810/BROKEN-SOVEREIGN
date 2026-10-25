using System.Collections.Generic;
using BrokenSovereign.Item;
using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 상점 창 (10/25): 대화창 [거래]로 열림. 화면 왼쪽 = 판매 목록(클릭 = 구매), 화면 오른쪽 = 인벤토리 창(가방 우클릭 = 판매, 30%).
    /// 마우스를 올리면 아이템 상세 창, 아래 보유 금화·안내 문구. Esc·× = 두 창 닫고 대화창으로. 휠 = 목록 넘기기.
    /// 판매 목록·가격은 ShopCatalog, 실제 구매·판매는 Inventory (SpendGold·AddGold·Add·Remove).
    /// </summary>
    public class ShopWindowView : MonoBehaviour
    {
        private static readonly Vector2 PANEL_SIZE = new Vector2(520f, 640f);
        private const float PANEL_X = -560f;   // 화면 가운데 기준 왼쪽 (인벤토리 창은 오른쪽)
        private const float LIST_WIDTH = 460f;
        private const float LIST_HEIGHT = 418f;
        private const float ROW_HEIGHT = 56f;
        private const int VISIBLE_ROWS = 7;
        private const float MESSAGE_SECONDS = 2.2f;

        private sealed class Row
        {
            public RectTransform Rect;
            public Image Background;
            public ItemSlotView Slot;
            public Text Name;
            public Text Price;
        }

        private NpcInteractor _interactor;
        private GameObject _player;
        private Inventory _inventory;
        private ItemTooltipView _tooltip;
        private RectTransform _panel;
        private Text _title;
        private Text _gold;
        private Text _message;
        private Image _close;
        private readonly List<Row> _stockRows = new List<Row>();
        private InventoryWindowView _inventoryWindow;
        private List<ShopOffer> _stock;
        private int _stockScroll;
        private float _messageUntil;

        public bool IsOpen => _panel != null && _panel.gameObject.activeSelf;

        /// <summary>거래 중 함께 여는 인벤토리 창 (HudView가 인벤토리 창을 만든 뒤 연결)</summary>
        public void SetInventoryWindow(InventoryWindowView window)
        {
            _inventoryWindow = window;
        }

        public void Initialize(NpcInteractor interactor, GameObject player, RectTransform hudRoot)
        {
            _interactor = interactor;
            _player = player;
            _inventory = player.GetComponent<Inventory>();
            RectTransform root = (RectTransform)transform;
            HudGraphics.Stretch(root);
            Build(root);
            _tooltip = new ItemTooltipView(hudRoot);

            _interactor.ShopOpened += OnOpened;
            _interactor.ShopClosed += OnClosed;
        }

        private void OnDestroy()
        {
            if (_interactor != null)
            {
                _interactor.ShopOpened -= OnOpened;
                _interactor.ShopClosed -= OnClosed;
            }
        }

        private void OnOpened(TownNpc npc, ShopType type)
        {
            if (_inventory == null)
            {
                _inventory = _player != null ? _player.GetComponent<Inventory>() : null;   // HudView가 가방을 나중에 붙임
            }

            _stock = ShopCatalog.Stock(type);
            _stockScroll = 0;
            _title.text = $"{npc.Title} {npc.DisplayName} · 거래";
            _messageUntil = 0f;
            _panel.gameObject.SetActive(true);
            _panel.SetAsLastSibling();
            Refresh();
            _inventoryWindow?.BeginTrade(Sell, ShopCatalog.SellPrice);
        }

        private void OnClosed()
        {
            _panel.gameObject.SetActive(false);
            _tooltip.Hide();
            _inventoryWindow?.EndTrade();
        }

        private void Update()
        {
            if (!IsOpen)
            {
                return;
            }

            Keyboard keyboard = Keyboard.current;

            if ((keyboard != null && keyboard.escapeKey.wasPressedThisFrame) || UiKit.IsCloseClicked(_close))
            {
                _interactor.CloseShop();
                return;
            }

            HandleScroll();
            HandleRows();
            _message.enabled = Time.unscaledTime < _messageUntil;
            _gold.text = $"보유 금화  {(_inventory != null ? _inventory.Gold : 0):N0}";
            _tooltip.Follow();
        }

        private void HandleScroll()
        {
            Mouse mouse = Mouse.current;
            float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;

            if (Mathf.Abs(scroll) < 0.01f)
            {
                return;
            }

            int step = scroll > 0f ? -1 : 1;

            if (_stockRows.Count > 0 && UiKit.IsHover((RectTransform)_stockRows[0].Rect.parent))
            {
                _stockScroll = Mathf.Clamp(_stockScroll + step, 0, Mathf.Max(0, _stock.Count - VISIBLE_ROWS));
                Refresh();
            }
        }

        private void HandleRows()
        {
            Mouse mouse = Mouse.current;
            bool right = mouse != null && mouse.rightButton.wasPressedThisFrame;
            ItemDefinition hovered = null;
            string footer = string.Empty;

            for (int i = 0; i < _stockRows.Count; i++)
            {
                Row row = _stockRows[i];
                int index = _stockScroll + i;
                bool valid = index < _stock.Count;
                bool hover = valid && UiKit.IsHover(row.Rect);
                row.Background.color = hover ? UiTheme.P.Hover : UiTheme.P.RowA;

                if (!hover)
                {
                    continue;
                }

                hovered = _stock[index].Item;
                footer = $"클릭: 구매 (금화 {_stock[index].Price:N0})";

                if (UiKit.Clicked)
                {
                    Buy(_stock[index]);
                }
            }

            if (hovered == null)
            {
                _tooltip.Hide();
            }
            else
            {
                _tooltip.Show(hovered, null, footer);
            }
        }

        private void Buy(ShopOffer offer)
        {
            if (_inventory == null || offer.Count == 0)
            {
                return;
            }

            if (_inventory.Gold < offer.Price)
            {
                Say("금화가 부족합니다");
                return;
            }

            if (_inventory.Add(offer.Item, 1, false) > 0)
            {
                Say("가방이 가득 찼습니다");
                return;
            }

            _inventory.SpendGold(offer.Price);

            if (offer.Count > 0)
            {
                offer.Count--;
            }

            Say($"{offer.Item.DisplayName} 구매  (-{offer.Price:N0})");
            Refresh();
        }

        // 인벤토리 창에서 가방 우클릭 (InventoryWindowView.BeginTrade)
        private void Sell(ItemKind kind, int index)
        {
            ItemStack stack = _inventory != null ? _inventory.Get(kind, index) : null;

            if (stack == null)
            {
                return;
            }

            int price = ShopCatalog.SellPrice(stack.Item);
            string name = stack.Item.DisplayName;
            _inventory.Remove(kind, index, 1);
            _inventory.AddGold(price, false);
            Say($"{name} 판매  (+{price:N0})");
            Refresh();
        }

        private void Say(string text)
        {
            _message.text = text;
            _messageUntil = Time.unscaledTime + MESSAGE_SECONDS;
        }

        private void Refresh()
        {
            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                int s = _stockScroll + i;
                ShopOffer offer = _stock != null && s < _stock.Count ? _stock[s] : null;
                SetRow(_stockRows[i], offer?.Item, offer == null ? 0 : 1,
                    offer == null ? string.Empty : offer.Count == 0 ? "품절" : $"금화 {offer.Price:N0}" + (offer.Count > 0 ? $"  (남은 {offer.Count})" : string.Empty),
                    offer != null && offer.Count == 0);
            }
        }

        private static void SetRow(Row row, ItemDefinition item, int count, string price, bool dim)
        {
            row.Rect.gameObject.SetActive(item != null);

            if (item == null)
            {
                return;
            }

            row.Slot.Set(item, count, dim);
            row.Name.text = item.DisplayName;
            row.Name.color = dim ? UiTheme.P.TextDisabled : ItemVisuals.RarityColor(item.Rarity);
            row.Price.text = price;
        }

        private void Build(RectTransform root)
        {
            UiPalette p = UiTheme.P;
            _panel = UiKit.Plate("ShopPanel", root).rectTransform;
            UiKit.Place(_panel, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(PANEL_X, 20f), PANEL_SIZE);
            _title = UiKit.TitleTab(_panel, "거래", 400f);
            _close = UiKit.CloseButton(_panel);

            BuildList("판매 목록 — 클릭: 구매  ·  팔기: 인벤토리에서 우클릭 (30%)", new Vector2(30f, 60f), _stockRows);

            _gold = UiKit.Label("Gold", _panel, 18, p.Title, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.Place(_gold.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 0f), new Vector2(34f, 50f), new Vector2(260f, 30f));
            Text hint = UiKit.Label("Hint", _panel, 13, p.TextDim, TextAnchor.MiddleRight);
            UiKit.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(1f, 0f), new Vector2(-34f, 50f), new Vector2(220f, 30f));
            hint.text = "휠: 넘기기  Esc: 돌아가기";
            _message = UiKit.Label("Message", _panel, 16, p.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiKit.Place(_message.rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 16f), new Vector2(PANEL_SIZE.x - 60f, 28f));
            _panel.gameObject.SetActive(false);
        }

        private void BuildList(string title, Vector2 position, List<Row> rows)
        {
            UiPalette p = UiTheme.P;
            Text header = UiKit.Label("Header", _panel, 15, p.TextDim, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiKit.PlaceTopLeft(header.rectTransform, position, new Vector2(LIST_WIDTH, 26f));
            header.text = title;

            Image box = UiKit.Box("List", _panel, UiTheme.WithAlpha(p.Inset, 0.6f));
            RectTransform list = box.rectTransform;
            UiKit.PlaceTopLeft(list, position + new Vector2(0f, 30f), new Vector2(LIST_WIDTH, LIST_HEIGHT));

            for (int i = 0; i < VISIBLE_ROWS; i++)
            {
                Image background = UiKit.Box($"Row{i}", list, p.RowA);
                RectTransform rect = background.rectTransform;
                UiKit.PlaceTopLeft(rect, new Vector2(6f, 6f + i * (ROW_HEIGHT + 2f)), new Vector2(LIST_WIDTH - 12f, ROW_HEIGHT));
                var slot = new ItemSlotView("Slot", rect);
                UiKit.Place(slot.Rect, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(48f, 48f));
                Text name = UiKit.Label("Name", rect, 16, p.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
                UiKit.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(0f, 0.5f), new Vector2(62f, 0f), new Vector2(220f, ROW_HEIGHT));
                Text price = UiKit.Label("Price", rect, 14, p.Title, TextAnchor.MiddleRight);
                UiKit.Place(price.rectTransform, new Vector2(1f, 0.5f), new Vector2(1f, 0.5f), new Vector2(-12f, 0f), new Vector2(170f, ROW_HEIGHT));
                rows.Add(new Row { Rect = rect, Background = background, Slot = slot, Name = name, Price = price });
            }
        }
    }
}
