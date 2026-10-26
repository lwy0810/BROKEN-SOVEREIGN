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

        [Tooltip("재사용 대기가 0인 즉시 발동 기술의 반복 간격 (초, 공격 속도 0% 기준). 0이면 SkillCaster 기본값 (기본 공격 간격)")]
        [SerializeField, Min(0f)] private float _attackInterval;

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

        [Tooltip("켜면 지속 시전(채널): 키를 누르고 있는 동안 진행, 떼거나 자원이 부족하면 종료 (난천무). 즉시 발동 간격·재사용 대기 미사용")]
        [SerializeField] private bool _channeled;

        [Tooltip("시전 모션 Animator 상태 이름. 비우면 \"Skill\" + 기술 ID 파스칼 표기 (radiant_smite → SkillRadiantSmite). " +
            "상태가 없으면 기본 공격 모션")]
        [SerializeField] private string _animationState;

        [Tooltip("신앙 충만 시 적용할 수치 수정치")]
        [SerializeField] private List<SkillModifierEntry> _empoweredModifiers = new List<SkillModifierEntry>();

        [SerializeField] private SkillBehaviour _behaviour;

        [Header("이펙트 (10/26, GameEffects가 재생)")]
        [Tooltip("시전 이펙트 프리팹 (비우면 없음)")]
        [SerializeField] private GameObject _castEffect;
        [Tooltip("시전 이펙트 위치: Player = 시전자 발밑, Target = 커서·대상 지점, Follow = 시전자를 따라다님")]
        [SerializeField] private SkillEffectAnchor _castEffectAnchor = SkillEffectAnchor.Player;
        [SerializeField, Min(0.05f)] private float _castEffectScale = 1f;
        [Tooltip("Follow 이펙트 유지: Natural = 이펙트 자체 길이, SkillDuration = 수치 duration(초), Channel = 지속 시전 동안")]
        [SerializeField] private SkillEffectLifetime _castEffectLifetime = SkillEffectLifetime.Natural;
        [Tooltip("시전 이펙트에 함께 재생할 보조 이펙트 (같은 위치, 비우면 없음)")]
        [SerializeField] private GameObject _castEffectExtra;
        [Tooltip("이 기술로 적을 맞혔을 때의 타격 이펙트 (비우면 기본 타격 이펙트)")]
        [SerializeField] private GameObject _hitEffect;
        [SerializeField, Min(0.05f)] private float _hitEffectScale = 1f;
        [Tooltip("타격 이펙트에 함께 재생할 보조 이펙트 (비우면 없음)")]
        [SerializeField] private GameObject _hitEffectExtra;

        public string SkillId => _skillId;
        public string DisplayName => _displayName;
        public float Cooldown => _cooldown;
        public float AttackInterval => _attackInterval;
        public float ResourceCost => _resourceCost;
        public SkillBehaviour Behaviour => _behaviour;
        public bool LocksMovement => _locksMovement;
        public SkillCastMovement CastMovement => _castMovement;
        public float CastTime => _castTime;
        public bool IsChanneled => _channeled;
        public GameObject CastEffect => _castEffect;
        public GameObject CastEffectExtra => _castEffectExtra;
        public SkillEffectAnchor CastEffectAnchor => _castEffectAnchor;
        public float CastEffectScale => _castEffectScale;
        public SkillEffectLifetime CastEffectLifetime => _castEffectLifetime;
        public GameObject HitEffect => _hitEffect;
        public GameObject HitEffectExtra => _hitEffectExtra;
        public float HitEffectScale => _hitEffectScale;

#if UNITY_EDITOR
        /// <summary>에디터 메뉴(이펙트 연결)에서 사용</summary>
        public void EditorSetEffects(GameObject cast, GameObject castExtra, SkillEffectAnchor anchor, float castScale,
            SkillEffectLifetime lifetime, GameObject hit, GameObject hitExtra, float hitScale)
        {
            _castEffect = cast;
            _castEffectExtra = castExtra;
            _castEffectAnchor = anchor;
            _castEffectScale = castScale;
            _castEffectLifetime = lifetime;
            _hitEffect = hit;
            _hitEffectExtra = hitExtra;
            _hitEffectScale = hitScale;
        }
#endif

        /// <summary>시전 모션 Animator 상태 이름 (CharacterSetupTools가 같은 규칙으로 상태 생성)</summary>
        public string AnimationState => string.IsNullOrEmpty(_animationState) ? ToAnimationState(_skillId) : _animationState;

        /// <summary>기술 ID → 상태 이름: radiant_smite → SkillRadiantSmite</summary>
        public static string ToAnimationState(string skillId)
        {
            var builder = new System.Text.StringBuilder("Skill");

            foreach (string part in (skillId ?? string.Empty).Split('_'))
            {
                if (part.Length > 0)
                {
                    builder.Append(char.ToUpperInvariant(part[0])).Append(part, 1, part.Length - 1);
                }
            }

            return builder.ToString();
        }

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

#if UNITY_EDITOR
        /// <summary>에디터 도구에서 기술 에셋을 만들 때 사용 (HeroSetupTools)</summary>
        public void EditorSetup(string skillId, string displayName, float cooldown, float resourceCost,
            SkillCastMovement castMovement, float castTime, bool channeled, SkillParameter[] parameters,
            SkillModifierEntry[] empoweredModifiers, SkillBehaviour behaviour)
        {
            _skillId = skillId;
            _displayName = displayName;
            _cooldown = cooldown;
            _attackInterval = 0f;
            _resourceCost = resourceCost;
            _castMovement = castMovement;
            _castTime = castTime;
            _channeled = channeled;
            _locksMovement = false;
            _parameters = new List<SkillParameter>(parameters);
            _empoweredModifiers = new List<SkillModifierEntry>(empoweredModifiers);
            _behaviour = behaviour;
        }
#endif

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

    /// <summary>시전 이펙트 위치 (10/26)</summary>
    public enum SkillEffectAnchor
    {
        Player,
        Target,
        Follow,
    }

    /// <summary>따라다니는 시전 이펙트 유지 시간 (10/26)</summary>
    public enum SkillEffectLifetime
    {
        Natural,
        SkillDuration,
        Channel,
    }
}
