namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기절 시간 판정 (순수 클래스). 기절 중 다시 걸리면 더 늦게 끝나는 쪽으로 연장합니다 (중첩 합산 없음).
    /// </summary>
    public class StunTimer
    {
        private float _endTime = float.NegativeInfinity;

        public float EndTime => _endTime;

        /// <summary>기절 적용. 기존보다 늦게 끝나면 갱신하고 true</summary>
        public bool Apply(float duration, float now)
        {
            if (duration <= 0f)
            {
                return false;
            }

            float end = now + duration;

            if (end <= _endTime)
            {
                return false;
            }

            _endTime = end;
            return true;
        }

        public bool IsStunned(float now)
        {
            return now < _endTime;
        }

        public void Clear()
        {
            _endTime = float.NegativeInfinity;
        }
    }
}
