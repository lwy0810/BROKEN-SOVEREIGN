using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 시전 1회의 정보. SkillCaster가 시전마다 만들어 SkillBehaviour에 전달합니다.
    /// </summary>
    public class SkillContext
    {
        public SkillDefinition Definition { get; }
        public Transform Caster { get; }

        /// <summary>시전 시점 커서 지점 (바닥)</summary>
        public Vector3 CursorPoint { get; }

        /// <summary>분기·충만 수정치까지 적용된 최종 수치</summary>
        public SkillParameterSet Parameters { get; }

        /// <summary>시전자 능력치. 없으면 null</summary>
        public StatSheet Stats { get; }

        /// <summary>피해 대상 레이어 (Enemy)</summary>
        public LayerMask TargetMask { get; }

        /// <summary>시전 후 경과 시간 (지속 기술용)</summary>
        public float Elapsed { get; set; }

        public SkillContext(SkillDefinition definition, Transform caster, Vector3 cursorPoint,
            SkillParameterSet parameters, StatSheet stats, LayerMask targetMask)
        {
            Definition = definition;
            Caster = caster;
            CursorPoint = cursorPoint;
            Parameters = parameters;
            Stats = stats;
            TargetMask = targetMask;
        }

        /// <summary>시전자 → 커서 수평 방향. 겹치면 시전자 정면</summary>
        public Vector3 AimDirection
        {
            get
            {
                Vector3 direction = CursorPoint - Caster.position;
                direction.y = 0f;
                return direction.sqrMagnitude > 0.0001f ? direction.normalized : Caster.forward;
            }
        }
    }
}
