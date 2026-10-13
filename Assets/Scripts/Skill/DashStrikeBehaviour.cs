using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 커서 방향 돌진. 이동한 구간을 매 프레임 캡슐로 쓸어 검사해 경로의 적마다 1회 피해 + 기절 (지속 기술).
    /// NavMesh 위에서만 이동하므로 벽에 막히면 그 자리에서 멈춥니다.
    /// 수치 키: coefficient, distance(m), duration(초), hitRadius(m), stunDuration(초)
    /// 사용 기술: 진격의 맹세
    /// </summary>
    [CreateAssetMenu(fileName = "DashStrikeBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Dash Strike")]
    public class DashStrikeBehaviour : SkillBehaviour
    {
        private const float FALLBACK_ATTACK_POWER = 100f;

        // 벽에 막혀 거의 움직이지 못하면 돌진 종료
        private const float BLOCKED_STEP_RATIO = 0.1f;

        private static readonly Collider[] _buffer = new Collider[32];

        [Tooltip("판정 캡슐 높이 오프셋 (시전자 위치 기준)")]
        [SerializeField] private float _hitHeightOffset = 0f;

        private class DashState
        {
            public NavMeshAgent Agent;
            public Vector3 Direction;
            public float Speed;
            public float Distance;
            public float Traveled;
            public readonly HashSet<IDamageable> Hit = new HashSet<IDamageable>();
        }

        public override void Begin(SkillContext context)
        {
            SkillParameterSet parameters = context.Parameters;
            float distance = parameters.Get(SkillParameterKeys.DISTANCE, 5f);
            float duration = Mathf.Max(0.05f, parameters.Get(SkillParameterKeys.DURATION, 0.25f));

            var state = new DashState
            {
                Agent = context.Caster.GetComponent<NavMeshAgent>(),
                Direction = context.AimDirection,
                Distance = distance,
                Speed = distance / duration
            };

            if (state.Agent != null && state.Agent.isOnNavMesh)
            {
                state.Agent.ResetPath();
            }

            context.Caster.rotation = Quaternion.LookRotation(state.Direction);
            context.State = state;

            // 출발 지점에 붙어 있는 적도 판정
            HitAlongPath(context, state, context.Caster.position, context.Caster.position);
        }

        public override bool Tick(SkillContext context, float deltaTime)
        {
            if (!(context.State is DashState state))
            {
                return false;
            }

            float step = Mathf.Min(state.Speed * deltaTime, state.Distance - state.Traveled);

            if (step <= 0f)
            {
                return false;
            }

            Vector3 from = context.Caster.position;
            Move(context.Caster, state, state.Direction * step);
            Vector3 to = context.Caster.position;

            HitAlongPath(context, state, from, to);

            float moved = Vector3.Distance(Flat(from), Flat(to));
            state.Traveled += step;

            bool blocked = moved < step * BLOCKED_STEP_RATIO;
            return !blocked && state.Traveled < state.Distance;
        }

        private static void Move(Transform caster, DashState state, Vector3 delta)
        {
            if (state.Agent != null && state.Agent.isOnNavMesh)
            {
                state.Agent.Move(delta);
            }
            else
            {
                caster.position += delta;
            }
        }

        private void HitAlongPath(SkillContext context, DashState state, Vector3 from, Vector3 to)
        {
            float radius = context.Parameters.Get(SkillParameterKeys.HIT_RADIUS, 1f);
            float coefficient = context.Parameters.Get(SkillParameterKeys.COEFFICIENT, 1.2f);
            float stunDuration = context.Parameters.Get(SkillParameterKeys.STUN_DURATION, 0.5f);
            Vector3 offset = Vector3.up * _hitHeightOffset;

            int count = Physics.OverlapCapsuleNonAlloc(from + offset, to + offset, radius, _buffer,
                context.TargetMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                IDamageable target = _buffer[i].GetComponentInParent<IDamageable>();

                if (target == null || target.IsDead || !state.Hit.Add(target))
                {
                    continue;
                }

                DamageInfo info = AttackDamage.Build(context.Caster.gameObject, context.Stats, coefficient,
                    FALLBACK_ATTACK_POWER, false);
                target.ApplyDamage(info);

                if (_buffer[i].GetComponentInParent<IStunnable>() is IStunnable stunnable)
                {
                    stunnable.Stun(stunDuration);
                }
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
