namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 받는 피해 배율 (같은 오브젝트의 Health가 방어 측 감소 뒤에 곱함).
    /// 예: 무력화 중 받는 피해 +25% (PoiseReceiver)
    /// </summary>
    public interface IDamageTakenModifier
    {
        /// <summary>1 = 그대로, 1.25 = 25% 증가</summary>
        float DamageTakenMultiplier { get; }
    }
}
