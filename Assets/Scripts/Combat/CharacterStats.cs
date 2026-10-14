using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// CharacterStatsData 에셋에서 기본 능력치를 받아 StatSheet를 만들고 보관합니다.
    /// Health·BasicAttack·ClickMover가 이 컴포넌트가 있으면 값을 여기서 읽습니다.
    /// Play 중 에셋 값을 바꾸면 즉시 반영됩니다.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [DisallowMultipleComponent]
    public class CharacterStats : MonoBehaviour
    {
        [Tooltip("기본 능력치 에셋 (Assets/Data/Balance)")]
        [SerializeField] private CharacterStatsData _data;

        private StatSheet _sheet;
        private CharacterStatsData _fallbackData;
        private CharacterStatsData _subscribedData;
        private DebugBuff _debugBuff;

        public StatSheet Sheet
        {
            get
            {
                if (_sheet == null)
                {
                    BuildSheet();
                }

                return _sheet;
            }
        }

        public CharacterStatsData Data => _data;

        private void Awake()
        {
            BuildSheet();
        }

        private void OnEnable()
        {
            Subscribe();
        }

        private void OnDisable()
        {
            Unsubscribe();
        }

        private void OnDestroy()
        {
            if (_fallbackData != null)
            {
                Destroy(_fallbackData);
            }
        }

        private void BuildSheet()
        {
            _sheet ??= new StatSheet();
            ApplyBaseValues();
        }

        private void ApplyBaseValues()
        {
            ResolveData().ApplyTo(_sheet);
        }

        private CharacterStatsData ResolveData()
        {
            if (_data != null)
            {
                return _data;
            }

            if (_fallbackData == null)
            {
                Debug.LogWarning($"{name}: CharacterStatsData가 비어 있어 기본값을 사용합니다.", this);
                _fallbackData = ScriptableObject.CreateInstance<CharacterStatsData>();
            }

            return _fallbackData;
        }

        private void Subscribe()
        {
            if (_subscribedData == _data)
            {
                return;
            }

            Unsubscribe();

            if (_data != null)
            {
                _data.Changed += OnDataChanged;
                _subscribedData = _data;
            }
        }

        private void Unsubscribe()
        {
            if (_subscribedData != null)
            {
                _subscribedData.Changed -= OnDataChanged;
                _subscribedData = null;
            }
        }

        private void OnDataChanged()
        {
            if (Application.isPlaying && _sheet != null)
            {
                ApplyBaseValues();
            }
        }

        // Play 중 에셋 교체 반영
        private void OnValidate()
        {
            if (Application.isPlaying && isActiveAndEnabled)
            {
                Subscribe();
                OnDataChanged();
            }
        }

        // ---- 디버그: 출처(버프) 추가·제거 확인용 ----

        [ContextMenu("Debug: 힘 +1000 버프 토글")]
        private void DebugToggleStrength()
        {
            ToggleDebugBuff(new StatModifier(StatType.Strength, ModifierType.Flat, 1000f));
        }

        [ContextMenu("Debug: 치명타 확률 +50% 버프 토글")]
        private void DebugToggleCrit()
        {
            ToggleDebugBuff(new StatModifier(StatType.CritChance, ModifierType.Flat, 0.5f));
        }

        [ContextMenu("Debug: 최대 체력 +50% 버프 토글")]
        private void DebugToggleMaxHealth()
        {
            ToggleDebugBuff(new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.5f));
        }

        [ContextMenu("Debug: 현재 최종 능력치 출력")]
        // 로그 1건에 전부 출력 (Console에서 클릭하면 전체 표시). 형식: 능력치 = 최종 (기본)
        public void DebugLogStats()
        {
            var builder = new System.Text.StringBuilder();
            string mainStat = Sheet.MainStat.HasValue ? Sheet.MainStat.Value.ToString() : "없음";
            builder.Append($"[{name}] 최종 능력치 — 주 스탯 {mainStat}, 피해 배율 {Sheet.MainStatDamageMultiplier:0.###}, ")
                .Append($"물리 피해 감소 {Sheet.GetMitigation(DamageType.Physical):P1}, ")
                .Append($"원소 피해 감소 {Sheet.GetMitigation(DamageType.Elemental):P1}");

            // 레벨·격 등 다른 컴포넌트 정보 (PlayerProgression 등)
            foreach (IDebugInfoProvider provider in GetComponents<IDebugInfoProvider>())
            {
                builder.Append('\n').Append(provider.DebugInfo);
            }

            foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            {
                builder.Append('\n').Append($"{stat} = {Sheet.Get(stat):0.###} (기본 {Sheet.GetBase(stat):0.###}");
                AppendSourceBreakdown(builder, stat);
                builder.Append(')');
            }

            Debug.Log(builder.ToString(), this);
        }

        // 출처별 기여: "| 레벨 +17, 격 +5, 충만 +25%" (Flat +값 / Percent +값% / Multiplicative ×(1−값))
        private void AppendSourceBreakdown(System.Text.StringBuilder builder, StatType stat)
        {
            bool first = true;

            foreach (IStatModifierSource source in Sheet.Sources)
            {
                float flat = 0f;
                float percent = 0f;
                float multiplier = 1f;
                bool found = false;

                foreach (StatModifier modifier in source.GetModifiers())
                {
                    if (modifier.Stat != stat)
                    {
                        continue;
                    }

                    found = true;

                    switch (modifier.Type)
                    {
                        case ModifierType.Flat:
                            flat += modifier.Value;
                            break;
                        case ModifierType.Percent:
                            percent += modifier.Value;
                            break;
                        case ModifierType.Multiplicative:
                            multiplier *= 1f - modifier.Value;
                            break;
                    }
                }

                if (!found)
                {
                    continue;
                }

                builder.Append(first ? " | " : ", ");
                first = false;
                builder.Append(source is INamedStatSource named ? named.SourceName : source.GetType().Name);

                if (!Mathf.Approximately(flat, 0f))
                {
                    builder.Append($" {flat:+0.###;-0.###}");
                }

                if (!Mathf.Approximately(percent, 0f))
                {
                    builder.Append($" {percent * 100f:+0.#;-0.#}%");
                }

                if (!Mathf.Approximately(multiplier, 1f))
                {
                    builder.Append($" ×{multiplier:0.###}");
                }
            }
        }

        private void ToggleDebugBuff(StatModifier modifier)
        {
            _debugBuff ??= new DebugBuff();

            if (!_debugBuff.Toggle(modifier))
            {
                Sheet.RemoveSource(_debugBuff);
            }
            else
            {
                Sheet.AddSource(_debugBuff);
                Sheet.NotifySourcesChanged();
            }

            Debug.Log($"{name}: 디버그 버프 {modifier.Stat} {(_debugBuff.Contains(modifier.Stat) ? "적용" : "해제")}");
        }

        private class DebugBuff : IStatModifierSource, INamedStatSource
        {
            public string SourceName => "디버그 버프";

            private readonly List<StatModifier> _modifiers = new List<StatModifier>();

            public IEnumerable<StatModifier> GetModifiers() => _modifiers;

            public bool Contains(StatType stat) => _modifiers.Exists(m => m.Stat == stat);

            /// <summary>같은 능력치가 있으면 제거, 없으면 추가. 남은 수정치가 있으면 true</summary>
            public bool Toggle(StatModifier modifier)
            {
                int index = _modifiers.FindIndex(m => m.Stat == modifier.Stat);

                if (index >= 0)
                {
                    _modifiers.RemoveAt(index);
                }
                else
                {
                    _modifiers.Add(modifier);
                }

                return _modifiers.Count > 0;
            }
        }
    }
}
