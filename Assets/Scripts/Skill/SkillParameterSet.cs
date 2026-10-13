using System.Collections.Generic;
using BrokenSovereign.Core;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 수치 목록 (키-값). 시전할 때마다 SkillDefinition에서 새로 만들고,
    /// 강화 분기·충만 수정치를 적용한 뒤 SkillBehaviour에 전달합니다.
    /// </summary>
    public class SkillParameterSet
    {
        private readonly Dictionary<string, float> _values = new Dictionary<string, float>();

        public IEnumerable<string> Keys => _values.Keys;

        public bool Has(string key)
        {
            return _values.ContainsKey(key);
        }

        /// <summary>값이 없으면 fallback을 돌려줍니다.</summary>
        public float Get(string key, float fallback = 0f)
        {
            return _values.TryGetValue(key, out float value) ? value : fallback;
        }

        public void Set(string key, float value)
        {
            _values[key] = value;
        }

        /// <summary>수정치 적용. 없는 키에 Flat이면 0에서 시작, Percent·Multiplicative는 무시</summary>
        public void Apply(SkillParameterModifier modifier)
        {
            bool exists = _values.TryGetValue(modifier.Key, out float current);

            switch (modifier.Type)
            {
                case ModifierType.Flat:
                    _values[modifier.Key] = current + modifier.Value;
                    break;
                case ModifierType.Percent:
                    if (exists)
                    {
                        _values[modifier.Key] = current * (1f + modifier.Value);
                    }

                    break;
                case ModifierType.Multiplicative:
                    if (exists)
                    {
                        _values[modifier.Key] = current * modifier.Value;
                    }

                    break;
            }
        }
    }
}
