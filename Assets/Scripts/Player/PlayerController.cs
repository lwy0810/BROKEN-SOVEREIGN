using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 입력을 받아 이동·공격·기술·회피·물약으로 분배합니다 (GDD 6.1).
    /// 대상 공격은 "교전(engage)"으로 처리합니다: 대상과 행동(ITargetedAction)을 기억하고
    /// 매 프레임 사거리 밖이면 추적, 사거리 안이면 자동 실행합니다.
    /// 기술 시전은 SkillCaster 구현 전까지 로그로 대신합니다.
    /// </summary>
    [RequireComponent(typeof(ClickMover))]
    [RequireComponent(typeof(DodgeAction))]
    [RequireComponent(typeof(PotionUser))]
    [RequireComponent(typeof(Health))]
    public class PlayerController : MonoBehaviour
    {
        private const float SKILL_REACH_RATIO = 0.8f;

        [SerializeField] private BasicAttack _basicAttack = new BasicAttack();
        [SerializeField] private float _heldSkillRepeatInterval = 0.5f;

        [Tooltip("선택한 대상이 이 거리(m)보다 멀어지면 선택 해제")]
        [SerializeField] private float _targetKeepDistance = 20f;

        private ClickMover _mover;
        private DodgeAction _dodge;
        private PotionUser _potion;
        private Health _health;
        private SkillCaster _skillCaster;
        private float _nextHeldSkillTime;
        private float _actionEndTime;

        // 교전 상태
        private ITargetedAction _engageAction;
        private Transform _engageTarget;
        private bool _isEngageHeld;
        private bool _hasEngageExecuted;

        public PlayerState State { get; private set; } = PlayerState.Idle;
        public bool IsDead => State == PlayerState.Dead;
        public float BasicAttackRange => _basicAttack.Range;
        public bool HasEngageTarget => _engageTarget != null;

        /// <summary>
        /// 선택한 대상 (원 테두리·상단 체력 게이지 표시용). 교전과 별개로 유지되며,
        /// 다른 적을 선택하거나 대상이 죽거나 멀어지면 바뀝니다. 바닥 클릭 이동으로는 해제되지 않습니다.
        /// </summary>
        public Health CurrentTarget { get; private set; }

        /// <summary>기본 공격이 실제로 나갔을 때 (공격 지점)</summary>
        public event Action<Vector3> BasicAttacked;

        /// <summary>기술을 시전했을 때 (슬롯, 커서 지점)</summary>
        public event Action<SkillSlot, Vector3> SkillUsed;

        /// <summary>회피를 시작했을 때 (회피 방향)</summary>
        public event Action<Vector3> Dodged;

        /// <summary>물약을 사용했을 때 (남은 횟수)</summary>
        public event Action<int> PotionUsed;

        private void Awake()
        {
            _mover = GetComponent<ClickMover>();
            _dodge = GetComponent<DodgeAction>();
            _potion = GetComponent<PotionUser>();
            _health = GetComponent<Health>();
            _skillCaster = GetComponent<SkillCaster>();
        }

        private void OnEnable()
        {
            if (_health == null)
            {
                Debug.LogError("PlayerController: Health 컴포넌트가 없습니다.", this);
                return;
            }

            _health.Died += OnDied;
            _health.Restored += OnRestored;
        }

        private void OnDisable()
        {
            if (_health == null)
            {
                return;
            }

            _health.Died -= OnDied;
            _health.Restored -= OnRestored;
        }

        private void Update()
        {
            if (IsDead)
            {
                return;
            }

            UpdateSelectedTarget();

            if (_engageTarget != null)
            {
                UpdateEngage();
            }

            if (State == PlayerState.Dodging && !_dodge.IsDodging)
            {
                State = PlayerState.Idle;
            }
            else if (State == PlayerState.Moving && _mover.HasArrived)
            {
                State = PlayerState.Idle;
            }
            else if (State == PlayerState.Attacking && Time.time >= _actionEndTime)
            {
                State = PlayerState.Idle;
            }
        }

        /// <summary>
        /// 좌클릭 + 바닥: 커서 지점으로 이동.
        /// </summary>
        public void MoveTo(Vector3 point)
        {
            if (IsActionLocked)
            {
                return;
            }

            ClearEngage();
            _mover.MoveTo(point);
            State = PlayerState.Moving;
        }

        /// <summary>
        /// 좌클릭 + 적: 기본 공격으로 교전 시작.
        /// </summary>
        public void AttackTarget(Transform target)
        {
            Engage(_basicAttack, target);
        }

        /// <summary>
        /// 교전 시작: 사거리 밖이면 추적, 사거리 안이면 자동 실행.
        /// ReleaseEngage 전까지는 반복 실행하고, ReleaseEngage 이후에는 1회 실행되면 해제합니다.
        /// 대상 지정 기술(원거리 기술 등)도 ITargetedAction을 구현해 이 메서드로 넘기면 같은 흐름을 사용합니다.
        /// </summary>
        public void Engage(ITargetedAction action, Transform target)
        {
            if (IsActionLocked || action == null || target == null)
            {
                return;
            }

            _engageAction = action;
            _engageTarget = target;
            SelectTarget(target);
            _isEngageHeld = true;
            _hasEngageExecuted = false;

            UpdateEngage();
        }

        /// <summary>
        /// 교전 버튼을 뗐을 때: 이미 실행했으면 즉시 해제, 아직이면 1회 실행 후 해제.
        /// </summary>
        public void ReleaseEngage()
        {
            _isEngageHeld = false;

            if (_hasEngageExecuted)
            {
                ClearEngage();
            }
        }

        /// <summary>
        /// Shift + 좌클릭: 이동을 멈추고 커서 방향으로 기본 공격.
        /// </summary>
        public void AttackInPlace(Vector3 point)
        {
            if (IsActionLocked)
            {
                return;
            }

            ClearEngage();
            _mover.Stop();
            _mover.FaceTowards(point);
            PerformAction(_basicAttack, null, point);
        }

        /// <summary>
        /// 1~4 (우클릭은 CastWhileHeld 경유): 이동을 멈추지 않고 커서 방향으로 시전 (정지 상태 강요 금지 원칙).
        /// 교전 중이면 교전을 유지합니다.
        /// </summary>
        public void UseSkill(SkillSlot slot, Vector3 point)
        {
            if (IsActionLocked)
            {
                return;
            }

            if (_skillCaster != null && _skillCaster.HasSkill(slot))
            {
                // 재사용 대기 중이면 시전하지 않음
                if (!_skillCaster.TryCast(slot, point))
                {
                    return;
                }

                // 시전 순간 시전 방향으로 즉시 회전 (등 뒤 대상도 바로 돌아서 시전)
                _mover.FaceTowards(point);
            }
            else
            {
                Debug.Log($"PlayerController: 기술 시전 {slot} → {point} (연결된 기술 없음)");
            }

            SkillUsed?.Invoke(slot, point);
        }

        /// <summary>
        /// 기술 키 유지: 커서 방향으로 이동(또는 제자리)하며 일정 간격으로 기술 반복 시전.
        /// 유지형 범위 기술(휠윈드 등)도 이 경로를 사용합니다.
        /// 실제 시전 가능 여부(재사용 대기·자원)는 SkillCaster 구현 시 연결.
        /// </summary>
        public void CastWhileHeld(SkillSlot slot, Vector3 point, bool moveTowardCursor)
        {
            if (IsActionLocked)
            {
                return;
            }

            ClearEngage();

            if (moveTowardCursor)
            {
                _mover.MoveTo(point);
                State = PlayerState.Moving;
            }
            else
            {
                _mover.Stop();
                _mover.FaceTowards(point);
            }

            if (Time.time < _nextHeldSkillTime)
            {
                return;
            }

            _nextHeldSkillTime = Time.time + _heldSkillRepeatInterval;
            UseSkill(slot, point);
        }

        /// <summary>
        /// 기술 키 유지 + 커서가 적 위: 적을 대상으로 선택하고, 기술 사거리 밖이면 접근, 안이면 멈춰서 적을 바라보며 반복 시전.
        /// 이동 방향(회피 경로)이 아닌 적 방향으로 시전하기 위한 경로입니다.
        /// </summary>
        public void CastAtTarget(SkillSlot slot, Transform target)
        {
            if (IsActionLocked || target == null)
            {
                return;
            }

            ClearEngage();
            SelectTarget(target);

            Vector3 targetPosition = target.position;
            float range = GetSkillReach(slot);

            if (!IsInRange(targetPosition, range))
            {
                _mover.MoveTo(targetPosition);
                State = PlayerState.Moving;
                return;
            }

            _mover.Stop();
            _mover.FaceTowards(targetPosition);

            if (State == PlayerState.Moving)
            {
                State = PlayerState.Idle;
            }

            if (Time.time < _nextHeldSkillTime)
            {
                return;
            }

            _nextHeldSkillTime = Time.time + _heldSkillRepeatInterval;
            UseSkill(slot, targetPosition);
        }

        // 대상 지정 시전 사거리: 기술 반경의 80% (대상 중심이 범위 안쪽에 확실히 들어오도록). 기술이 없으면 기본 공격 사거리
        private float GetSkillReach(SkillSlot slot)
        {
            float radius = _skillCaster != null ? _skillCaster.GetRange(slot) : 0f;
            return radius > 0f ? radius * SKILL_REACH_RATIO : _basicAttack.Range;
        }

        /// <summary>
        /// 이동 중지 (기술 키를 뗄 때).
        /// </summary>
        public void StopMoving()
        {
            if (IsActionLocked)
            {
                return;
            }

            ClearEngage();
            _mover.Stop();
            State = PlayerState.Idle;
        }

        /// <summary>
        /// Space: 커서 방향 회피. 교전은 해제됩니다.
        /// </summary>
        public void Dodge(Vector3 point)
        {
            if (IsDead)
            {
                return;
            }

            if (_dodge.TryDodge(point - transform.position))
            {
                ClearEngage();
                State = PlayerState.Dodging;
                Dodged?.Invoke(_dodge.Direction);
            }
        }

        /// <summary>
        /// HUD 기술 슬롯 표시용 상태. SkillCaster에 기술이 연결된 슬롯은 기술 재사용 대기를 표시합니다.
        /// 그 외: 좌클릭은 기본 공격 간격, 우클릭은 유지 시전 간격, 1~4번은 미개방(무직)
        /// </summary>
        public SkillSlotState GetSlotState(SkillSlot slot)
        {
            if (_skillCaster != null && _skillCaster.HasSkill(slot))
            {
                return _skillCaster.GetSlotState(slot);
            }

            switch (slot)
            {
                case SkillSlot.LeftClick:
                    return new SkillSlotState(true, _basicAttack.CooldownRemaining, _basicAttack.Duration);
                case SkillSlot.RightClick:
                    return new SkillSlotState(true, Mathf.Max(0f, _nextHeldSkillTime - Time.time), _heldSkillRepeatInterval);
                default:
                    return SkillSlotState.Locked;
            }
        }

        /// <summary>
        /// Q: 물약 사용.
        /// </summary>
        public void UsePotion()
        {
            if (IsDead)
            {
                return;
            }

            if (_potion.TryUse())
            {
                PotionUsed?.Invoke(_potion.Charges);
            }
        }

        private void UpdateEngage()
        {
            if (IsActionLocked)
            {
                return;
            }

            if (!IsValidTarget(_engageTarget))
            {
                ClearEngage();

                if (State == PlayerState.Moving)
                {
                    _mover.Stop();
                    State = PlayerState.Idle;
                }

                return;
            }

            Vector3 targetPosition = _engageTarget.position;

            // 사거리 밖: 대상의 현재 위치로 추적 (대상이 움직여도 따라감)
            if (!IsInRange(targetPosition, _engageAction.Range))
            {
                _mover.MoveTo(targetPosition);
                State = PlayerState.Moving;
                return;
            }

            // 사거리 안: 멈춰서 대상을 바라보고 실행 가능해지면 실행
            _mover.Stop();
            _mover.FaceTowards(targetPosition);

            if (State == PlayerState.Moving)
            {
                State = PlayerState.Idle;
            }

            if (!PerformAction(_engageAction, _engageTarget, targetPosition))
            {
                return;
            }

            _hasEngageExecuted = true;

            if (!_isEngageHeld)
            {
                ClearEngage();
            }
        }

        private bool PerformAction(ITargetedAction action, Transform target, Vector3 point)
        {
            if (!action.TryExecute(transform, target, point))
            {
                return false;
            }

            State = PlayerState.Attacking;
            _actionEndTime = Time.time + action.Duration;

            if (action == _basicAttack)
            {
                BasicAttacked?.Invoke(point);
            }

            return true;
        }

        /// <summary>선택 대상 해제</summary>
        public void ClearTarget()
        {
            CurrentTarget = null;
        }

        private void SelectTarget(Transform target)
        {
            Health health = target.GetComponentInParent<Health>();

            if (health != null)
            {
                CurrentTarget = health;
            }
        }

        // 사라졌거나 죽었거나 멀어진 대상은 선택 해제
        private void UpdateSelectedTarget()
        {
            if (CurrentTarget == null)
            {
                return;
            }

            if (CurrentTarget.IsDead
                || !CurrentTarget.gameObject.activeInHierarchy
                || !IsInRange(CurrentTarget.transform.position, _targetKeepDistance))
            {
                ClearTarget();
            }
        }

        private void ClearEngage()
        {
            _engageAction = null;
            _engageTarget = null;
            _isEngageHeld = false;
            _hasEngageExecuted = false;
        }

        // 회피 중·사망 시 다른 행동 불가
        // 회피·사망·이동 잠금 기술(돌진 등) 진행 중에는 다른 행동 불가
        private bool IsActionLocked => State == PlayerState.Dodging || State == PlayerState.Dead
            || (_skillCaster != null && _skillCaster.IsMovementLocked);

        private void OnDied()
        {
            ClearEngage();
            ClearTarget();
            _mover.Stop();
            State = PlayerState.Dead;
            Debug.Log("PlayerController: 사망");
        }

        private void OnRestored()
        {
            State = PlayerState.Idle;
        }

        // 사라졌거나 죽은 대상은 교전 대상에서 제외
        private static bool IsValidTarget(Transform target)
        {
            if (target == null || !target.gameObject.activeInHierarchy)
            {
                return false;
            }

            IDamageable damageable = target.GetComponentInParent<IDamageable>();
            return damageable == null || !damageable.IsDead;
        }

        // 수평 거리 기준. 큰 적은 반경 보정이 필요하면 여기서 처리
        private bool IsInRange(Vector3 targetPosition, float range)
        {
            Vector3 offset = targetPosition - transform.position;
            offset.y = 0f;
            return offset.sqrMagnitude <= range * range;
        }
    }
}
