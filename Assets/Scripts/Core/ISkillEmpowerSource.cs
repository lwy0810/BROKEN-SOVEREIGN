namespace BrokenSovereign.Core
{
    /// <summary>
    /// 기술 강화 상태를 알려 주는 출처 (직업 게이지 충만 버프 등).
    /// 강화 중이면 SkillCaster가 기술의 충만 수정치를 적용합니다.
    /// </summary>
    public interface ISkillEmpowerSource
    {
        bool IsSkillEmpowered { get; }
    }
}
