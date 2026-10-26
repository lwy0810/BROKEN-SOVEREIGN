using System.Collections.Generic;
using BrokenSovereign.Item;
using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>상점 종류 (NPC 이름으로 정함, NpcShop 컴포넌트가 있으면 그 값)</summary>
    public enum ShopType
    {
        None,
        Goods,   // 잡화 상인: 물약·영약·재료
        Forge,   // 대장장이: 무기·방어구 (랜덤 진열)
        Clinic   // 의원: 물약·영약
    }

    /// <summary>판매 목록 한 줄. Count &lt; 0 = 무한</summary>
    public sealed class ShopOffer
    {
        public ItemDefinition Item;
        public int Price;
        public int Count;
    }

    /// <summary>
    /// 마을 상점 판매 목록·가격 (10/25). 가격 = 희귀도 기준 (소모품·재료는 낮게), 팔 때 = 30%.
    /// 대장장이 진열은 처음 열 때 무작위로 만들고 Restock()까지 유지 (마을 귀환 시 새로 고치는 연결은 추후).
    /// 수치 조정은 이 파일 표에서만.
    /// </summary>
    public static class ShopCatalog
    {
        public const float SELL_RATE = 0.3f;
        private const int FORGE_STOCK = 6;

        private static readonly int[] EQUIP_PRICE = { 60, 150, 400, 1000, 2500 };   // 일반 ~ 전설
        private static readonly Dictionary<ShopType, List<ShopOffer>> _stock = new Dictionary<ShopType, List<ShopOffer>>();

        public static ShopType TypeOf(TownNpc npc)
        {
            if (npc == null)
            {
                return ShopType.None;
            }

            if (npc.TryGetComponent(out NpcShop shop))
            {
                return shop.Type;
            }

            string name = npc.gameObject.name;
            return name.Contains("Merchant") ? ShopType.Goods
                : name.Contains("Blacksmith") ? ShopType.Forge
                : name.Contains("Healer") ? ShopType.Clinic
                : ShopType.None;
        }

        /// <summary>대화 창 보조 설명</summary>
        public static string Describe(ShopType type, INpcService service)
        {
            string trade = type switch
            {
                ShopType.Goods => "물약과 잡화를 사고팔 수 있습니다.",
                ShopType.Forge => "무기와 방어구를 사고팔 수 있습니다.",
                ShopType.Clinic => "물약을 살 수 있습니다.",
                _ => string.Empty
            };

            return service != null ? $"{service.ActionLabel}: 체력을 모두 회복합니다 (무료).  {trade}" : trade;
        }

        public static int Price(ItemDefinition item)
        {
            int rarity = Mathf.Clamp((int)item.Rarity, 0, EQUIP_PRICE.Length - 1);

            return item.Kind switch
            {
                ItemKind.Equipment => EQUIP_PRICE[rarity],
                ItemKind.Consumable => item.Id == ItemCatalog.POTION_ID ? 40 : EQUIP_PRICE[rarity] / 2,
                _ => Mathf.Max(10, EQUIP_PRICE[rarity] / 4)
            };
        }

        public static int SellPrice(ItemDefinition item)
        {
            return Mathf.Max(1, Mathf.RoundToInt(Price(item) * SELL_RATE));
        }

        public static List<ShopOffer> Stock(ShopType type)
        {
            if (_stock.TryGetValue(type, out List<ShopOffer> list))
            {
                return list;
            }

            list = new List<ShopOffer>();

            switch (type)
            {
                case ShopType.Goods:
                    Offer(list, ItemCatalog.Get(ItemCatalog.POTION_ID), -1);
                    Offer(list, ItemCatalog.Get(ItemCatalog.ELIXIR_ID), 2);
                    Offer(list, ItemCatalog.Get("mat_shard"), -1);
                    Offer(list, ItemCatalog.Get("mat_scroll"), 5);
                    break;
                case ShopType.Clinic:
                    Offer(list, ItemCatalog.Get(ItemCatalog.POTION_ID), -1);
                    Offer(list, ItemCatalog.Get(ItemCatalog.ELIXIR_ID), 1);
                    break;
                case ShopType.Forge:
                    for (int i = 0; i < FORGE_STOCK; i++)
                    {
                        ItemRarity rarity = i < 3 ? ItemRarity.Common : i < 5 ? ItemRarity.Uncommon : ItemRarity.Rare;
                        Offer(list, ItemCatalog.RandomEquipment(rarity), 1);
                    }

                    break;
            }

            _stock[type] = list;
            return list;
        }

        /// <summary>진열 새로 고침 (다음에 열 때 다시 만듦)</summary>
        public static void Restock()
        {
            _stock.Clear();
        }

        private static void Offer(List<ShopOffer> list, ItemDefinition item, int count)
        {
            if (item != null)
            {
                list.Add(new ShopOffer { Item = item, Price = Price(item), Count = count });
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            _stock.Clear();
        }
    }
}
