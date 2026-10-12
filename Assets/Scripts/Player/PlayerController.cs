using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
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
        [SerializeField] private BasicAttack _basicAttack = new BasicAttack();
        [SerializeField] private float _heldSkillRepeatInterval = 0.5f;

        private ClickMover _mover;
        private DodgeAction _dodge;
        private PotionUser _potion;
        private Health _health;
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

            Debug.Log($"PlayerController: 기술 시전 {slot} → {point} (SkillCaster 구현 시 연결)");
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

        private void ClearEngage()
        {
            _engageAction = null;
            _engageTarget = null;
            _isEngageHeld = false;
            _hasEngageExecuted = false;
        }

        // 회피 중·사망 시 다른 행동 불가
        private bool IsActionLocked => State == PlayerState.Dodging || State == PlayerState.Dead;

        private void OnDied()
        {
            ClearEngage();
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
