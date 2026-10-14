using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 연속 충격파 일정 (순수 클래스). 파동 i는 i × 간격 시점에 발생하고 반경은 기본 반경 + i × 증가량입니다.
    /// 사용 기술: 격진
    /// </summary>
    public class ShockwavePlan
    {
        public int WaveCount { get; }
        public float Interval { get; }
        public float BaseRadius { get; }
        public float RadiusStep { get; }

        public ShockwavePlan(int waveCount, float interval, float baseRadius, float radiusStep)
        {
            WaveCount = Mathf.Max(1, waveCount);
            Interval = Mathf.Max(0f, interval);
            BaseRadius = Mathf.Max(0f, baseRadius);
            RadiusStep = radiusStep;
        }

        /// <summary>마지막 파동이 발생하는 시점</summary>
        public float TotalDuration => (WaveCount - 1) * Interval;

        public float TimeAt(int index)
        {
            return Mathf.Clamp(index, 0, WaveCount - 1) * Interval;
        }

        public float RadiusAt(int index)
        {
            return Mathf.Max(0f, BaseRadius + Mathf.Clamp(index, 0, WaveCount - 1) * RadiusStep);
        }

        public bool IsLast(int index)
        {
            return index == WaveCount - 1;
        }

        /// <summary>경과 시간까지 발생했어야 하는 파동 수 (0 ~ WaveCount)</summary>
        public int WavesDueBy(float elapsed)
        {
            if (elapsed < 0f)
            {
                return 0;
            }

            if (Interval <= 0f)
            {
                return WaveCount;
            }

            // 부동소수 오차로 경계 시점 파동을 놓치지 않도록 작은 여유값
            int due = Mathf.FloorToInt(elapsed / Interval + 0.0001f) + 1;
            return Mathf.Min(due, WaveCount);
        }
    }
}
