using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>영웅 규칙 설정 데이터. 규칙 객체(HeroRule)를 만듭니다.</summary>
    public abstract class HeroRuleDefinition : ScriptableObject
    {
        public abstract HeroRule CreateRule(HeroRuleContext context);
    }
}
