using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>아이템 분류 (인벤토리 탭: 장비·소모품·재료)</summary>
    public enum ItemKind
    {
        Equipment,
        Consumable,
        Material
    }

    /// <summary>장비 부위 (인벤토리·캐릭터 창 장비 6칸 순서)</summary>
    public enum EquipSlot
    {
        Weapon,
        Helmet,
        Armor,
        Gloves,
        Boots,
        Accessory
    }

    /// <summary>희귀도 (낮음 → 높음). 칸 테두리 색으로 구분</summary>
    public enum ItemRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    /// <summary>아이콘 모양 (UI가 코드로 그림)</summary>
    public enum ItemIcon
    {
        Mace,
        Helmet,
        Armor,
        Gloves,
        Boots,
        Ring,
        Potion,
        Gem,
        Scroll
    }

    /// <summary>
    /// 아이템 정의 (10/21 인벤토리). 프로토타입은 에셋 대신 ItemCatalog 코드 목록으로 정의합니다.
    /// 장비 능력치는 착용 시 PlayerEquipment가 StatSheet에 더합니다 (Flat: 비율 능력치는 소수, 0.03 = 3%).
    /// </summary>
    public sealed class ItemDefinition
    {
        public string Id { get; }
        public string DisplayName { get; }
        public ItemKind Kind { get; }
        public EquipSlot Slot { get; }
        public ItemRarity Rarity { get; }
        public ItemIcon Icon { get; }
        public Color IconColor { get; }
        public int MaxStack { get; }
        public string Description { get; }
        public IReadOnlyList<StatModifier> Stats { get; }

        public bool IsEquipment => Kind == ItemKind.Equipment;

        /// <summary>중요 아이템: 소모품이면 사용 전 확인 창 (10/25)</summary>
        public bool IsImportant { get; private set; }

        /// <summary>중요 아이템으로 표시 (카탈로그 등록 시 체인 호출)</summary>
        public ItemDefinition MarkImportant()
        {
            IsImportant = true;
            return this;
        }

        public ItemDefinition(string id, string displayName, ItemKind kind, EquipSlot slot, ItemRarity rarity,
            ItemIcon icon, Color iconColor, int maxStack, string description, params StatModifier[] stats)
        {
            Id = id;
            DisplayName = displayName;
            Kind = kind;
            Slot = slot;
            Rarity = rarity;
            Icon = icon;
            IconColor = iconColor;
            MaxStack = Mathf.Max(1, maxStack);
            Description = description ?? string.Empty;
            Stats = stats ?? new StatModifier[0];
        }

        /// <summary>이 아이템이 주는 능력치 합 (없으면 0)</summary>
        public float GetStat(StatType stat)
        {
            float sum = 0f;

            foreach (StatModifier modifier in Stats)
            {
                if (modifier.Stat == stat && modifier.Type == ModifierType.Flat)
                {
                    sum += modifier.Value;
                }
            }

            return sum;
        }
    }

    /// <summary>가방 한 칸: 아이템 + 개수</summary>
    public sealed class ItemStack
    {
        public ItemDefinition Item;
        public int Count;

        public ItemStack(ItemDefinition item, int count)
        {
            Item = item;
            Count = count;
        }
    }
}
