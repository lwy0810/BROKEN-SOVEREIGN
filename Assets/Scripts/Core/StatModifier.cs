namespace BrokenSovereign.Core
{
    /// <summary>
    /// 능력치 수정치 하나 (장비 옵션, 신물 효과, 버프 등).
    /// </summary>
    public readonly struct StatModifier
    {
        public readonly StatType Stat;
        public readonly ModifierType Type;
        public readonly float Value;

        public StatModifier(StatType stat, ModifierType type, float value)
        {
            Stat = stat;
            Type = type;
            Value = value;
        }
    }
}
