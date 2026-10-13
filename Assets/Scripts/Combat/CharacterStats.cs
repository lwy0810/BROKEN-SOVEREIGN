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
        private void DebugLogStats()
        {
            foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            {
                Debug.Log($"{name} {stat}: {Sheet.Get(stat):0.###}");
            }

            Debug.Log($"{name} 주 스탯 피해 배율: {Sheet.MainStatDamageMultiplier:0.###}, "
                + $"물리 피해 감소: {Sheet.GetMitigation(DamageType.Physical):P1}, "
                + $"원소 피해 감소: {Sheet.GetMitigation(DamageType.Elemental):P1}");
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

        private class DebugBuff : IStatModifierSource
        {
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
