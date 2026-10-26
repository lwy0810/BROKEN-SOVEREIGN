using System;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 동작 (전략 객체). 수치는 SkillContext.Parameters에서 읽고, 동작 방식만 정의합니다.
    /// 즉발 기술은 Begin에서 처리하고 Tick에서 false를 돌려줍니다.
    /// 지속 기술은 Tick이 true인 동안 매 프레임 호출되고, false가 되면 End가 호출됩니다.
    /// </summary>
    public abstract class SkillBehaviour : ScriptableObject
    {
        /// <summary>
        /// 타격 순간 (10/26): 모션의 타격 시점에 피해를 주는 기술이 그 순간에 알림. 효과음이 이 시점에 재생됩니다
        /// </summary>
        public static event Action<SkillDefinition, Vector3> ImpactLanded;

        /// <summary>
        /// true면 시전 시 효과음을 바로 내지 않고 타격 순간(ImpactLanded)에 냄 (일반 공격과 같은 방식)
        /// </summary>
        public virtual bool DelaysImpact => false;

        /// <summary>시작할 때 남아 있어야 하는 자원 (소모하지 않음). 지속 시전의 최소 유지 시간분 (10/26)</summary>
        public virtual float GetStartReserve(SkillParameterSet parameters)
        {
            return 0f;
        }

        protected static void RaiseImpact(SkillDefinition definition, Vector3 point)
        {
            ImpactLanded?.Invoke(definition, point);
        }

        public abstract void Begin(SkillContext context);

        /// <summary>계속 진행하면 true. 기본은 즉발(false)</summary>
        public virtual bool Tick(SkillContext context, float deltaTime)
        {
            return false;
        }

        public virtual void End(SkillContext context)
        {
        }
    }
}
