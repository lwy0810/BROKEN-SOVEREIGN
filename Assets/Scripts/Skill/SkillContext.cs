using BrokenSovereign.Combat;
using BrokenSovereign.Core;
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

        /// <summary>충만 강화 시전 여부 (수치는 이미 강화 수정치 적용됨. 동작 추가가 필요한 기술만 확인)</summary>
        public bool IsEmpowered { get; }

        /// <summary>시전 후 경과 시간 (지속 기술용)</summary>
        public float Elapsed { get; set; }

        /// <summary>지속 기술의 전체 시간. 0보다 크면 HUD에 남은 시간 표시 (버프 등)</summary>
        public float Duration { get; set; }

        public float Remaining => Mathf.Max(0f, Duration - Elapsed);

        /// <summary>동작별 시전 상태 저장 (SkillBehaviour 에셋은 공유되므로 시전마다 여기에 보관)</summary>
        public object State { get; set; }

        /// <summary>시전 비용 자원 (마나). 지속 시전의 유지 비용 소모용. 없으면 null</summary>
        public ISkillResource Resource { get; set; }

        /// <summary>지속 시전: 마지막으로 키 유지가 확인된 시각 (Time.time)</summary>
        public float LastHeldTime { get; set; }

        /// <summary>지속 시전: 키를 뗐으면 true (다음 Tick에서 종료)</summary>
        public bool IsReleased { get; set; }

        public SkillContext(SkillDefinition definition, Transform caster, Vector3 cursorPoint,
            SkillParameterSet parameters, StatSheet stats, LayerMask targetMask, bool isEmpowered = false)
        {
            IsEmpowered = isEmpowered;
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
