using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 단일 대상 타격 (즉발). 커서 지점의 적을 우선 노리고, 없으면 커서 방향 부채꼴에서 가장 가까운 적 1명.
    /// 수치 키: coefficient(계수), radius(타격 거리 m), angle(대상 탐색 각도)
    /// 사용 기술: 강타
    /// </summary>
    [CreateAssetMenu(fileName = "SingleStrikeBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Single Strike")]
    public class SingleStrikeBehaviour : SkillBehaviour
    {
        private const float FALLBACK_ATTACK_POWER = 100f;

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

        public override void Begin(SkillContext context)
        {
            SkillParameterSet parameters = context.Parameters;
            float coefficient = parameters.Get(SkillParameterKeys.COEFFICIENT, 1.5f);
            float radius = parameters.Get(SkillParameterKeys.RADIUS, 2.5f);
            float angle = parameters.Get(SkillParameterKeys.ANGLE, 120f);

            Vector3 origin = context.Caster.position;
            IDamageable target = FindTarget(context, origin, radius, angle, out Vector3 targetPosition);

            if (target == null)
            {
                Debug.Log($"{context.Definition.DisplayName}: 대상 없음");
                return;
            }

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCircle(targetPosition, _hitMarkRadius, context.CursorPoint.y, _areaColor,
                    _areaDuration);
            }

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
