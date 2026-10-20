using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 분쇄 일격: 단일 대상 타격(SingleStrike) + 무력화된 대상이면 처형.
    /// 처형: executeCoefficient 피해 + 대상 주변 충격파(shockwaveRadius·shockwaveCoefficient) + 무력화 즉시 종료.
    /// 무력화되지 않은 대상: coefficient 피해 후 버팀 게이지를 poiseBreakRatio만큼 즉시 감소 (충만 강화 수치).
    /// 수치 키: coefficient, radius, angle, executeCoefficient, shockwaveRadius, shockwaveCoefficient, poiseBreakRatio
    /// 사용 기술: 분쇄 일격 (야로미르 4번)
    /// </summary>
    [CreateAssetMenu(fileName = "CrushStrikeBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Crush Strike")]
    public class CrushStrikeBehaviour : SingleStrikeBehaviour
    {
        [SerializeField] private Color _executeColor = new Color(1f, 0.78f, 0.2f, 0.55f);
        [SerializeField] private float _executeMarkDuration = 0.35f;

        private readonly List<IDamageable> _shockTargets = new List<IDamageable>();

        protected override void Strike(SkillContext context, IDamageable target, Vector3 targetPosition,
            float coefficient)
        {
            SkillParameterSet parameters = context.Parameters;
            IStaggerable staggerable = (target as Component)?.GetComponent<IStaggerable>();
            GameObject caster = context.Caster.gameObject;

            if (staggerable != null && staggerable.IsStaggered)
            {
                float execute = parameters.Get(SkillParameterKeys.EXECUTE_COEFFICIENT, coefficient * 2f);
                target.ApplyDamage(AttackDamage.Build(caster, context.Stats, execute, FALLBACK_ATTACK_POWER, false));

                float radius = parameters.Get(SkillParameterKeys.SHOCKWAVE_RADIUS, 2.5f);
                float shock = parameters.Get(SkillParameterKeys.SHOCKWAVE_COEFFICIENT, 1.5f);
                AttackArea.FindAllInCircle(targetPosition, radius, context.TargetMask, _shockTargets);

                foreach (IDamageable other in _shockTargets)
                {
                    if (other != target)
                    {
                        other.ApplyDamage(AttackDamage.Build(caster, context.Stats, shock, FALLBACK_ATTACK_POWER, false));
                    }
                }

                _shockTargets.Clear();
                AttackAreaDebugDrawer.ShowCircle(targetPosition, radius, context.CursorPoint.y, _executeColor,
                    _executeMarkDuration);
                staggerable.EndStagger();
                Debug.Log($"{context.Definition.DisplayName}: 처형 ({(target as Component)?.name})");
                return;
            }

            base.Strike(context, target, targetPosition, coefficient);

            // 첫 피해에 분쇄 규칙이 버팀 게이지를 붙이므로 피해 뒤에 다시 조회
            float breakRatio = parameters.Get(SkillParameterKeys.POISE_BREAK_RATIO);

            if (breakRatio > 0f && !target.IsDead)
            {
                (target as Component)?.GetComponent<IStaggerable>()?.ReduceRatio(breakRatio);
            }
        }
    }
}
