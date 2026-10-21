using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 가방 (10/21): 분류(장비·소모품·재료)마다 따로 96칸(12×8), 겹침, 골드. 아이템은 자기 분류 가방에만 들어갑니다.
    /// 칸 위치는 플레이어가 끌어서 바꿀 수 있고 정렬 버튼으로 정리합니다. UI(InventoryWindowView)는 Changed를 받아 다시 그립니다.
    /// </summary>
    public class Inventory : MonoBehaviour
    {
        public const int SIZE = 96;

        private readonly ItemStack[][] _pages =
        {
            new ItemStack[SIZE], new ItemStack[SIZE], new ItemStack[SIZE]
        };

        public int Gold { get; private set; }

        /// <summary>내용이 바뀜 (UI 다시 그리기)</summary>
        public event Action Changed;

        /// <summary>새로 얻음 (획득 알림). 아이템, 개수</summary>
        public event Action<ItemDefinition, int> ItemAcquired;

        /// <summary>골드 얻음 (획득 알림)</summary>
        public event Action<int> GoldAcquired;

        private ItemStack[] Page(ItemKind kind)
        {
            return _pages[(int)kind];
        }

        public int UsedCount(ItemKind kind)
        {
            int count = 0;

            foreach (ItemStack stack in Page(kind))
            {
                if (stack != null)
                {
                    count++;
                }
            }

            return count;
        }

        public ItemStack Get(ItemKind kind, int index)
        {
            return index >= 0 && index < SIZE ? Page(kind)[index] : null;
        }

        /// <summary>그 분류 가방에서 겹칠 수 있는 칸부터 채우고 남으면 빈 칸에. 다 못 넣으면 남은 개수 반환</summary>
        public int Add(ItemDefinition item, int count = 1, bool notify = true)
        {
            if (item == null || count <= 0)
            {
                return 0;
            }

            ItemStack[] slots = Page(item.Kind);
            int remaining = count;

            for (int i = 0; i < SIZE && remaining > 0 && item.MaxStack > 1; i++)
            {
                ItemStack stack = slots[i];

                if (stack != null && stack.Item == item && stack.Count < item.MaxStack)
                {
                    int add = Mathf.Min(remaining, item.MaxStack - stack.Count);
                    stack.Count += add;
                    remaining -= add;
                }
            }

            for (int i = 0; i < SIZE && remaining > 0; i++)
            {
                if (slots[i] == null)
                {
                    int add = Mathf.Min(remaining, item.MaxStack);
                    slots[i] = new ItemStack(item, add);
                    remaining -= add;
                }
            }

            int added = count - remaining;

            if (added > 0)
            {
                if (notify)
                {
                    ItemAcquired?.Invoke(item, added);
                }

                Changed?.Invoke();
            }

            return remaining;
        }

        /// <summary>지정 칸에 그대로 놓기 (그 아이템 분류 가방의 빈 칸일 때만)</summary>
        public bool Place(int index, ItemStack stack)
        {
            if (stack == null || index < 0 || index >= SIZE || Page(stack.Item.Kind)[index] != null)
            {
                return false;
            }

            Page(stack.Item.Kind)[index] = stack;
            Changed?.Invoke();
            return true;
        }

        /// <summary>칸에서 개수만큼 빼기. 다 빠지면 칸 비움</summary>
        public void Remove(ItemKind kind, int index, int count = 1)
        {
            ItemStack stack = Get(kind, index);

            if (stack == null)
            {
                return;
            }

            stack.Count -= count;

            if (stack.Count <= 0)
            {
                Page(kind)[index] = null;
            }

            Changed?.Invoke();
        }

        /// <summary>칸에서 꺼냄 (칸 비움)</summary>
        public ItemStack Take(ItemKind kind, int index)
        {
            ItemStack stack = Get(kind, index);

            if (stack != null)
            {
                Page(kind)[index] = null;
                Changed?.Invoke();
            }

            return stack;
        }

        /// <summary>같은 가방 안 두 칸 바꾸기. 같은 아이템이면 겹치기</summary>
        public void Move(ItemKind kind, int from, int to)
        {
            ItemStack[] slots = Page(kind);

            if (from == to || Get(kind, from) == null || to < 0 || to >= SIZE)
            {
                return;
            }

            ItemStack a = slots[from];
            ItemStack b = slots[to];

            if (b != null && b.Item == a.Item && a.Item.MaxStack > 1 && b.Count < b.Item.MaxStack)
            {
                int add = Mathf.Min(a.Count, b.Item.MaxStack - b.Count);
                b.Count += add;
                a.Count -= add;

                if (a.Count <= 0)
                {
                    slots[from] = null;
                }
            }
            else
            {
                slots[from] = b;
                slots[to] = a;
            }

            Changed?.Invoke();
        }

        public int FirstEmpty(ItemKind kind)
        {
            ItemStack[] slots = Page(kind);

            for (int i = 0; i < SIZE; i++)
            {
                if (slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>정렬: 장비는 부위 순, 모두 희귀도 높은 순·이름 순. 같은 아이템은 합침</summary>
        public void Sort(ItemKind kind)
        {
            ItemStack[] slots = Page(kind);
            var stacks = new List<ItemStack>();

            foreach (ItemStack stack in slots)
            {
                if (stack != null)
                {
                    stacks.Add(stack);
                }
            }

            Array.Clear(slots, 0, SIZE);
            stacks.Sort((a, b) =>
            {
                int c;
                if (a.Item.IsEquipment && (c = a.Item.Slot.CompareTo(b.Item.Slot)) != 0) return c;
                if ((c = b.Item.Rarity.CompareTo(a.Item.Rarity)) != 0) return c;
                return string.CompareOrdinal(a.Item.DisplayName, b.Item.DisplayName);
            });

            int index = 0;

            foreach (ItemStack stack in stacks)
            {
                ItemStack last = index > 0 ? slots[index - 1] : null;

                if (last != null && last.Item == stack.Item && last.Count < last.Item.MaxStack)
                {
                    int add = Mathf.Min(stack.Count, last.Item.MaxStack - last.Count);
                    last.Count += add;
                    stack.Count -= add;
                }

                if (stack.Count > 0)
                {
                    slots[index++] = stack;
                }
            }

            Changed?.Invoke();
        }

        public void AddGold(int amount, bool notify = true)
        {
            if (amount <= 0)
            {
                return;
            }

            Gold += amount;

            if (notify)
            {
                GoldAcquired?.Invoke(amount);
            }

            Changed?.Invoke();
        }
    }
}
