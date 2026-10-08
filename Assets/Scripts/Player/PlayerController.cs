using System;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 입력을 받아 이동·공격·기술·회피·물약으로 분배합니다 (GDD 6.1).
    /// 기본 공격과 기술 시전은 Combat·Skill 구현 전까지 로그로 대신합니다.
    /// </summary>
    [RequireComponent(typeof(ClickMover))]
    [RequireComponent(typeof(DodgeAction))]
    [RequireComponent(typeof(PotionUser))]
    public class PlayerController : MonoBehaviour
    {
        [SerializeField] private float _basicAttackRange = 2f;
        [SerializeField] private float _basicAttackInterval = 0.6f;
        [SerializeField] private float _heldSkillRepeatInterval = 0.5f;

        private ClickMover _mover;
        private DodgeAction _dodge;
        private PotionUser _potion;
        private float _nextBasicAttackTime;
        private float _nextHeldSkillTime;

        public PlayerState State { get; private set; } = PlayerState.Idle;
        public float BasicAttackRange => _basicAttackRange;

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
        }

        private void Update()
        {
            if (State == PlayerState.Dodging && !_dodge.IsDodging)
            {
                State = PlayerState.Idle;
            }
            else if (State == PlayerState.Moving && _mover.HasArrived)
            {
                State = PlayerState.Idle;
            }
            else if (State == PlayerState.Attacking && Time.time >= _nextBasicAttackTime)
            {
                State = PlayerState.Idle;
            }
        }

        /// <summary>
        /// 좌클릭 + 바닥: 커서 지점으로 이동.
        /// </summary>
        public void MoveTo(Vector3 point)
        {
            if (State == PlayerState.Dodging)
            {
                return;
            }

            _mover.MoveTo(point);
            State = PlayerState.Moving;
        }

        /// <summary>
        /// 좌클릭 + 적: 사거리 밖이면 접근, 사거리 안이면 기본 공격.
        /// </summary>
        public void AttackTarget(Transform target)
        {
            if (State == PlayerState.Dodging || target == null)
            {
                return;
            }

            Vector3 offset = target.position - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude > _basicAttackRange * _basicAttackRange)
            {
                _mover.MoveTo(target.position);
                State = PlayerState.Moving;
                return;
            }

            _mover.Stop();
            _mover.FaceTowards(target.position);
            TryBasicAttack(target.name, target.position);
        }

        /// <summary>
        /// Shift + 좌클릭: 이동을 멈추고 커서 방향으로 기본 공격.
        /// </summary>
        public void AttackInPlace(Vector3 point)
        {
            if (State == PlayerState.Dodging)
            {
                return;
            }

            _mover.Stop();
            _mover.FaceTowards(point);
            TryBasicAttack("제자리", point);
        }

        /// <summary>
        /// 1~4 (우클릭은 CastWhileHeld 경유): 이동을 멈추지 않고 커서 방향으로 시전 (정지 상태 강요 금지 원칙).
        /// </summary>
        public void UseSkill(SkillSlot slot, Vector3 point)
        {
            if (State == PlayerState.Dodging)
            {
                return;
            }

            Debug.Log($"PlayerController: 기술 시전 {slot} → {point} (SkillCaster 구현 시 연결)");
            SkillUsed?.Invoke(slot, point);
        }

        /// <summary>
        /// 기술 키 유지: 커서 방향으로 이동(또는 제자리)하며 일정 간격으로 기술 반복 시전.
        /// 실제 시전 가능 여부(재사용 대기·자원)는 SkillCaster 구현 시 연결.
        /// </summary>
        public void CastWhileHeld(SkillSlot slot, Vector3 point, bool moveTowardCursor)
        {
            if (State == PlayerState.Dodging)
            {
                return;
            }

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
            if (State == PlayerState.Dodging)
            {
                return;
            }

            _mover.Stop();
            State = PlayerState.Idle;
        }

        /// <summary>
        /// Space: 커서 방향 회피.
        /// </summary>
        public void Dodge(Vector3 point)
        {
            if (_dodge.TryDodge(point - transform.position))
            {
                State = PlayerState.Dodging;
                Dodged?.Invoke(_dodge.Direction);
            }
        }

        /// <summary>
        /// Q: 물약 사용.
        /// </summary>
        public void UsePotion()
        {
            if (_potion.TryUse())
            {
                PotionUsed?.Invoke(_potion.Charges);
            }
        }

        private void TryBasicAttack(string targetName, Vector3 point)
        {
            if (Time.time < _nextBasicAttackTime)
            {
                return;
            }

            _nextBasicAttackTime = Time.time + _basicAttackInterval;
            State = PlayerState.Attacking;
            Debug.Log($"PlayerController: 기본 공격 → {targetName} (Combat 구현 시 연결)");
            BasicAttacked?.Invoke(point);
        }
    }
}
