namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 공격 모션에서 무기가 닿는 순간까지의 시간 (10/26). 공격자(PlayerAnimator 등)가 구현하면
    /// BasicAttack이 그 시간 뒤에 피해를 적용합니다. 없으면 BasicAttack의 기본 지연을 씁니다.
    /// </summary>
    public interface IAttackImpactTiming
    {
        /// <summary>공격 간격(공격 속도 반영)으로 재생할 때, 공격 시작부터 타격 순간까지의 시간(초)</summary>
        float GetImpactDelay(float interval);
    }
}
