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
    /// 공격자의 IBasicAttackModifier(석천 모드변환 등)가 사거리·계수·방어 관통·일직선 다중 타격을 바꿉니다.
    /// </summary>
    [Serializable]
    public class BasicAttack : ITargetedAction
    {
        [SerializeField] private float _range = 2f;

        [Tooltip("공격 속도 0%일 때의 공격 간격(초). 실제 간격 = 이 값 ÷ (1 + 공격 속도). " +
            "공격자에게 IAttackIntervalSource(무기)가 있으면 그 값을 우선 사용")]
        [SerializeField] private float _interval = 1f;

        [Tooltip("공격 동작 시간(초, 공격 속도 0% 기준). 이 시간 동안만 이동 불가. 공격 간격과 별개 (0 = 발동 즉시 이동 가능)")]
        [SerializeField, Min(0f)] private float _actionTime = 0f;

        [Tooltip("타격 지연(초, 공격 속도 0% 기준): 공격 시작 후 이 시간 뒤 피해 적용. " +
            "공격자에게 IAttackImpactTiming(PlayerAnimator)이 있으면 모션 기준 값을 우선 사용 (10/26)")]
        [SerializeField, Min(0f)] private float _impactDelay = 0.35f;

        [Tooltip("타격 순간 대상이 사거리 + 이 거리보다 멀어지면 빗나감")]
        [SerializeField, Min(0f)] private float _impactRangeSlack = 1f;

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
        private float _lastActionTime;
        private Transform _owner;

        private static readonly System.Collections.Generic.List<IBasicAttackModifier> _modifierBuffer =
            new System.Collections.Generic.List<IBasicAttackModifier>();
        private static readonly System.Collections.Generic.List<IDamageable> _lineBuffer =
            new System.Collections.Generic.List<IDamageable>();

        /// <summary>현재 사거리 (소유자 수정치 반영: 석천 장봉 +1.5m 등)</summary>
        public float Range => _range + GetProfile(_owner).RangeBonus;

        /// <summary>사거리 계산에 쓸 소유자 연결 (PlayerController가 Awake에서 호출)</summary>
        public void BindOwner(Transform owner)
        {
            _owner = owner;
        }

        /// <summary>공격자 수정치를 모두 적용한 판정 방식</summary>
        public static BasicAttackProfile GetProfile(Transform attacker)
        {
            BasicAttackProfile profile = BasicAttackProfile.Default;

            if (attacker == null)
            {
                return profile;
            }

            attacker.GetComponents(_modifierBuffer);

            foreach (IBasicAttackModifier modifier in _modifierBuffer)
            {
                if (modifier is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                modifier.ModifyBasicAttack(ref profile);
            }

            _modifierBuffer.Clear();
            return profile;
        }

        /// <summary>마지막 공격에 적용된 공격 간격 (공격 속도 반영). HUD 재사용 대기 표시용</summary>
        public float Interval => _lastInterval > 0f ? _lastInterval : _interval;

        /// <summary>공격 동작 시간 (이동 잠금, 공격 속도 반영). 공격 간격보다 짧아 다음 공격 전에 이동 가능</summary>
        public float Duration => _lastActionTime;

        public bool IsReady => Time.time >= _nextReadyTime;
        public float CooldownRemaining => Mathf.Max(0f, _nextReadyTime - Time.time);

        /// <summary>기본 공격 간격 (공격 속도 0% 기준). 무기 등 공격 간격 출처가 있으면 우선 사용</summary>
        public float GetBaseInterval(Transform attacker)
        {
            return attacker.TryGetComponent(out IAttackIntervalSource source) && source.BaseAttackInterval > 0f
                ? source.BaseAttackInterval
                : _interval;
        }

        public bool TryExecute(Transform attacker, Transform target, Vector3 point)
        {
            if (attacker == null)
            {
                return false;
            }

            // 공유 타이머(SkillCaster)가 있으면 즉시 발동 기술과 간격을 함께 씀
            attacker.TryGetComponent(out ISharedAttackTimer shared);

            if (shared != null ? !shared.IsAttackReady : !IsReady)
            {
                return false;
            }

            StatSheet stats = attacker.TryGetComponent(out CharacterStats characterStats) ? characterStats.Sheet : null;
            float attackSpeed = stats != null ? Mathf.Max(0f, stats.Get(StatType.AttackSpeed)) : 0f;
            float baseInterval = GetBaseInterval(attacker);
            _lastInterval = baseInterval / (1f + attackSpeed);
            _nextReadyTime = Time.time + _lastInterval;
            shared?.ConsumeAttack(_lastInterval);

            // 10/26: 피해는 모션의 타격 순간에 적용. 그 전까지는 제자리 (이동 입력 시 공격 취소 → 피해 없음)
            float impact = GetImpactDelay(attacker, _lastInterval, attackSpeed);
            _lastActionTime = Mathf.Max(Mathf.Min(_actionTime, baseInterval) / (1f + attackSpeed), impact);
            AttackImpactRunner.For(attacker).Schedule(impact, () => Strike(attacker, target, point));
            return true;
        }

        /// <summary>대기 중인 타격 취소 (공격 동작을 이동·회피로 끊었을 때)</summary>
        public void CancelPending(Transform attacker)
        {
            if (attacker != null && attacker.TryGetComponent(out AttackImpactRunner runner))
            {
                runner.Cancel();
            }
        }

        private float GetImpactDelay(Transform attacker, float interval, float attackSpeed)
        {
            float delay = attacker.TryGetComponent(out IAttackImpactTiming timing)
                ? timing.GetImpactDelay(interval)
                : _impactDelay / (1f + attackSpeed);
            return Mathf.Clamp(delay, 0f, interval * 0.9f);
        }

        // 타격 순간: 공격자·대상 상태를 다시 확인하고 피해 적용
        private void Strike(Transform attacker, Transform target, Vector3 point)
        {
            if (attacker == null || (attacker.TryGetComponent(out Health self) && self.IsDead))
            {
                return;
            }

            StatSheet stats = attacker.TryGetComponent(out CharacterStats characterStats) ? characterStats.Sheet : null;
            Vector3 origin = attacker.position;
            Vector3 forward = (target != null ? target.position : point) - origin;
            forward.y = 0f;

            if (forward.sqrMagnitude < 0.0001f)
            {
                forward = attacker.forward;
            }

            BasicAttackProfile profile = GetProfile(attacker);
            float range = _range + profile.RangeBonus;

            if (profile.IsLine)
            {
                ExecuteLine(attacker, target, origin, forward, range, profile, stats, point.y);
                return;
            }

            IDamageable victim = null;

            if (target != null)
            {
                victim = target.GetComponentInParent<IDamageable>();

                if (victim == null)
                {
                    Debug.LogWarning($"BasicAttack: {target.name}에 Health(IDamageable)가 없어 피해를 줄 수 없습니다.", target);
                }
                else
                {
                    // 모션 중에 대상이 멀어졌으면 빗나감
                    Vector3 offset = target.position - origin;
                    offset.y = 0f;

                    if (offset.magnitude > range + _impactRangeSlack)
                    {
                        victim = null;
                    }
                }
            }
            else
            {
                AttackArea.TryFindClosestInCone(origin, forward, range, _inPlaceAngle, _targetMask,
                    out victim, out _);
            }

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCone(origin, forward, range, _inPlaceAngle, point.y, _areaColor, _areaDuration);
            }

            if (victim == null || victim.IsDead)
            {
                Debug.Log("BasicAttack: 기본 공격 → 빗나감");
                return;
            }

            DamageInfo info = BuildDamage(attacker.gameObject, stats, profile);
            victim.ApplyDamage(info);
            Debug.Log($"BasicAttack: 기본 공격 → {(victim as Component)?.name} 피해 {info.Amount:0}{(info.IsCritical ? " (치명타)" : string.Empty)} (방어 측 감소 전)");
        }

        // 일직선 다중 타격: 대상(있으면) 방향 직사각형 안의 적을 가까운 순으로 최대 수까지
        private void ExecuteLine(Transform attacker, Transform target, Vector3 origin, Vector3 forward, float range,
            BasicAttackProfile profile, StatSheet stats, float groundY)
        {
            int maxTargets = profile.MaxTargets > 0 ? profile.MaxTargets : 1;
            AttackArea.FindAllInLine(origin, forward, range, profile.LineWidth, 0, _targetMask, _lineBuffer);

            // 지정한 대상은 판정 폭 밖이어도 맨 앞에 포함
            IDamageable picked = target != null ? target.GetComponentInParent<IDamageable>() : null;

            if (picked != null && !picked.IsDead)
            {
                _lineBuffer.Remove(picked);
                _lineBuffer.Insert(0, picked);
            }

            if (_lineBuffer.Count > maxTargets)
            {
                _lineBuffer.RemoveRange(maxTargets, _lineBuffer.Count - maxTargets);
            }

            if (_showArea)
            {
                float halfAngle = Mathf.Atan2(profile.LineWidth * 0.5f, Mathf.Max(range, 0.01f)) * Mathf.Rad2Deg;
                AttackAreaDebugDrawer.ShowCone(origin, forward, range, Mathf.Max(6f, halfAngle * 2f), groundY,
                    _areaColor, _areaDuration);
            }

            foreach (IDamageable victim in _lineBuffer)
            {
                victim.ApplyDamage(BuildDamage(attacker.gameObject, stats, profile));
            }

            Debug.Log($"BasicAttack: 일직선 기본 공격 → {_lineBuffer.Count}명 적중");
            _lineBuffer.Clear();
        }

        private DamageInfo BuildDamage(GameObject attacker, StatSheet stats, BasicAttackProfile profile)
        {
            return AttackDamage.Build(attacker, stats, _coefficient * profile.CoefficientMultiplier, _attackPower, true,
                DamageType.Physical, profile.ArmorPenetration);
        }
    }
}
