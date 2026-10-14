using System.Collections.Generic;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 효과 ID별 종료 시각 관리 (순수 클래스). 같은 효과를 다시 걸면 중첩 없이 종료 시각만 갱신합니다.
    /// 사용: StatusEffectHost (전장의 함성 등 디버프)
    /// </summary>
    public class TimedEffectTable
    {
        private readonly Dictionary<string, float> _endTimes = new Dictionary<string, float>();
        private readonly List<string> _expired = new List<string>();

        public int Count => _endTimes.Count;

        /// <summary>효과 적용. 새로 걸렸으면 true, 이미 있어 갱신만 했으면 false</summary>
        public bool Apply(string id, float duration, float now)
        {
            bool isNew = !_endTimes.ContainsKey(id);
            float end = now + duration;

            // 남은 시간이 더 길면 유지 (짧은 효과로 덮어써 일찍 끝나지 않도록)
            if (isNew || end > _endTimes[id])
            {
                _endTimes[id] = end;
            }

            return isNew;
        }

        public bool IsActive(string id, float now)
        {
            return _endTimes.TryGetValue(id, out float end) && now < end;
        }

        public float Remaining(string id, float now)
        {
            return _endTimes.TryGetValue(id, out float end) ? System.Math.Max(0f, end - now) : 0f;
        }

        /// <summary>끝난 효과를 목록에서 제거하고 ID를 돌려줌 (돌려준 목록은 다음 호출까지만 유효)</summary>
        public IReadOnlyList<string> RemoveExpired(float now)
        {
            _expired.Clear();

            foreach (KeyValuePair<string, float> pair in _endTimes)
            {
                if (now >= pair.Value)
                {
                    _expired.Add(pair.Key);
                }
            }

            foreach (string id in _expired)
            {
                _endTimes.Remove(id);
            }

            return _expired;
        }

        public void Clear()
        {
            _endTimes.Clear();
        }
    }
}
