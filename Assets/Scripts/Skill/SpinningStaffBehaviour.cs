using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 회전 지속 시전 (키 유지). 모델을 회전시키며 일정 간격으로 주변 원형 범위를 타격합니다.
    /// 이동은 PlayerController(ChannelSkill)가 커서 방향으로 처리하고, 여기서는 이동 속도 배율만 적용합니다.
    /// 키를 떼거나(IsReleased) 유지 비용을 낼 수 없으면 종료. 충만이면 시작 후 일정 시간 비용 없음 + 종료 시 내려찍기.
    /// 수치 키: coefficient, radius, tickInterval, moveSpeedMultiplier, costPerSecond, freeDuration,
    /// finishCoefficient, finishRadius
    /// 최소 유지 (10/26): 키를 짧게 눌러도 최소 _minDuration(1초)은 회전. 자원은 초당 소모(costPerSecond)만 쓰며,
    ///   시작하려면 최소 유지 시간만큼의 자원이 있어야 함 (GetStartReserve)
    /// 사용 기술: 난천무 (석천 4번)
    /// </summary>
    [CreateAssetMenu(fileName = "SpinningStaffBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Spinning Staff")]
    public class SpinningStaffBehaviour : SkillBehaviour
    {
        private const float FALLBACK_ATTACK_POWER = 100f;

        [Tooltip("키 유지 확인이 이 시간(초) 끊기면 종료 (키 떼기 이벤트를 놓쳤을 때 안전장치)")]
        [SerializeField, Min(0.05f)] private float _holdGrace = 0.25f;

        [Tooltip("키를 바로 떼도 이 시간(초)만큼은 회전")]
        [SerializeField, Min(0f)] private float _minDuration = 1f;

        [Tooltip("모델 회전 속도 (도/초)")]
        [SerializeField] private float _spinSpeed = 900f;

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showArea = true;
        [SerializeField] private Color _areaColor = new Color(1f, 0.82f, 0.3f, 0.35f);
        [SerializeField] private Color _finishColor = new Color(1f, 0.6f, 0.1f, 0.5f);
        [SerializeField] private float _areaDuration = 0.15f;

        private readonly List<IDamageable> _targets = new List<IDamageable>();

        private class SpinState
        {
            public float NextTick;
            public ISpeedMultiplierTarget Mover;
            public Transform Model;
            public Quaternion ModelRotation;
            public float Angle;
        }

        public override void Begin(SkillContext context)
        {
            var state = new SpinState();
            context.State = state;

            state.Mover = context.Caster.GetComponent<ISpeedMultiplierTarget>();
            state.Mover?.SetSpeedMultiplier(context.Parameters.Get(SkillParameterKeys.MOVE_SPEED_MULTIPLIER, 0.8f));

            // 회전 모션이 없을 때만 모델(애니메이터)을 코드로 회전. 루트는 이동 방향을 유지
            Animator animator = context.Caster.GetComponentInChildren<Animator>();
            bool hasMotion = animator != null &&
                animator.HasState(0, Animator.StringToHash(context.Definition.AnimationState));

            if (animator != null && !hasMotion && animator.transform != context.Caster)
            {
                state.Model = animator.transform;
                state.ModelRotation = state.Model.localRotation;
            }

            HitAround(context, context.Parameters.Get(SkillParameterKeys.COEFFICIENT, 0.6f),
                context.Parameters.Get(SkillParameterKeys.RADIUS, 2.5f), _areaColor);
            state.NextTick = context.Parameters.Get(SkillParameterKeys.TICK_INTERVAL, 0.5f);
        }

        public override bool Tick(SkillContext context, float deltaTime)
        {
            var state = (SpinState)context.State;
            SkillParameterSet parameters = context.Parameters;

            bool released = context.IsReleased || Time.time - context.LastHeldTime > _holdGrace;

            if (released && context.Elapsed >= _minDuration)
            {
                return false;
            }

            // 유지 비용 (충만 시작이면 freeDuration 동안 없음). 낼 수 없으면 종료
            if (context.Elapsed > parameters.Get(SkillParameterKeys.FREE_DURATION))
            {
                float cost = parameters.Get(SkillParameterKeys.COST_PER_SECOND) * deltaTime;

                if (cost > 0f && context.Resource != null && !context.Resource.TrySpend(cost))
                {
                    Debug.Log($"{context.Definition.DisplayName}: 자원 부족으로 종료");
                    return false;
                }
            }

            float interval = Mathf.Max(0.05f, parameters.Get(SkillParameterKeys.TICK_INTERVAL, 0.5f));

            while (context.Elapsed >= state.NextTick)
            {
                HitAround(context, parameters.Get(SkillParameterKeys.COEFFICIENT, 0.6f),
                    parameters.Get(SkillParameterKeys.RADIUS, 2.5f), _areaColor);
                state.NextTick += interval;
            }

            if (state.Model != null)
            {
                state.Angle = Mathf.Repeat(state.Angle + _spinSpeed * deltaTime, 360f);
                state.Model.localRotation = state.ModelRotation * Quaternion.Euler(0f, state.Angle, 0f);
            }

            return true;
        }

        public override float GetStartReserve(SkillParameterSet parameters)
        {
            float paid = Mathf.Max(0f, _minDuration - parameters.Get(SkillParameterKeys.FREE_DURATION));
            return parameters.Get(SkillParameterKeys.COST_PER_SECOND) * paid;
        }

        public override void End(SkillContext context)
        {
            if (!(context.State is SpinState state))
            {
                return;
            }

            state.Mover?.SetSpeedMultiplier(1f);

            if (state.Model != null)
            {
                state.Model.localRotation = state.ModelRotation;
            }

            // 충만 마무리: 봉 내려찍기
            float finish = context.Parameters.Get(SkillParameterKeys.FINISH_COEFFICIENT);

            if (context.IsEmpowered && finish > 0f)
            {
                HitAround(context, finish, context.Parameters.Get(SkillParameterKeys.FINISH_RADIUS, 3f), _finishColor);
            }
        }

        private void HitAround(SkillContext context, float coefficient, float radius, Color color)
        {
            Vector3 origin = context.Caster.position;

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCircle(origin, radius, origin.y, color, _areaDuration);
            }

            AttackArea.FindAllInCircle(origin, radius, context.TargetMask, _targets);

            foreach (IDamageable target in _targets)
            {
                target.ApplyDamage(AttackDamage.Build(context.Caster.gameObject, context.Stats, coefficient,
                    FALLBACK_ATTACK_POWER, false));
            }

            _targets.Clear();
        }
    }
}
