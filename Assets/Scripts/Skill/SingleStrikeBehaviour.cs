using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 단일 대상 타격 (즉발). 커서 지점의 적을 우선 노리고, 없으면 커서 방향 부채꼴에서 가장 가까운 적 1명.
    /// 수치 키: coefficient(계수), radius(타격 거리 m), angle(대상 탐색 각도)
    /// 확산(우클릭 강화 분기 A, 10/26): spreadCount·spreadRadius·spreadTotal이 있으면 대상 주변 적도 함께 맞히고
    ///   총 피해(spreadTotal배)를 맞은 수로 나눔 (한 명당 최대 100%). 혼자면 기본 피해 그대로
    /// 타격 시점 (10/26): 일반 공격과 같이 모션의 타격 순간(IAttackImpactTiming.SkillImpactDelay)에
    ///   피해·타격 이펙트·효과음(ImpactLanded)이 함께 나옴. 대상은 시전할 때 정함
    /// 사용 기술: 강타
    /// </summary>
    [CreateAssetMenu(fileName = "SingleStrikeBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Single Strike")]
    public class SingleStrikeBehaviour : SkillBehaviour
    {
        protected const float FALLBACK_ATTACK_POWER = 100f;

        [Tooltip("커서 지점에서 이 반경 안의 적을 우선 대상으로 선택 (m)")]
        [SerializeField] private float _cursorPickRadius = 1f;

        [Tooltip("타격 거리 여유 (대상 충돌체 크기 보정, m)")]
        [SerializeField] private float _reachTolerance = 0.5f;

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showArea = true;
        [SerializeField] private Color _areaColor = new Color(1f, 0.3f, 0.2f, 0.45f);
        [SerializeField] private float _areaDuration = 0.2f;
        [SerializeField] private float _hitMarkRadius = 0.8f;

        private readonly List<IDamageable> _candidates = new List<IDamageable>();
        private readonly List<IDamageable> _spread = new List<IDamageable>();

        [Tooltip("공격자에게 IAttackImpactTiming이 없을 때 타격까지 시간 (초)")]
        [SerializeField, Min(0f)] private float _fallbackImpactDelay = 0.3f;

        public override bool DelaysImpact => true;

        public override void Begin(SkillContext context)
        {
            SkillParameterSet parameters = context.Parameters;
            float radius = parameters.Get(SkillParameterKeys.RADIUS, 2.5f);
            float angle = parameters.Get(SkillParameterKeys.ANGLE, 120f);

            Vector3 origin = context.Caster.position;
            IDamageable target = FindTarget(context, origin, radius, angle, out Vector3 targetPosition);

            if (target == null)
            {
                Debug.Log($"{context.Definition.DisplayName}: 대상 없음");
            }

            // 일반 공격과 같은 방식: 모션의 타격 순간에 피해 (그 사이 대상이 죽거나 사라지면 효과음만)
            float delay = context.Caster.TryGetComponent(out IAttackImpactTiming timing)
                ? timing.SkillImpactDelay
                : _fallbackImpactDelay;
            AttackImpactRunner.For(context.Caster).ScheduleSkill(delay, () => Impact(context, target, targetPosition));
        }

        private void Impact(SkillContext context, IDamageable target, Vector3 targetPosition)
        {
            if (context.Caster == null)
            {
                return;
            }

            bool alive = target != null && target is Component component && component != null && !target.IsDead;

            if (alive)
            {
                targetPosition = ((Component)target).transform.position;
            }

            RaiseImpact(context.Definition, alive ? targetPosition : context.Caster.position);

            if (!alive)
            {
                return;
            }

            SkillParameterSet parameters = context.Parameters;
            float coefficient = parameters.Get(SkillParameterKeys.COEFFICIENT, 1.5f);

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCircle(targetPosition, _hitMarkRadius, context.CursorPoint.y, _areaColor,
                    _areaDuration);
            }

            // 확산: 대상 주변 적을 가까운 순으로 함께 맞히고 피해를 나눔
            CollectSpread(context, target, targetPosition);
            float share = _spread.Count > 0
                ? Mathf.Min(1f, parameters.Get(SkillParameterKeys.SPREAD_TOTAL, 1f) / (_spread.Count + 1))
                : 1f;

            Strike(context, target, targetPosition, coefficient * share);

            foreach (IDamageable extra in _spread)
            {
                DealDamage(context, extra, coefficient * share);
            }

            _spread.Clear();
        }

        private void CollectSpread(SkillContext context, IDamageable primary, Vector3 center)
        {
            _spread.Clear();
            int count = Mathf.RoundToInt(context.Parameters.Get(SkillParameterKeys.SPREAD_COUNT, 0f));
            float radius = context.Parameters.Get(SkillParameterKeys.SPREAD_RADIUS, 0f);

            if (count <= 0 || radius <= 0f)
            {
                return;
            }

            AttackArea.FindAllInCircle(center, radius, context.TargetMask, _candidates);
            _candidates.Remove(primary);
            _candidates.Sort((a, b) => Distance(a, center).CompareTo(Distance(b, center)));

            for (int i = 0; i < _candidates.Count && _spread.Count < count; i++)
            {
                _spread.Add(_candidates[i]);
            }

            _candidates.Clear();
        }

        private static float Distance(IDamageable target, Vector3 center)
        {
            return target is Component component ? (component.transform.position - center).sqrMagnitude : float.MaxValue;
        }

        /// <summary>기본 피해만 (확산으로 함께 맞는 적)</summary>
        protected void DealDamage(SkillContext context, IDamageable target, float coefficient)
        {
            DamageInfo info = AttackDamage.Build(context.Caster.gameObject, context.Stats, coefficient,
                FALLBACK_ATTACK_POWER, false);
            target.ApplyDamage(info);
        }

        /// <summary>대상 타격. 파생 동작(분쇄 일격 등)이 처형·충격파 같은 추가 효과를 덧붙임</summary>
        protected virtual void Strike(SkillContext context, IDamageable target, Vector3 targetPosition,
            float coefficient)
        {
            DamageInfo info = AttackDamage.Build(context.Caster.gameObject, context.Stats, coefficient,
                FALLBACK_ATTACK_POWER, false);
            target.ApplyDamage(info);
        }

        private IDamageable FindTarget(SkillContext context, Vector3 origin, float radius, float angle,
            out Vector3 targetPosition)
        {
            // 1) 커서가 가리킨 적 (타격 거리 안일 때만)
            AttackArea.FindAllInCircle(context.CursorPoint, _cursorPickRadius, context.TargetMask, _candidates);
            IDamageable picked = null;
            targetPosition = origin;
            float closestSqr = float.MaxValue;
            float reach = radius + _reachTolerance;

            foreach (IDamageable candidate in _candidates)
            {
                if (!(candidate is Component component))
                {
                    continue;
                }

                Vector3 position = component.transform.position;
                Vector3 fromCaster = position - origin;
                fromCaster.y = 0f;

                if (fromCaster.sqrMagnitude > reach * reach)
                {
                    continue;
                }

                Vector3 fromCursor = position - context.CursorPoint;
                fromCursor.y = 0f;

                if (fromCursor.sqrMagnitude < closestSqr)
                {
                    closestSqr = fromCursor.sqrMagnitude;
                    picked = candidate;
                    targetPosition = position;
                }
            }

            _candidates.Clear();

            if (picked != null)
            {
                return picked;
            }

            // 2) 커서 방향 부채꼴에서 가장 가까운 적
            return AttackArea.TryFindClosestInCone(origin, context.AimDirection, radius, angle, context.TargetMask,
                out IDamageable nearest, out targetPosition)
                ? nearest
                : null;
        }
    }
}
