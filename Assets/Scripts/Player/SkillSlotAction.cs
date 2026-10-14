using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 기술 슬롯을 교전 행동(ITargetedAction)으로 감쌉니다.
    /// 적을 대상으로 기술을 쓸 때 기본 공격과 같은 흐름(접근 → 사거리 안 정지 → 실행)을 사용하기 위한 어댑터입니다.
    /// </summary>
    public class SkillSlotAction : ITargetedAction
    {
        private readonly SkillCaster _caster;
        private readonly Func<SkillSlot, float> _getReach;

        public SkillSlotAction(SkillCaster caster, SkillSlot slot, Func<SkillSlot, float> getReach)
        {
            _caster = caster;
            Slot = slot;
            _getReach = getReach;
        }

        public SkillSlot Slot { get; }

        /// <summary>접근 사거리 (PlayerController 기준: 기술 반경 × 0.8, 반경이 없으면 기본 공격 사거리)</summary>
        public float Range => _getReach != null ? _getReach(Slot) : 0f;

        /// <summary>시전 동작 시간</summary>
        public float Duration => _caster != null ? _caster.GetCastTime(Slot) : 0f;

        public bool TryExecute(Transform attacker, Transform target, Vector3 point)
        {
            if (_caster == null)
            {
                return false;
            }

            Vector3 castPoint = target != null ? target.position : point;
            return _caster.TryCast(Slot, castPoint);
        }
    }
}
