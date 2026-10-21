using BrokenSovereign.Core;

namespace BrokenSovereign.UI
{
    /// <summary>능력치 표시 이름·형식 (캐릭터 창·상세 정보·아이템 툴팁 공용, 10/21)</summary>
    public static class StatText
    {
        public static string Name(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "힘";
                case StatType.Agility: return "민첩";
                case StatType.Intelligence: return "지능";
                case StatType.AttackPower: return "공격력";
                case StatType.DamageBonus: return "피해 증가";
                case StatType.PhysicalDamageBonus: return "물리 피해 증가";
                case StatType.ElementalDamageBonus: return "원소 피해 증가";
                case StatType.AttackSpeed: return "공격 속도";
                case StatType.CritChance: return "치명타 확률";
                case StatType.CritDamage: return "치명타 피해";
                case StatType.MaxHealth: return "최대 체력";
                case StatType.LifeRegen: return "초당 생명력 회복";
                case StatType.Armor: return "방어력";
                case StatType.ElementalResistance: return "원소 저항";
                case StatType.MaxElementalResistance: return "최대 원소 저항";
                case StatType.DodgeChance: return "회피율";
                case StatType.DamageReduction: return "받는 피해 감소";
                case StatType.MoveSpeed: return "이동 속도";
                case StatType.CooldownReduction: return "재사용 대기 감소";
                case StatType.ResourceCostReduction: return "기력 소모 감소";
                case StatType.PotionHealing: return "물약 회복량 증가";
                case StatType.GaugeGainOnDamaged: return "피격 시 신앙 충전";
                default: return stat.ToString();
            }
        }

        /// <summary>비율 능력치 (소수로 저장, % 표시)</summary>
        public static bool IsPercent(StatType stat)
        {
            switch (stat)
            {
                case StatType.DamageBonus:
                case StatType.PhysicalDamageBonus:
                case StatType.ElementalDamageBonus:
                case StatType.AttackSpeed:
                case StatType.CritChance:
                case StatType.CritDamage:
                case StatType.ElementalResistance:
                case StatType.MaxElementalResistance:
                case StatType.DodgeChance:
                case StatType.DamageReduction:
                case StatType.CooldownReduction:
                case StatType.ResourceCostReduction:
                case StatType.PotionHealing:
                case StatType.GaugeGainOnDamaged:
                    return true;
                default:
                    return false;
            }
        }

        public static string Value(StatType stat, float value)
        {
            return IsPercent(stat) ? $"{value * 100f:0.#}%" : $"{value:0.#}";
        }

        /// <summary>부호 붙인 값 (+3% / -5)</summary>
        public static string Signed(StatType stat, float value)
        {
            string sign = value >= 0f ? "+" : "-";
            float abs = value < 0f ? -value : value;
            return sign + Value(stat, abs);
        }
    }
}
