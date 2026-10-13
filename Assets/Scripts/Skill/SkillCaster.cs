using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 슬롯에 연결된 기술을 시전합니다: 재사용 대기 확인 → 수치 생성 → SkillBehaviour 실행 → 재사용 대기 시작.
    /// 지속 기술은 끝날 때까지 매 프레임 Tick을 호출합니다.
    /// 슬롯 연결은 임시로 Inspector 목록을 사용하며, SkillLoadout(후보 장착·해금) 구현 시 교체합니다.
    /// 자원 소모·강화 분기·충만은 다음 단계에서 연결합니다.
    /// </summary>
    public class SkillCaster : MonoBehaviour
    {
        [Serializable]
        private struct SkillSlotBinding
        {
            public SkillSlot Slot;
            public SkillDefinition Skill;
        }

        [SerializeField] private List<SkillSlotBinding> _bindings = new List<SkillSlotBinding>();

        [Tooltip("기술 피해 대상 레이어 (Enemy)")]
        [SerializeField] private LayerMask _targetMask;

        private readonly CooldownTracker _cooldowns = new CooldownTracker();
        private readonly List<SkillContext> _activeCasts = new List<SkillContext>();
        private CharacterStats _stats;

        /// <summary>기술을 시전했을 때 (슬롯, 기술)</summary>
        public event Action<SkillSlot, SkillDefinition> SkillCast;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
        }

        private void Update()
        {
            _cooldowns.Tick(Time.deltaTime);
            TickActiveCasts(Time.deltaTime);
        }

        public bool HasSkill(SkillSlot slot)
        {
            return GetSkill(slot) != null;
        }

        public SkillDefinition GetSkill(SkillSlot slot)
        {
            foreach (SkillSlotBinding binding in _bindings)
            {
                if (binding.Slot == slot && binding.Skill != null)
                {
                    return binding.Skill;
                }
            }

            return null;
        }

        /// <summary>대상 지정 시 접근할 사거리 (기술 반경). 기술이 없거나 반경이 없으면 0</summary>
        public float GetRange(SkillSlot slot)
        {
            SkillDefinition skill = GetSkill(slot);
            return skill != null ? skill.GetBaseParameter(SkillParameterKeys.RADIUS) : 0f;
        }

        public bool TryCast(SkillSlot slot, Vector3 cursorPoint)
        {
            SkillDefinition skill = GetSkill(slot);

            if (skill == null || !_cooldowns.IsReady(skill.SkillId))
            {
                return false;
            }

            if (skill.Behaviour == null)
            {
                Debug.LogWarning($"SkillCaster: {skill.DisplayName}에 SkillBehaviour가 없습니다.", skill);
                return false;
            }

            StatSheet stats = _stats != null ? _stats.Sheet : null;
            var context = new SkillContext(skill, transform, cursorPoint, skill.CreateParameters(), stats, _targetMask);

            skill.Behaviour.Begin(context);
            _activeCasts.Add(context);
            _cooldowns.Start(skill.SkillId, GetCooldown(skill, stats));
            SkillCast?.Invoke(slot, skill);
            return true;
        }

        /// <summary>HUD 표시용. 기술이 없는 슬롯은 미개방</summary>
        public SkillSlotState GetSlotState(SkillSlot slot)
        {
            SkillDefinition skill = GetSkill(slot);

            if (skill == null)
            {
                return SkillSlotState.Locked;
            }

            return new SkillSlotState(true, _cooldowns.Remaining(skill.SkillId), _cooldowns.Duration(skill.SkillId));
        }

        // 재사용 대기 감소 능력치 적용 (최대 90%)
        private static float GetCooldown(SkillDefinition skill, StatSheet stats)
        {
            float reduction = stats != null ? Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.9f) : 0f;
            return skill.Cooldown * (1f - reduction);
        }

        private void TickActiveCasts(float deltaTime)
        {
            for (int i = _activeCasts.Count - 1; i >= 0; i--)
            {
                SkillContext context = _activeCasts[i];
                context.Elapsed += deltaTime;

                if (context.Definition.Behaviour.Tick(context, deltaTime))
                {
                    continue;
                }

                context.Definition.Behaviour.End(context);
                _activeCasts.RemoveAt(i);
            }
        }
    }
}
