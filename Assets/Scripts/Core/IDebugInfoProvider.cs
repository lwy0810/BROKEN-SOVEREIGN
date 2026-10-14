namespace BrokenSovereign.Core
{
    /// <summary>
    /// 디버그 출력에 한 줄 정보를 덧붙이는 컴포넌트 (CharacterStats 최종 능력치 출력 등).
    /// 출력하는 쪽이 제공하는 쪽을 몰라도 되도록 Core에 둡니다.
    /// </summary>
    public interface IDebugInfoProvider
    {
        string DebugInfo { get; }
    }
}
