namespace BrokenSovereign.Hero
{
    /// <summary>
    /// 영웅 고유 메커니즘 규칙 (순수 클래스). 이벤트를 구독해 동작하며 전투 코드에 영웅별 분기를 두지 않습니다.
    /// 수명: HeroPathController.SelectPath에서 생성 → Activate, 플레이어 제거 시 Deactivate.
    /// 예: 석천 사거리 전환(ReachShiftRule), 야로미르 분쇄(CrushRule)
    /// </summary>
    public abstract class HeroRule
    {
        protected HeroRule(HeroRuleContext context)
        {
            Context = context;
        }

        protected HeroRuleContext Context { get; }

        /// <summary>이벤트 구독·수정치 등록</summary>
        public abstract void Activate();

        /// <summary>구독·등록 해제</summary>
        public abstract void Deactivate();
    }
}
