namespace BrokenSovereign.Core
{
    /// <summary>
    /// 기술 시전 비용 자원 (마나). SkillCaster가 시전 전에 소모합니다.
    /// </summary>
    public interface ISkillResource
    {
        bool CanSpend(float amount);

        /// <summary>부족하면 false, 충분하면 소모 후 true</summary>
        bool TrySpend(float amount);
    }
}
