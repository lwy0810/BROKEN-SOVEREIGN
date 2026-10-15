using BrokenSovereign.Combat;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>영웅 규칙이 사용하는 플레이어 참조 묶음 (순수 클래스)</summary>
    public class HeroRuleContext
    {
        public HeroRuleContext(GameObject player, SkillCaster caster, CharacterStats stats)
        {
            Player = player;
            Caster = caster;
            Stats = stats;
        }

        public GameObject Player { get; }
        public SkillCaster Caster { get; }
        public CharacterStats Stats { get; }
    }
}
