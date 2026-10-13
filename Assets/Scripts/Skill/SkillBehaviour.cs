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
