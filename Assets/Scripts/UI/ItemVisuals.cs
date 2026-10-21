using System.Collections.Generic;
using BrokenSovereign.Item;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 아이템 표시용 색·이름·스프라이트 (10/21 인벤토리). 아이콘은 32칸 격자 다각형/원으로 코드에서 그린 흰 도형이고
    /// Image.color(아이템 색)로 물들입니다. 칸 테두리·안쪽 빛은 희귀도 색으로 물들이는 9-slice.
    /// </summary>
    public static class ItemVisuals
    {
        private static readonly Dictionary<ItemIcon, Sprite> _icons = new Dictionary<ItemIcon, Sprite>();
        private static Sprite _frame;
        private static Sprite _glow;

        public static Color RarityColor(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return Hex(0x5F, 0xB3, 0x6A);
                case ItemRarity.Rare: return Hex(0x4A, 0x90, 0xE2);
                case ItemRarity.Epic: return Hex(0xB4, 0x8C, 0xFF);
                case ItemRarity.Legendary: return Hex(0xE9, 0xB4, 0x5A);
                default: return Hex(0x9A, 0xA0, 0xAA);
            }
        }

        public static string RarityName(ItemRarity rarity)
        {
            switch (rarity)
            {
                case ItemRarity.Uncommon: return "고급";
                case ItemRarity.Rare: return "희귀";
                case ItemRarity.Epic: return "영웅";
                case ItemRarity.Legendary: return "전설";
                default: return "일반";
            }
        }

        public static string SlotName(EquipSlot slot)
        {
            switch (slot)
            {
                case EquipSlot.Weapon: return "무기";
                case EquipSlot.Helmet: return "투구";
                case EquipSlot.Armor: return "갑옷";
                case EquipSlot.Gloves: return "장갑";
                case EquipSlot.Boots: return "신발";
                default: return "장신구";
            }
        }

        public static string KindName(ItemDefinition item)
        {
            switch (item.Kind)
            {
                case ItemKind.Equipment: return SlotName(item.Slot);
                case ItemKind.Consumable: return "소모품";
                default: return "재료";
            }
        }

        /// <summary>칸 테두리 (흰 선, 희귀도 색으로 물들임). 9-slice</summary>
        public static Sprite Frame
        {
            get
            {
                if (_frame == null)
                {
                    _frame = UiSprites.Make("ItemFrame", 64, 64, 16, (x, y, w, h) =>
                    {
                        float d = UiSprites.RoundRect(x, y, w, h, 7f);
                        float a = Mathf.Clamp01(d + 2.5f) ;
                        return UiSprites.Cover(new Color(1f, 1f, 1f, a), d);
                    });
                }

                return _frame;
            }
        }

        /// <summary>칸 안쪽 가장자리 빛 (희귀도 색). 9-slice</summary>
        public static Sprite Glow
        {
            get
            {
                if (_glow == null)
                {
                    _glow = UiSprites.Make("ItemGlow", 64, 64, 20, (x, y, w, h) =>
                    {
                        float d = UiSprites.RoundRect(x, y, w, h, 7f);
                        float a = Mathf.Clamp01(1f + d / 14f);
                        return UiSprites.Cover(new Color(1f, 1f, 1f, a * a * 0.9f), d);
                    });
                }

                return _glow;
            }
        }

        public static Sprite Icon(ItemIcon icon)
        {
            if (!_icons.TryGetValue(icon, out Sprite sprite) || sprite == null)
            {
                sprite = UiSprites.MakeSuper("ItemIcon" + icon, 64, (u, v) => Inside(icon, u * 32f, (1f - v) * 32f) ? Color.white : Color.clear);
                _icons[icon] = sprite;
            }

            return sprite;
        }

        // 32칸 격자 (y 아래로) 안쪽 판정
        private static bool Inside(ItemIcon icon, float x, float y)
        {
            switch (icon)
            {
                case ItemIcon.Mace:
                    return Poly(x, y, 5f, 25f, 18.5f, 11.5f, 21.5f, 14.5f, 8f, 28f) ||
                        Poly(x, y, 16f, 9.5f, 22.5f, 3f, 29f, 9.5f, 22.5f, 16f);
                case ItemIcon.Helmet:
                    return Poly(x, y, 6f, 25f, 6f, 14f, 9f, 8f, 16f, 5f, 23f, 8f, 26f, 14f, 26f, 25f, 21f, 25f, 21f, 18f, 11f, 18f, 11f, 25f);
                case ItemIcon.Armor:
                    return Poly(x, y, 8f, 5f, 12f, 7.5f, 16f, 5f, 20f, 7.5f, 24f, 5f, 29f, 12f, 24.5f, 15f, 24.5f, 27f, 7.5f, 27f, 7.5f, 15f, 3f, 12f);
                case ItemIcon.Gloves:
                    return Poly(x, y, 10f, 27f, 10f, 13f, 11f, 6f, 13.5f, 6f, 14f, 12f, 15f, 4f, 17.5f, 4f, 18f, 12f, 19f, 5f, 21.5f, 5f,
                        22f, 13f, 23.5f, 9f, 26f, 10f, 24f, 18f, 22f, 27f);
                case ItemIcon.Boots:
                    return Poly(x, y, 10f, 5f, 19f, 5f, 19f, 18f, 27f, 22f, 27f, 27f, 10f, 27f);
                case ItemIcon.Ring:
                {
                    float d = Dist(x, y, 16f, 19f);
                    return (d < 9.5f && d > 5.8f) || Poly(x, y, 12.5f, 7f, 16f, 3f, 19.5f, 7f, 16f, 11f);
                }
                case ItemIcon.Potion:
                    return Dist(x, y, 16f, 20f) < 9f || (x > 13f && x < 19f && y > 5f && y < 13f) || (x > 11.5f && x < 20.5f && y > 3f && y < 6f);
                case ItemIcon.Gem:
                    return Poly(x, y, 16f, 3f, 26f, 12f, 16f, 29f, 6f, 12f);
                case ItemIcon.Scroll:
                {
                    bool page = x > 8f && x < 24f && y > 5f && y < 27f;
                    bool line = x > 11f && ((y > 10f && y < 12f && x < 21f) || (y > 15f && y < 17f && x < 21f) || (y > 20f && y < 22f && x < 18f));
                    return page && !line;
                }
                default:
                    return false;
            }
        }

        private static float Dist(float x, float y, float cx, float cy)
        {
            return Mathf.Sqrt((x - cx) * (x - cx) + (y - cy) * (y - cy));
        }

        // 짝홀 규칙 점-다각형 판정 (꼭짓점 x,y 나열)
        private static bool Poly(float x, float y, params float[] p)
        {
            bool inside = false;
            int n = p.Length / 2;

            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = p[i * 2], yi = p[i * 2 + 1], xj = p[j * 2], yj = p[j * 2 + 1];

                if ((yi > y) != (yj > y) && x < (xj - xi) * (y - yi) / (yj - yi) + xi)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Color Hex(int r, int g, int b)
        {
            return new Color(r / 255f, g / 255f, b / 255f);
        }
    }
}
