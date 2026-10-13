using UnityEngine;

namespace BrokenSovereign.Core
{
    /// <summary>
    /// 직업 고유 게이지 (신앙·투기·집중 등). HUD 기술 바 위 막대로 표시합니다.
    /// 가득 차면 버프가 발동하고, 버프 동안 게이지가 줄어들며 남은 시간을 나타냅니다.
    /// </summary>
    public interface IJobGauge
    {
        string GaugeName { get; }
        float Current { get; }
        float Max { get; }
        Color DisplayColor { get; }

        bool IsBuffActive { get; }
        string BuffName { get; }
        float BuffRemaining { get; }
    }
}
