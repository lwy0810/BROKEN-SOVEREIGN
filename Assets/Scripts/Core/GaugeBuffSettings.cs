using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Core
{
    /// <summary>
    /// 직업 게이지가 가득 찼을 때 발동하는 버프 (직업마다 JobDefinition에서 설정).
    /// 지속 동안 능력치 수정치를 적용하고 모든 기술을 강화합니다.
    /// </summary>
    [Serializable]
    public class GaugeBuffSettings
    {
        public string DisplayName = "충만";

        [Min(0.1f)] public float Duration = 10f;

        [Tooltip("버프 동안 기술 충만 수정치 적용")]
        public bool EmpowerSkills = true;

        public List<StatModifierEntry> Modifiers = new List<StatModifierEntry>
        {
            new StatModifierEntry(StatType.DamageBonus, ModifierType.Flat, 0.25f),
            new StatModifierEntry(StatType.DamageReduction, ModifierType.Flat, 0.15f)
        };
    }
}
