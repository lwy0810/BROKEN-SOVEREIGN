using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 커서 방향 전방 부채꼴 내려찍기. 범위 안의 모든 적에게 피해 (즉발).
    /// 수치 키: coefficient(계수), radius(반경 m), angle(각도)
    /// 사용 기술: 잔향 강타, 격광 강타(예정)
    /// </summary>
    [CreateAssetMenu(fileName = "ConeSmashBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Cone Smash")]
    public class ConeSmashBehaviour : SkillBehaviour
    {
        // 시전자에게 CharacterStats가 없을 때 쓰는 공격력
        private const float FALLBACK_ATTACK_POWER = 100f;

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showArea = true;
        [SerializeField] private Color _areaColor = new Color(1f, 0.55f, 0.1f, 0.4f);
        [SerializeField] private float _areaDuration = 0.25f;

        private readonly List<IDamageable> _targets = new List<IDamageable>();

        public override void Begin(SkillContext context)
        {
            SkillParameterSet parameters = context.Parameters;
            float coefficient = parameters.Get(SkillParameterKeys.COEFFICIENT, 1f);
            float radius = parameters.Get(SkillParameterKeys.RADIUS, 3f);
            float angle = parameters.Get(SkillParameterKeys.ANGLE, 120f);

            Vector3 origin = context.Caster.position;
            Vector3 forward = context.AimDirection;

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCone(origin, forward, radius, angle, context.CursorPoint.y, _areaColor,
                    _areaDuration);
            }

            AttackArea.FindAllInCone(origin, forward, radius, angle, context.TargetMask, _targets);

            foreach (IDamageable target in _targets)
            {
                // 대상마다 치명타를 따로 판정
                DamageInfo info = AttackDamage.Build(context.Caster.gameObject, context.Stats, coefficient,
                    FALLBACK_ATTACK_POWER, false);
                target.ApplyDamage(info);
            }

            Debug.Log($"{context.Definition.DisplayName}: 적중 {_targets.Count}");
            _targets.Clear();
        }
    }
}
