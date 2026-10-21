using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 프로토타입 아이템 목록 (10/21). 장비 6부위 × 희귀도별, 소모품(치유 물약), 재료.
    /// 수치는 시연용 초안 (레벨 1~10 구간, 기본 공격력 50 기준).
    /// </summary>
    public static class ItemCatalog
    {
        public const string POTION_ID = "potion_heal";

        private static readonly Color IRON = new Color(0.85f, 0.83f, 0.78f);
        private static readonly Color BRONZE = new Color(0.86f, 0.66f, 0.42f);
        private static readonly Color SILVER = new Color(0.78f, 0.86f, 0.95f);
        private static readonly Color VIOLET = new Color(0.8f, 0.68f, 1f);
        private static readonly Color GOLD = new Color(1f, 0.84f, 0.5f);

        private static readonly List<ItemDefinition> _items = new List<ItemDefinition>();
        private static readonly Dictionary<string, ItemDefinition> _byId = new Dictionary<string, ItemDefinition>();

        public static IReadOnlyList<ItemDefinition> All
        {
            get
            {
                Ensure();
                return _items;
            }
        }

        public static ItemDefinition Get(string id)
        {
            Ensure();
            return id != null && _byId.TryGetValue(id, out ItemDefinition item) ? item : null;
        }

        /// <summary>희귀도에 맞는 장비 하나 (없으면 가까운 낮은 등급)</summary>
        public static ItemDefinition RandomEquipment(ItemRarity rarity)
        {
            Ensure();

            for (int r = (int)rarity; r >= 0; r--)
            {
                List<ItemDefinition> pool = _items.FindAll(i => i.IsEquipment && (int)i.Rarity == r);

                if (pool.Count > 0)
                {
                    return pool[Random.Range(0, pool.Count)];
                }
            }

            return null;
        }

        public static ItemDefinition RandomMaterial(ItemRarity maxRarity)
        {
            Ensure();
            List<ItemDefinition> pool = _items.FindAll(i => i.Kind == ItemKind.Material && i.Rarity <= maxRarity);
            return pool.Count > 0 ? pool[Random.Range(0, pool.Count)] : null;
        }

        private static void Ensure()
        {
            if (_items.Count > 0)
            {
                return;
            }

            // 무기 (공격력)
            Equip("mace_old", "낡은 철퇴", EquipSlot.Weapon, ItemRarity.Common, ItemIcon.Mace, IRON,
                S(StatType.AttackPower, 8f));
            Equip("mace_pilgrim", "순례자의 철퇴", EquipSlot.Weapon, ItemRarity.Uncommon, ItemIcon.Mace, BRONZE,
                S(StatType.AttackPower, 14f), S(StatType.AttackSpeed, 0.03f));
            Equip("mace_gate", "천계 수문 철퇴", EquipSlot.Weapon, ItemRarity.Rare, ItemIcon.Mace, SILVER,
                S(StatType.AttackPower, 22f), S(StatType.CritChance, 0.02f));
            Equip("mace_oath", "맹세의 전투 망치", EquipSlot.Weapon, ItemRarity.Epic, ItemIcon.Mace, VIOLET,
                S(StatType.AttackPower, 32f), S(StatType.CritDamage, 0.15f));
            Equip("mace_crown", "부서진 왕관의 철퇴", EquipSlot.Weapon, ItemRarity.Legendary, ItemIcon.Mace, GOLD,
                S(StatType.AttackPower, 45f), S(StatType.DamageBonus, 0.08f), S(StatType.CritChance, 0.03f));

            // 투구 (방어력·최대 체력)
            Equip("helm_leather", "가죽 두건", EquipSlot.Helmet, ItemRarity.Common, ItemIcon.Helmet, BRONZE,
                S(StatType.Armor, 4f));
            Equip("helm_iron", "철제 투구", EquipSlot.Helmet, ItemRarity.Uncommon, ItemIcon.Helmet, IRON,
                S(StatType.Armor, 8f), S(StatType.MaxHealth, 10f));
            Equip("helm_guard", "수문장의 투구", EquipSlot.Helmet, ItemRarity.Rare, ItemIcon.Helmet, SILVER,
                S(StatType.Armor, 12f), S(StatType.MaxHealth, 20f));
            Equip("helm_saint", "성투사의 관", EquipSlot.Helmet, ItemRarity.Epic, ItemIcon.Helmet, VIOLET,
                S(StatType.Armor, 16f), S(StatType.MaxHealth, 30f), S(StatType.CooldownReduction, 0.03f));

            // 갑옷 (방어력·받는 피해 감소)
            Equip("armor_cloth", "누빔 갑옷", EquipSlot.Armor, ItemRarity.Common, ItemIcon.Armor, BRONZE,
                S(StatType.Armor, 8f));
            Equip("armor_chain", "사슬 갑옷", EquipSlot.Armor, ItemRarity.Uncommon, ItemIcon.Armor, IRON,
                S(StatType.Armor, 14f), S(StatType.MaxHealth, 15f));
            Equip("armor_plate", "천계 판금 갑옷", EquipSlot.Armor, ItemRarity.Rare, ItemIcon.Armor, SILVER,
                S(StatType.Armor, 22f), S(StatType.DamageReduction, 0.03f));
            Equip("armor_aegis", "불굴의 흉갑", EquipSlot.Armor, ItemRarity.Epic, ItemIcon.Armor, VIOLET,
                S(StatType.Armor, 30f), S(StatType.DamageReduction, 0.05f), S(StatType.MaxHealth, 25f));
            Equip("armor_sovereign", "군주의 잔영 갑주", EquipSlot.Armor, ItemRarity.Legendary, ItemIcon.Armor, GOLD,
                S(StatType.Armor, 40f), S(StatType.DamageReduction, 0.08f), S(StatType.ElementalResistance, 0.1f));

            // 장갑 (공격 속도·치명타)
            Equip("glove_leather", "가죽 장갑", EquipSlot.Gloves, ItemRarity.Common, ItemIcon.Gloves, BRONZE,
                S(StatType.AttackSpeed, 0.02f));
            Equip("glove_iron", "철제 건틀릿", EquipSlot.Gloves, ItemRarity.Uncommon, ItemIcon.Gloves, IRON,
                S(StatType.AttackSpeed, 0.04f), S(StatType.Armor, 3f));
            Equip("glove_strike", "타격자의 손", EquipSlot.Gloves, ItemRarity.Rare, ItemIcon.Gloves, SILVER,
                S(StatType.AttackSpeed, 0.06f), S(StatType.CritChance, 0.03f));
            Equip("glove_judge", "심판의 건틀릿", EquipSlot.Gloves, ItemRarity.Epic, ItemIcon.Gloves, VIOLET,
                S(StatType.AttackSpeed, 0.08f), S(StatType.CritDamage, 0.2f));

            // 신발 (이동 속도·회피)
            Equip("boots_leather", "가죽 장화", EquipSlot.Boots, ItemRarity.Common, ItemIcon.Boots, BRONZE,
                S(StatType.MoveSpeed, 0.2f));
            Equip("boots_iron", "강철 각반", EquipSlot.Boots, ItemRarity.Uncommon, ItemIcon.Boots, IRON,
                S(StatType.MoveSpeed, 0.3f), S(StatType.Armor, 4f));
            Equip("boots_wind", "바람결 장화", EquipSlot.Boots, ItemRarity.Rare, ItemIcon.Boots, SILVER,
                S(StatType.MoveSpeed, 0.4f), S(StatType.DodgeChance, 0.03f));
            Equip("boots_cloud", "구름 걸음", EquipSlot.Boots, ItemRarity.Epic, ItemIcon.Boots, VIOLET,
                S(StatType.MoveSpeed, 0.6f), S(StatType.DodgeChance, 0.05f));

            // 장신구 (치명타·자원)
            Equip("ring_copper", "구리 반지", EquipSlot.Accessory, ItemRarity.Common, ItemIcon.Ring, BRONZE,
                S(StatType.CritChance, 0.01f));
            Equip("ring_jade", "옥 반지", EquipSlot.Accessory, ItemRarity.Uncommon, ItemIcon.Ring, new Color(0.7f, 0.92f, 0.65f),
                S(StatType.CritChance, 0.02f), S(StatType.LifeRegen, 0.5f));
            Equip("ring_star", "별빛 반지", EquipSlot.Accessory, ItemRarity.Rare, ItemIcon.Ring, SILVER,
                S(StatType.CritChance, 0.03f), S(StatType.ResourceCostReduction, 0.05f));
            Equip("ring_seal", "수문장의 금빛 인장", EquipSlot.Accessory, ItemRarity.Legendary, ItemIcon.Ring, GOLD,
                S(StatType.CritChance, 0.04f), S(StatType.DamageBonus, 0.06f), S(StatType.CooldownReduction, 0.05f));

            // 소모품·재료
            Add(new ItemDefinition(POTION_ID, "치유 물약", ItemKind.Consumable, EquipSlot.Weapon, ItemRarity.Common,
                ItemIcon.Potion, new Color(0.85f, 0.25f, 0.2f), 20, "우클릭: 물약 칸 1 충전"));
            Add(new ItemDefinition("mat_shard", "천계 파편", ItemKind.Material, EquipSlot.Weapon, ItemRarity.Common,
                ItemIcon.Gem, new Color(0.75f, 0.77f, 0.82f), 99, "천계 적이 남긴 빛바랜 조각"));
            Add(new ItemDefinition("mat_crystal", "빛나는 결정", ItemKind.Material, EquipSlot.Weapon, ItemRarity.Rare,
                ItemIcon.Gem, new Color(0.5f, 0.75f, 1f), 99, "정예가 지니던 맑은 결정"));
            Add(new ItemDefinition("mat_essence", "신성 정수", ItemKind.Material, EquipSlot.Weapon, ItemRarity.Epic,
                ItemIcon.Gem, new Color(0.79f, 0.66f, 1f), 99, "신의 힘이 응축된 정수"));
            Add(new ItemDefinition("mat_scroll", "낡은 기도문", ItemKind.Material, EquipSlot.Weapon, ItemRarity.Uncommon,
                ItemIcon.Scroll, new Color(0.9f, 0.85f, 0.7f), 20, "누군가의 기도가 적힌 두루마리"));
        }

        private static StatModifier S(StatType stat, float value)
        {
            return new StatModifier(stat, ModifierType.Flat, value);
        }

        private static void Equip(string id, string name, EquipSlot slot, ItemRarity rarity, ItemIcon icon, Color color,
            params StatModifier[] stats)
        {
            Add(new ItemDefinition(id, name, ItemKind.Equipment, slot, rarity, icon, color, 1, string.Empty, stats));
        }

        private static void Add(ItemDefinition item)
        {
            _items.Add(item);
            _byId[item.Id] = item;
        }
    }
}
