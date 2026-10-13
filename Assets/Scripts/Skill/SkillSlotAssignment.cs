using System;
using BrokenSovereign.Core;

namespace BrokenSovereign.Skill
{
    /// <summary>슬롯과 기술 연결 한 쌍 (SkillCaster 초기 목록, JobDefinition 기술 구성)</summary>
    [Serializable]
    public struct SkillSlotAssignment
    {
        public SkillSlot Slot;
        public SkillDefinition Skill;

        public SkillSlotAssignment(SkillSlot slot, SkillDefinition skill)
        {
            Slot = slot;
            Skill = skill;
        }
    }
}
