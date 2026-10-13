namespace BrokenSovereign.Core
{
    /// <summary>
    /// 수정치 적용 방식.
    /// - Flat: 기본값에 더함
    /// - Percent: (기본값 + Flat 합)에 (1 + 비율 합)을 곱함
    /// - Multiplicative: 출처마다 곱해서 쌓음. 1 − (1 − a)(1 − b)… (피해 감소, 재사용 대기 감소용)
    /// </summary>
    public enum ModifierType
    {
        Flat,
        Percent,
        Multiplicative
    }
}
