using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 버팀 게이지 (순수 클래스, EditMode 테스트 대상). 야로미르 분쇄 (GDD 5장).
    /// 피해마다 감소 → 0이 되면 무력화(일정 시간) + 게이지 전량 회복 → 무력화가 끝난 뒤 일정 시간 다시 무력화되지 않음.
    /// 마지막 감소 후 일정 시간이 지나면 초당 최대치의 일정 비율씩 회복합니다.
    /// 시간은 호출하는 쪽이 넘깁니다 (Time.time).
    /// </summary>
    public class PoiseGauge
    {
        private readonly float _recoverDelay;
        private readonly float _recoverRatio;
        private readonly float _immunityDuration;

        private float _lastReduceTime = float.NegativeInfinity;
        private float _staggerEndTime = float.NegativeInfinity;
        private float _immuneEndTime = float.NegativeInfinity;

        /// <param name="max">최대치 (일반 30 / 정예 100 / 보스 300)</param>
        /// <param name="recoverDelay">마지막 감소 후 회복 시작까지 (초)</param>
        /// <param name="recoverRatio">초당 회복 비율 (0.1 = 최대치의 10%)</param>
        /// <param name="immunityDuration">무력화가 끝난 뒤 다시 무력화되지 않는 시간 (초)</param>
        public PoiseGauge(float max, float recoverDelay = 3f, float recoverRatio = 0.1f, float immunityDuration = 10f)
        {
            Max = Mathf.Max(1f, max);
            Current = Max;
            _recoverDelay = Mathf.Max(0f, recoverDelay);
            _recoverRatio = Mathf.Max(0f, recoverRatio);
            _immunityDuration = Mathf.Max(0f, immunityDuration);
        }

        public float Max { get; }
        public float Current { get; private set; }
        public float Ratio => Current / Max;

        public bool IsStaggered(float now)
        {
            return now < _staggerEndTime;
        }

        /// <summary>무력화 중이거나 무력화 후 면역 시간이면 true (게이지가 깎이지 않음)</summary>
        public bool IsImmune(float now)
        {
            return now < _immuneEndTime;
        }

        /// <summary>게이지 감소. 이번 감소로 무력화되면 true</summary>
        /// <param name="staggerDuration">무력화 시간 (초)</param>
        public bool Reduce(float amount, float now, float staggerDuration)
        {
            if (amount <= 0f || IsImmune(now))
            {
                return false;
            }

            _lastReduceTime = now;
            Current = Mathf.Max(0f, Current - amount);

            if (Current > 0f)
            {
                return false;
            }

            // 무력화: 게이지 전량 회복, 무력화가 끝난 뒤부터 면역 시간
            Current = Max;
            _staggerEndTime = now + Mathf.Max(0f, staggerDuration);
            _immuneEndTime = _staggerEndTime + _immunityDuration;
            return true;
        }

        /// <summary>최대치 비율만큼 감소 (분쇄 일격 충만: 50%)</summary>
        public bool ReduceRatio(float ratio, float now, float staggerDuration)
        {
            return Reduce(Max * Mathf.Clamp01(ratio), now, staggerDuration);
        }

        /// <summary>무력화 즉시 종료 (처형). 면역 시간은 지금부터</summary>
        public void EndStagger(float now)
        {
            if (!IsStaggered(now))
            {
                return;
            }

            _staggerEndTime = now;
            _immuneEndTime = now + _immunityDuration;
        }

        /// <summary>자연 회복 (마지막 감소 후 recoverDelay가 지나야 시작)</summary>
        public void Tick(float now, float deltaTime)
        {
            if (Current >= Max || now - _lastReduceTime < _recoverDelay)
            {
                return;
            }

            Current = Mathf.Min(Max, Current + Max * _recoverRatio * deltaTime);
        }
    }
}
