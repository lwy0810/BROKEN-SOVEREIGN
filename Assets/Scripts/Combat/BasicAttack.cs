using System;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기본 공격 (GDD 6.1). 단일 대상 물리 피해.
    /// - 대상 지정: 지정한 대상에게 피해
    /// - 제자리(대상 없음): 전방 부채꼴 안의 가장 가까운 적에게 피해
    /// 공격자에게 CharacterStats가 있으면 공격력·피해 증가·주 스탯·치명타·공격 속도를 능력치에서 읽고,
    /// 없으면 아래 기본값을 사용합니다. 방어 측 감소(방어력·회피)는 대상 Health가 적용합니다.
    /// 원거리 기본 공격이 필요하면 Range를 늘리거나 ITargetedAction을 구현한 별도 클래스로 교체합니다.
    /// </summary>
    [Serializable]
    public class BasicAttack : ITargetedAction
    {
        [SerializeField] private float _range = 2f;

        [Tooltip("공격 속도 0%일 때의 공격 간격(초). 실제 간격 = 이 값 ÷ (1 + 공격 속도)")]
        [SerializeField] private float _interval = 0.6f;

        [Tooltip("공격자에게 CharacterStats가 없을 때 사용하는 공격력")]
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
        private float _lastInterval = -1f;

        public float Range => _range;

        /// <summary>마지막 공격에 적용된 공격 간격 (공격 속도 반영)</summary>
        public float Duration => _lastInterval > 0f ? _lastInterval : _interval;

        public bool IsReady => Time.time >= _nextReadyTime;
        public float CooldownRemaining => Mathf.Max(0f, _nextReadyTime - Time.time);

        public bool TryExecute(Transform attacker, Transform target, Vector3 point)
        {
            if (!IsReady || attacker == null)
            {
                return false;
            }

            StatSheet stats = attacker.TryGetComponent(out CharacterStats characterStats) ? characterStats.Sheet : null;
            float attackSpeed = stats != null ? Mathf.Max(0f, stats.Get(StatType.AttackSpeed)) : 0f;
            _lastInterval = _interval / (1f + attackSpeed);
            _nextReadyTime = Time.time + _lastInterval;

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

            DamageInfo info = BuildDamage(attacker.gameObject, stats);
            victim.ApplyDamage(info);
            Debug.Log($"BasicAttack: 기본 공격 → {(victim as Component)?.name} 피해 {info.Amount:0}{(info.IsCritical ? " (치명타)" : string.Empty)} (방어 측 감소 전)");
            return true;
        }

        private DamageInfo BuildDamage(GameObject attacker, StatSheet stats)
        {
            const DamageType type = DamageType.Physical;

            if (stats == null)
            {
                float plain = DamageCalculator.Calculate(new DamageRequest(_attackPower, _coefficient));
                return new DamageInfo(attacker, plain, true, type);
            }

            bool isCritical = StatFormula.RollCrit(stats.Get(StatType.CritChance), UnityEngine.Random.value);
            float critMultiplier = isCritical ? 1f + Mathf.Max(0f, stats.Get(StatType.CritDamage)) : 1f;

            float amount = DamageCalculator.Calculate(new DamageRequest(
                stats.Get(StatType.AttackPower),
                _coefficient,
                stats.GetDamageBonus(type),
                0f,
                stats.MainStatDamageMultiplier,
                critMultiplier));

            return new DamageInfo(attacker, amount, true, type, isCritical);
        }
    }
}
