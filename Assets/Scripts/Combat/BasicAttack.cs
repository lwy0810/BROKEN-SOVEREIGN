using System;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기본 공격 (GDD 6.1). 단일 대상 피해.
    /// - 대상 지정: 지정한 대상에게 피해
    /// - 제자리(대상 없음): 전방 부채꼴 안의 가장 가까운 적에게 피해
    /// 원거리 기본 공격이 필요하면 Range를 늘리거나 ITargetedAction을 구현한 별도 클래스로 교체합니다.
    /// </summary>
    [Serializable]
    public class BasicAttack : ITargetedAction
    {
        [SerializeField] private float _range = 2f;
        [SerializeField] private float _interval = 0.6f;
        [SerializeField] private float _attackPower = 100f;
        [SerializeField] private float _coefficient = 1f;

        [Tooltip("제자리 공격 판정 각도 (전방 부채꼴)")]
        [SerializeField] private float _inPlaceAngle = 90f;

        [Tooltip("제자리 공격이 찾을 대상 레이어 (Enemy)")]
        [SerializeField] private LayerMask _targetMask;

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showArea = true;
        [SerializeField] private Color _areaColor = new Color(1f, 0f, 0f, 0.35f);
        [SerializeField] private float _areaDuration = 0.2f;

        private float _nextReadyTime;

        public float Range => _range;
        public float Duration => _interval;
        public bool IsReady => Time.time >= _nextReadyTime;

        public bool TryExecute(Transform attacker, Transform target, Vector3 point)
        {
            if (!IsReady || attacker == null)
            {
                return false;
            }

            _nextReadyTime = Time.time + _interval;

            Vector3 origin = attacker.position;
            Vector3 forward = point - origin;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = attacker.forward;
            }

            IDamageable victim = null;

            if (target != null)
            {
                victim = target.GetComponentInParent<IDamageable>();

                if (victim == null)
                {
                    Debug.LogWarning($"BasicAttack: {target.name}에 Health(IDamageable)가 없어 피해를 줄 수 없습니다.", target);
                }
            }
            else
            {
                AttackArea.TryFindClosestInCone(origin, forward, _range, _inPlaceAngle, _targetMask,
                    out victim, out _);
            }

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCone(origin, forward, _range, _inPlaceAngle, point.y, _areaColor, _areaDuration);
            }

            if (victim == null || victim.IsDead)
            {
                Debug.Log("BasicAttack: 기본 공격 → 빗나감");
                return true;
            }

            float amount = DamageCalculator.Calculate(new DamageRequest(_attackPower, _coefficient));
            victim.ApplyDamage(new DamageInfo(attacker.gameObject, amount, true));
            Debug.Log($"BasicAttack: 기본 공격 → {(victim as Component)?.name} 피해 {amount}");
            return true;
        }
    }
}
