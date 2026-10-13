using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 신앙 게이지 규칙 (순수 클래스).
    /// 적중·피격으로 충전, 비전투 시 감소. 가득 차면 버프 발동:
    /// 버프 동안 충전·감소 없이 게이지가 남은 시간 비율로 줄어들고, 끝나면 0.
    /// </summary>
    public class FaithModel
    {
        private readonly float _decayPerSecond;
        private float _buffDuration;
        private float _buffRemaining;

        public float Current { get; private set; }
        public float Max { get; private set; }

        public bool IsBuffActive => _buffRemaining > 0f;
        public float BuffRemaining => _buffRemaining;

        public FaithModel(float max, float decayPerSecond, float buffDuration)
        {
            Max = Mathf.Max(1f, max);
            _decayPerSecond = Mathf.Max(0f, decayPerSecond);
            _buffDuration = Mathf.Max(0.1f, buffDuration);
        }

        /// <summary>충전. 이번 충전으로 가득 차서 버프가 시작되면 true</summary>
        public bool Gain(float amount)
        {
            if (IsBuffActive || amount <= 0f)
            {
                return false;
            }

            Current = Mathf.Min(Max, Current + amount);

            if (Current < Max)
            {
                return false;
            }

            _buffRemaining = _buffDuration;
            return true;
        }

        /// <summary>시간 경과. 이번 Tick에 버프가 끝나면 true</summary>
        public bool Tick(float deltaTime, bool inCombat)
        {
            if (IsBuffActive)
            {
                _buffRemaining = Mathf.Max(0f, _buffRemaining - deltaTime);
                Current = Max * (_buffRemaining / _buffDuration);

                if (_buffRemaining > 0f)
                {
                    return false;
                }

                Current = 0f;
                return true;
            }

            if (!inCombat && Current > 0f)
            {
                Current = Mathf.Max(0f, Current - _decayPerSecond * deltaTime);
            }

            return false;
        }

        public void SetBuffDuration(float duration)
        {
            _buffDuration = Mathf.Max(0.1f, duration);
        }

        public void SetMax(float max)
        {
            Max = Mathf.Max(1f, max);
            Current = Mathf.Min(Current, Max);
        }

        /// <summary>게이지·버프 초기화 (직업 전환 시)</summary>
        public void Reset()
        {
            Current = 0f;
            _buffRemaining = 0f;
        }
    }
}
