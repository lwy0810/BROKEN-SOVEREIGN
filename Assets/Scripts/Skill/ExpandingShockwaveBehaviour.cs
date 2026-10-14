using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 시전 지점 중심 원형 충격파를 일정 간격으로 연속 발생 (지속 기술). 파동마다 반경이 커지고,
    /// 마지막 파동에 적중한 적은 띄우기(IAirborne, 없으면 기절).
    /// 파동 중심은 시전 지점에 고정되어 시전 후 이동해도 따라오지 않습니다.
    /// 수치 키: coefficient(파동당 계수), radius(첫 파동 반경), radiusStep(파동당 반경 증가),
    /// waveCount(파동 수), waveInterval(파동 간격 초), airborneDuration(띄우기 초)
    /// 사용 기술: 격진
    /// </summary>
    [CreateAssetMenu(fileName = "ExpandingShockwaveBehaviour",
        menuName = "BrokenSovereign/Skill Behaviour/Expanding Shockwave")]
    public class ExpandingShockwaveBehaviour : SkillBehaviour
    {
        private const float FALLBACK_ATTACK_POWER = 100f;

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showArea = true;
        [SerializeField] private Color _waveColor = new Color(1f, 0.85f, 0.3f, 0.35f);
        [SerializeField] private Color _lastWaveColor = new Color(1f, 0.45f, 0.1f, 0.45f);
        [SerializeField] private float _areaDuration = 0.2f;

        private readonly List<IDamageable> _targets = new List<IDamageable>();

        private class WaveState
        {
            public Vector3 Origin;
            public float GroundY;
            public ShockwavePlan Plan;
            public int Fired;
            public float Elapsed;
        }

        public override void Begin(SkillContext context)
        {
            SkillParameterSet parameters = context.Parameters;

            var state = new WaveState
            {
                Origin = context.Caster.position,
                GroundY = context.CursorPoint.y,
                Plan = new ShockwavePlan(
                    Mathf.RoundToInt(parameters.Get(SkillParameterKeys.WAVE_COUNT, 3f)),
                    parameters.Get(SkillParameterKeys.WAVE_INTERVAL, 0.3f),
                    parameters.Get(SkillParameterKeys.RADIUS, 3f),
                    parameters.Get(SkillParameterKeys.RADIUS_STEP, 1.5f))
            };

            context.State = state;
            FireDue(context, state);
        }

        public override bool Tick(SkillContext context, float deltaTime)
        {
            if (!(context.State is WaveState state))
            {
                return false;
            }

            state.Elapsed += deltaTime;
            FireDue(context, state);
            return state.Fired < state.Plan.WaveCount;
        }

        // 프레임이 길어 여러 파동이 한 번에 밀려도 순서대로 모두 발생
        private void FireDue(SkillContext context, WaveState state)
        {
            int due = state.Plan.WavesDueBy(state.Elapsed);

            while (state.Fired < due)
            {
                FireWave(context, state, state.Fired);
                state.Fired++;
            }
        }

        private void FireWave(SkillContext context, WaveState state, int index)
        {
            SkillParameterSet parameters = context.Parameters;
            float coefficient = parameters.Get(SkillParameterKeys.COEFFICIENT, 1.5f);
            float radius = state.Plan.RadiusAt(index);
            bool isLast = state.Plan.IsLast(index);

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCircle(state.Origin, radius, state.GroundY,
                    isLast ? _lastWaveColor : _waveColor, _areaDuration);
            }

            AttackArea.FindAllInCircle(state.Origin, radius, context.TargetMask, _targets);

            foreach (IDamageable target in _targets)
            {
                DamageInfo info = AttackDamage.Build(context.Caster.gameObject, context.Stats, coefficient,
                    FALLBACK_ATTACK_POWER, false);
                target.ApplyDamage(info);

                if (isLast && !target.IsDead)
                {
                    Launch(target, parameters.Get(SkillParameterKeys.AIRBORNE_DURATION, 0.8f));
                }
            }

            Debug.Log($"{context.Definition.DisplayName} 파동 {index + 1}/{state.Plan.WaveCount}: " +
                $"반경 {radius:0.#}m, 적중 {_targets.Count}");
            _targets.Clear();
        }

        private static void Launch(IDamageable target, float duration)
        {
            if (duration <= 0f || !(target is Component component))
            {
                return;
            }

            if (component.GetComponentInParent<IAirborne>() is IAirborne airborne)
            {
                airborne.Launch(duration);
            }
            else if (component.GetComponentInParent<IStunnable>() is IStunnable stunnable)
            {
                stunnable.Stun(duration);
            }
        }
    }
}
