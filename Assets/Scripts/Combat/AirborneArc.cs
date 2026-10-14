using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 띄우기 높이 계산 (순수 클래스). 포물선: 시작·끝은 0, 중간 지점에서 최대 높이.
    /// </summary>
    public static class AirborneArc
    {
        public static float HeightAt(float elapsed, float duration, float maxHeight)
        {
            if (duration <= 0f || elapsed <= 0f || elapsed >= duration)
            {
                return 0f;
            }

            float x = elapsed / duration;
            return 4f * maxHeight * x * (1f - x);
        }
    }
}
