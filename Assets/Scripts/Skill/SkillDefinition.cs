using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 정의 데이터. 수치는 키-값 목록(SkillParameterKeys)으로, 동작은 SkillBehaviour 에셋으로 지정합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "SkillDefinition", menuName = "BrokenSovereign/Skill Definition")]
    public class SkillDefinition : ScriptableObject
    {
        [Serializable]
        public struct SkillParameter
        {
            [Tooltip("SkillParameterKeys 이름 (예: coefficient, radius, angle)")]
            public string Key;
            public float Value;

            public SkillParameter(string key, float value)
            {
                Key = key;
                Value = value;
            }
        }

        [Serializable]
        public struct SkillModifierEntry
        {
            [Tooltip("SkillParameterKeys 이름")]
            public string Key;

            [Tooltip("Flat: 더하기 / Percent: (1 + 값) 곱하기 / Multiplicative: 값 곱하기")]
            public ModifierType Type;

            public float Value;
        }

        [Tooltip("기술 고유 ID (재사용 대기·분기 저장 키)")]
        [SerializeField] private string _skillId = "skill";
        [SerializeField] private string _displayName = "기술";

        [Tooltip("재사용 대기(초). 재사용 대기 감소 능력치 적용 전 값")]
        [SerializeField, Min(0f)] private float _cooldown;

        [Tooltip("직업 자원 소모량 (신앙 등). 무직 공용 기술은 0. 자원 연결은 다음 단계")]
        [SerializeField, Min(0f)] private float _resourceCost;

        [SerializeField] private List<SkillParameter> _parameters = new List<SkillParameter>
        {
            new SkillParameter(SkillParameterKeys.COEFFICIENT, 1f)
        };

        [Tooltip("켜면 기술이 진행되는 동안 이동·다른 행동 잠금 (돌진 등). 회피는 가능")]
        [SerializeField] private bool _locksMovement;

        [Tooltip("기술 키 유지 시 이동 방식. StopInRange: 사거리 안이면 멈춰서 시전 / MoveWhileCasting: 이동하며 시전 (휠윈드)")]
        [SerializeField] private SkillCastMovement _castMovement = SkillCastMovement.StopInRange;

        [Tooltip("시전 동작 시간(초). 이 시간 동안 시전자는 멈춰 있고 이동·다른 공격 불가 (회피는 가능). MoveWhileCasting 기술은 적용 안 함")]
        [SerializeField, Min(0f)] private float _castTime = 0.4f;

        [Tooltip("신앙 충만 시 적용할 수치 수정치")]
        [SerializeField] private List<SkillModifierEntry> _empoweredModifiers = new List<SkillModifierEntry>();

        [SerializeField] private SkillBehaviour _behaviour;

        public string SkillId => _skillId;
        public string DisplayName => _displayName;
        public float Cooldown => _cooldown;
        public float ResourceCost => _resourceCost;
        public SkillBehaviour Behaviour => _behaviour;
        public bool LocksMovement => _locksMovement;
        public SkillCastMovement CastMovement => _castMovement;
        public float CastTime => _castTime;

        /// <summary>에셋에 적힌 기본 수치 하나 (분기·충만 적용 전). 없으면 fallback</summary>
        public float GetBaseParameter(string key, float fallback = 0f)
        {
            foreach (SkillParameter parameter in _parameters)
            {
                if (parameter.Key == key)
                {
                    return parameter.Value;
                }
            }

            return fallback;
        }

        /// <summary>시전마다 새 수치 목록 생성 (분기·충만 수정치가 에셋을 바꾸지 않도록)</summary>
        public SkillParameterSet CreateParameters(bool empowered = false)
        {
            var set = new SkillParameterSet();

            foreach (SkillParameter parameter in _parameters)
            {
                if (!string.IsNullOrEmpty(parameter.Key))
                {
                    set.Set(parameter.Key, parameter.Value);
                }
            }

            if (empowered)
            {
                foreach (SkillModifierEntry entry in _empoweredModifiers)
                {
                    if (!string.IsNullOrEmpty(entry.Key))
                    {
                        set.Apply(new SkillParameterModifier(entry.Key, entry.Type, entry.Value));
                    }
                }
            }

            return set;
        }
    }
}
