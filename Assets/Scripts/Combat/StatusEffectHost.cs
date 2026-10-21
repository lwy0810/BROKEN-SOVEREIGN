using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 대상에 걸린 시간제 능력치 효과(디버프·버프) 관리. 효과마다 능력치 출처를 StatSheet에 등록하고, 끝나면 제거합니다.
    /// 같은 효과 ID를 다시 걸면 중첩 없이 수치·종료 시각만 갱신합니다.
    /// 처음 효과가 걸릴 때 대상에 자동으로 붙으므로 씬·프리팹에 미리 추가하지 않아도 됩니다 (GetOrAdd).
    /// </summary>
    [RequireComponent(typeof(CharacterStats))]
    public class StatusEffectHost : MonoBehaviour
    {
        private class EffectSource : IStatModifierSource, INamedStatSource
        {
            public readonly List<StatModifier> Modifiers = new List<StatModifier>();
            public string Name;

            public string SourceName => Name;

            public IEnumerable<StatModifier> GetModifiers() => Modifiers;
        }

        private readonly TimedEffectTable _table = new TimedEffectTable();
        private readonly Dictionary<string, EffectSource> _sources = new Dictionary<string, EffectSource>();
        private CharacterStats _stats;
        private Health _health;

        /// <summary>효과가 새로 걸리거나 갱신됐을 때 (대상, 표시 이름). 디버그 표시용</summary>
        public static event Action<StatusEffectHost, string> AnyApplied;

        public int Count => _sources.Count;

        /// <summary>대상의 효과 관리자. 없으면 붙임 (CharacterStats가 없으면 null)</summary>
        public static StatusEffectHost GetOrAdd(GameObject target)
        {
            if (target == null || !target.TryGetComponent(out CharacterStats _))
            {
                return null;
            }

            return target.TryGetComponent(out StatusEffectHost host) ? host : target.AddComponent<StatusEffectHost>();
        }

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            if (_health != null)
            {
                _health.Died += ClearAll;
                _health.Restored += ClearAll;
            }
        }

        private void OnDisable()
        {
            if (_health != null)
            {
                _health.Died -= ClearAll;
                _health.Restored -= ClearAll;
            }
        }

        /// <summary>효과 적용 (id가 같으면 갱신). 수치는 새 값으로 교체</summary>
        public void Apply(string id, string displayName, IEnumerable<StatModifier> modifiers, float duration)
        {
            if (string.IsNullOrEmpty(id) || duration <= 0f || (_health != null && _health.IsDead))
            {
                return;
            }

            bool isNew = _table.Apply(id, duration, Time.time);

            if (!_sources.TryGetValue(id, out EffectSource source))
            {
                source = new EffectSource();
                _sources[id] = source;
            }

            source.Name = displayName;
            source.Modifiers.Clear();
            source.Modifiers.AddRange(modifiers);

            if (isNew)
            {
                _stats.Sheet.AddSource(source);
            }
            else
            {
                // 수치가 바뀌었을 수 있으므로 다시 등록해 능력치 갱신
                _stats.Sheet.RemoveSource(source);
                _stats.Sheet.AddSource(source);
            }

            AnyApplied?.Invoke(this, displayName);
        }

        public bool Has(string id)
        {
            return _table.IsActive(id, Time.time);
        }

        /// <summary>걸려 있는 효과 이름 목록 (머리 위 표시용)</summary>
        public void GetActiveNames(List<string> results)
        {
            results.Clear();

            foreach (EffectSource source in _sources.Values)
            {
                results.Add(source.Name);
            }
        }

        /// <summary>걸려 있는 효과 "이름 남은 초" 목록 (디버그 표시용)</summary>
        public void GetActiveLabels(List<string> results)
        {
            results.Clear();

            foreach (KeyValuePair<string, EffectSource> pair in _sources)
            {
                results.Add($"{pair.Value.Name} {Mathf.CeilToInt(_table.Remaining(pair.Key, Time.time))}초");
            }
        }

        private void Update()
        {
            if (_sources.Count == 0)
            {
                return;
            }

            foreach (string id in _table.RemoveExpired(Time.time))
            {
                Remove(id);
            }
        }

        private void Remove(string id)
        {
            if (_sources.TryGetValue(id, out EffectSource source))
            {
                _stats.Sheet.RemoveSource(source);
                _sources.Remove(id);
            }
        }

        private void ClearAll()
        {
            foreach (EffectSource source in _sources.Values)
            {
                _stats.Sheet.RemoveSource(source);
            }

            _sources.Clear();
            _table.Clear();
        }
    }
}
