namespace BrokenSovereign.Core
{
    /// <summary>
    /// 능력치 종류. 비율 능력치는 소수로 저장합니다 (0.05 = 5%).
    /// 1차 스탯(힘·민첩·지능)은 StatSheet에서 2차 능력치로 변환됩니다.
    /// </summary>
    public enum StatType
    {
        // 1차 스탯
        Strength,
        Agility,
        Intelligence,

        // 공격
        AttackPower,
        DamageBonus,
        PhysicalDamageBonus,
        ElementalDamageBonus,
        AttackSpeed,
        CritChance,
        CritDamage,

        // 방어
        MaxHealth,
        LifeRegen,
        Armor,
        ElementalResistance,
        MaxElementalResistance,
        DodgeChance,
        DamageReduction,

        // 유틸
        MoveSpeed,
        CooldownReduction,
        ResourceCostReduction,
        PotionHealing
    }
}
