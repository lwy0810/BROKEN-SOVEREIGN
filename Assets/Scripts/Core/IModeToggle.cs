namespace BrokenSovereign.Core
{
    /// <summary>두 모드를 전환하는 고유 메커니즘 (석천 모드변환 등). 전환 기술(ModeToggleBehaviour)이 호출합니다.</summary>
    public interface IModeToggle
    {
        void ToggleMode();
    }
}
