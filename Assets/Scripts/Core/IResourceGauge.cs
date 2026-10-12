using UnityEngine;

namespace BrokenSovereign.Core
{
    /// <summary>
    /// HUD 자원 구슬에 표시할 직업 자원 (신앙, 투기, 마나 등).
    /// 무직 상태처럼 자원이 없으면 구현체가 없으며, HUD는 잠긴 구슬로 표시합니다.
    /// </summary>
    public interface IResourceGauge
    {
        float Current { get; }
        float Max { get; }

        /// <summary>구슬 액체 색</summary>
        Color DisplayColor { get; }
    }
}
