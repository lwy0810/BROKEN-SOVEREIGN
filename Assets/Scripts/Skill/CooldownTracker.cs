using System.Collections.Generic;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술별 재사용 대기 (순수 클래스). 매 프레임 Tick으로 시간을 줄입니다.
    /// </summary>
    public class CooldownTracker
    {
        private struct Entry
        {
            public float Remaining;
            public float Duration;
        }

        private readonly Dictionary<string, Entry> _entries = new Dictionary<string, Entry>();
        private readonly List<string> _keyBuffer = new List<string>();

        public void Start(string skillId, float seconds)
        {
            if (seconds <= 0f)
            {
                _entries.Remove(skillId);
                return;
            }

            _entries[skillId] = new Entry { Remaining = seconds, Duration = seconds };
        }

        public void Tick(float deltaTime)
        {
            if (_entries.Count == 0)
            {
                return;
            }

            _keyBuffer.Clear();
            _keyBuffer.AddRange(_entries.Keys);

            foreach (string key in _keyBuffer)
            {
                Entry entry = _entries[key];
                entry.Remaining -= deltaTime;

                if (entry.Remaining <= 0f)
                {
                    _entries.Remove(key);
                }
                else
                {
                    _entries[key] = entry;
                }
            }
        }

        public bool IsReady(string skillId)
        {
            return !_entries.ContainsKey(skillId);
        }

        public float Remaining(string skillId)
        {
            return _entries.TryGetValue(skillId, out Entry entry) ? entry.Remaining : 0f;
        }

        /// <summary>마지막으로 시작한 재사용 대기 전체 시간 (HUD 비율 표시용)</summary>
        public float Duration(string skillId)
        {
            return _entries.TryGetValue(skillId, out Entry entry) ? entry.Duration : 0f;
        }
    }
}
