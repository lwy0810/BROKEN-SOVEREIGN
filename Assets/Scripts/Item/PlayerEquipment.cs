using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 장착 장비 6칸 (무기·투구·갑옷·장갑·신발·장신구, 10/21). 장착 장비 능력치 합을 StatSheet 출처 "장비"로 더합니다.
    /// 무기 아이템은 능력치만 반영하고 손의 외형은 PlayerWeapon(직업)이 정합니다 (GDD 5.2 반신 이전 규칙과 같은 방향).
    /// 가방과 주고받기: 가방 칸 → 장착(자리에 있던 장비는 그 칸으로), 장착 칸 → 가방(빈 칸 또는 지정 칸).
    /// </summary>
    public class PlayerEquipment : MonoBehaviour, IStatModifierSource, INamedStatSource
    {
        public static readonly EquipSlot[] SLOTS =
        {
            EquipSlot.Weapon, EquipSlot.Helmet, EquipSlot.Armor, EquipSlot.Gloves, EquipSlot.Boots, EquipSlot.Accessory
        };

        private readonly Dictionary<EquipSlot, ItemDefinition> _equipped = new Dictionary<EquipSlot, ItemDefinition>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private CharacterStats _stats;
        private Inventory _inventory;

        public string SourceName => "장비";

        public event Action Changed;

        private void Awake()
        {
            _inventory = GetComponent<Inventory>();
            _stats = GetComponent<CharacterStats>();
        }

        private void Start()
        {
            if (_stats != null)
            {
                _stats.Sheet.AddSource(this);
            }
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.Sheet.RemoveSource(this);
            }
        }

        public ItemDefinition Get(EquipSlot slot)
        {
            return _equipped.TryGetValue(slot, out ItemDefinition item) ? item : null;
        }

        /// <summary>장착 장비들의 능력치 합</summary>
        public float GetBonus(StatType stat)
        {
            float sum = 0f;

            foreach (ItemDefinition item in _equipped.Values)
            {
                sum += item.GetStat(stat);
            }

            return sum;
        }

        public IEnumerable<StatModifier> GetModifiers()
        {
            return _modifiers;
        }

        /// <summary>가방 칸의 장비를 장착. 원래 장비는 그 칸으로 돌아감</summary>
        public bool EquipFromInventory(int index)
        {
            ItemStack stack = _inventory != null ? _inventory.Get(index) : null;

            if (stack == null || !stack.Item.IsEquipment)
            {
                return false;
            }

            EquipSlot slot = stack.Item.Slot;
            ItemDefinition previous = Get(slot);
            _inventory.Take(index);
            _equipped[slot] = stack.Item;

            if (previous != null)
            {
                _inventory.Place(index, new ItemStack(previous, 1));
            }

            Rebuild();
            return true;
        }

        /// <summary>장착 해제 → 가방(지정 칸이 비어 있으면 그 칸, 아니면 첫 빈 칸). 가방이 가득이면 실패</summary>
        public bool Unequip(EquipSlot slot, int targetIndex = -1)
        {
            ItemDefinition item = Get(slot);

            if (item == null || _inventory == null)
            {
                return false;
            }

            if (targetIndex >= 0 && _inventory.Get(targetIndex) != null)
            {
                // 지정 칸에 같은 부위 장비가 있으면 맞바꿈
                return _inventory.Get(targetIndex).Item.IsEquipment && _inventory.Get(targetIndex).Item.Slot == slot &&
                    EquipFromInventory(targetIndex);
            }

            int index = targetIndex >= 0 ? targetIndex : _inventory.FirstEmpty();

            if (index < 0)
            {
                return false;
            }

            _equipped.Remove(slot);
            _inventory.Place(index, new ItemStack(item, 1));
            Rebuild();
            return true;
        }

        private void Rebuild()
        {
            _modifiers.Clear();

            foreach (ItemDefinition item in _equipped.Values)
            {
                _modifiers.AddRange(item.Stats);
            }

            if (_stats != null)
            {
                _stats.Sheet.NotifySourcesChanged();
            }

            Changed?.Invoke();
        }
    }
}
