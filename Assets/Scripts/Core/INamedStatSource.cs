namespace BrokenSovereign.Core
{
    /// <summary>
    /// 능력치 출처 이름 (디버그 출력에서 보너스 출처 표시용: 레벨, 격, 충만, 기술 버프 등).
    /// 구현하지 않은 출처는 클래스 이름으로 표시합니다.
    /// </summary>
    public interface INamedStatSource
    {
        string SourceName { get; }
    }
}
