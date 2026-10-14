namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기본 공격 간격(초, 공격 속도 0% 기준)을 정하는 출처. 무기 장착 시스템이 구현합니다 (예: 한손 둔기 1.0초, 양손 둔기 1.3초).
    /// 공격자에게 이 컴포넌트가 있으면 기본 공격과 즉시 발동 기술이 이 간격을 사용하고, 없으면 각자의 기본값(1초)을 씁니다.
    /// 실제 간격 = 기본 공격 간격 ÷ (1 + 공격 속도)
    /// </summary>
    public interface IAttackIntervalSource
    {
        float BaseAttackInterval { get; }
    }
}
