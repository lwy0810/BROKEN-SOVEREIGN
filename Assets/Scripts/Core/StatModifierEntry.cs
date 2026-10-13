using System;

namespace BrokenSovereign.Core
{
    /// <summary>Inspector에서 편집하는 능력치 수정치 (버프·장비 정의용)</summary>
    [Serializable]
    public struct StatModifierEntry
    {
        public StatType Stat;
        public ModifierType Type;

        [UnityEngine.Tooltip("비율 능력치는 소수 (0.25 = 25%)")]
        public float Value;

        public StatModifierEntry(StatType stat, ModifierType type, float value)
        {
            Stat = stat;
            Type = type;
            Value = value;
        }

        public StatModifier ToModifier()
        {
            return new StatModifier(Stat, Type, Value);
        }
    }
}
