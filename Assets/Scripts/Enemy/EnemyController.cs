using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 일반 적 AI: 대기 → 감지·추적 → 예비 동작 후 근접 공격 → 이탈 시 시작 위치로 복귀.
    /// 상태 전이는 EnemyBrain, 이동·공격 실행은 이 컴포넌트가 담당합니다.
    /// 이동 속도·공격력은 CharacterStats에서 읽습니다.
    /// </summary>
    [RequireComponent(typeof(Health))]
    [RequireComponent(typeof(NavMeshAgent))]
    public class EnemyController : MonoBehaviour, IStunnable
    {
        [SerializeField] private EnemyDefinition _definition;

        [Tooltip("공격 대상. 비우면 Player 태그 오브젝트")]
        [SerializeField] private Transform _target;

        private Health _health;
        private NavMeshAgent _agent;
        private CharacterStats _stats;
        private Health _targetHealth;
        private EnemyBrain _brain;

        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private float _nextRepathTime;
        private float _nextAttackTime;
        private float _attackHitTime;
        private float _attackEndTime;
        private bool _hasHit;
        private Vector3 _attackForward;
        private bool _wasDamaged;

        private readonly StunTimer _stun = new StunTimer();

        public EnemyState State { get; private set; } = EnemyState.Idle;
        public bool IsStunned => _stun.IsStunned(Time.time);

        public event Action<float> Stunned;
        public Transform Target => _target;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<CharacterStats>();

            if (_definition == null)
            {
                Debug.LogWarning($"{name}: EnemyDefinition이 비어 있어 기본값을 사용합니다.", this);
                _definition = ScriptableObject.CreateInstance<EnemyDefinition>();
            }

            _brain = new EnemyBrain(_definition.ToBrainSettings());
            _homePosition = transform.position;
            _homeRotation = transform.rotation;
            _agent.stoppingDistance = 0f;
        }

        private void OnEnable()
        {
            _health.Damaged += OnDamaged;
            _health.Restored += OnRestored;

            if (_stats != null)
            {
                _stats.Sheet.Changed += ApplySpeed;
            }
        }

        private void OnDisable()
        {
            _health.Damaged -= OnDamaged;
            _health.Restored -= OnRestored;

            if (_stats != null)
            {
                _stats.Sheet.Changed -= ApplySpeed;
            }
        }

        private void Start()
        {
            if (_target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                _target = player != null ? player.transform : null;
            }

            _targetHealth = _target != null ? _target.GetComponentInParent<Health>() : null;

            if (_target != null && _targetHealth == null)
            {
                Debug.LogWarning($"{name}: 대상 {_target.name}에 Health가 없어 공격할 수 없습니다.", this);
            }

            ApplySpeed();
        }

        private void Update()
        {
            // 기절 중: 멈춰서 아무것도 하지 않음 (사망 판정은 기절보다 우선)
            if (IsStunned && !_health.IsDead)
            {
                StopAgent();
                return;
            }

            EnemySense sense = BuildSense();
            _wasDamaged = false;

            EnemyState next = _brain.Evaluate(State, sense);

            if (next != State)
            {
                ChangeState(next);
            }

            TickState(sense);
        }

        private EnemySense BuildSense()
        {
            bool hasTarget = _targetHealth != null && !_targetHealth.IsDead && _target.gameObject.activeInHierarchy;
            float distanceToTarget = hasTarget ? FlatDistance(transform.position, _target.position) : float.MaxValue;

            return new EnemySense(
                _health.IsDead,
                hasTarget,
                distanceToTarget,
                FlatDistance(transform.position, _homePosition),
                _wasDamaged,
                Time.time >= _nextAttackTime,
                Time.time >= _attackEndTime);
        }

        private void ChangeState(EnemyState next)
        {
            State = next;

            switch (next)
            {
                case EnemyState.Idle:
                    StopAgent();
                    break;

                case EnemyState.Chase:
                    _nextRepathTime = 0f;
                    break;

                case EnemyState.Attack:
                    BeginAttack();
                    break;

                case EnemyState.Return:
                    // 추적 포기 시 체력 전부 회복
                    _health.Heal(_health.Max);
                    MoveAgent(_homePosition);
                    break;

                case EnemyState.Dead:
                    // 숨겨진 상태에서 다른 에이전트를 밀어내지 않도록 비활성화
                    StopAgent();
                    _agent.enabled = false;
                    break;
            }
        }

        private void TickState(EnemySense sense)
        {
            switch (State)
            {
                case EnemyState.Chase:
                    TickChase(sense);
                    break;

                case EnemyState.Attack:
                    if (!_hasHit && Time.time >= _attackHitTime)
                    {
                        PerformHit();
                    }

                    break;
            }
        }

        // 사거리 밖이면 대상 위치로 이동, 사거리 안이면 멈춰서 바라보며 다음 공격 대기
        private void TickChase(EnemySense sense)
        {
            if (sense.DistanceToTarget <= _definition.AttackRange)
            {
                StopAgent();
                FaceTowards(_target.position);
                return;
            }

            if (Time.time >= _nextRepathTime)
            {
                _nextRepathTime = Time.time + _definition.RepathInterval;
                MoveAgent(_target.position);
            }
        }

        // 예비 동작 시작: 방향을 고정하고 범위를 표시. 표시가 끝나는 순간 타격
        private void BeginAttack()
        {
            StopAgent();

            Vector3 forward = _target != null ? _target.position - transform.position : transform.forward;
            forward.y = 0f;
            _attackForward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(_attackForward);

            _hasHit = false;
            _attackHitTime = Time.time + _definition.AttackWindup;
            _attackEndTime = _attackHitTime + _definition.AttackRecovery;
            _nextAttackTime = Time.time + _definition.AttackInterval;

            if (_definition.ShowAttackArea)
            {
                AttackAreaDebugDrawer.ShowCone(transform.position, _attackForward, _definition.AttackRange,
                    _definition.AttackAngle, transform.position.y, _definition.AttackAreaColor,
                    Mathf.Max(0.05f, _definition.AttackWindup));
            }
        }

        // 타격 시점에 대상이 범위 안에 있으면 피해 (회피 무적·회피 확률은 대상 Health가 처리)
        private void PerformHit()
        {
            _hasHit = true;

            if (_targetHealth == null || _targetHealth.IsDead)
            {
                return;
            }

            if (!AttackArea.IsInCone(transform.position, _attackForward, _definition.AttackRange,
                    _definition.AttackAngle, _target.position))
            {
                return;
            }

            DamageInfo info = AttackDamage.Build(gameObject, _stats != null ? _stats.Sheet : null,
                _definition.AttackCoefficient, _definition.FallbackAttackPower, false);
            _targetHealth.ApplyDamage(info);
        }

        /// <summary>기절: 이동 정지, 진행 중인 공격 취소</summary>
        public void Stun(float duration)
        {
            if (_health.IsDead || !_stun.Apply(duration, Time.time))
            {
                return;
            }

            StopAgent();

            if (State == EnemyState.Attack)
            {
                // 예비 동작 중이면 타격하지 않고 공격 종료로 처리 (기절이 끝나면 다시 추적)
                _hasHit = true;
                _attackEndTime = Time.time;
            }

            Stunned?.Invoke(duration);
        }

        private void OnDamaged(DamageInfo info)
        {
            _wasDamaged = true;
        }

        // 재등장: 시작 위치로 되돌리고 대기
        private void OnRestored()
        {
            _agent.enabled = true;
            _agent.Warp(_homePosition);
            transform.rotation = _homeRotation;
            _nextAttackTime = 0f;
            _attackEndTime = 0f;
            _stun.Clear();
            ChangeState(EnemyState.Idle);
        }

        private void ApplySpeed()
        {
            if (_stats != null)
            {
                _agent.speed = Mathf.Max(0f, _stats.Sheet.Get(StatType.MoveSpeed));
            }
        }

        private void MoveAgent(Vector3 destination)
        {
            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = false;
            _agent.SetDestination(destination);
        }

        private void StopAgent()
        {
            if (!_agent.enabled || !_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        private void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, Quaternion.LookRotation(direction),
                _definition.TurnSpeed * Time.deltaTime);
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        // 선택 시 감지(노랑)·추적 포기(주황)·끌려가기 한계(파랑) 범위 표시
        private void OnDrawGizmosSelected()
        {
            if (_definition == null)
            {
                return;
            }

            Vector3 home = Application.isPlaying ? _homePosition : transform.position;
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, _definition.DetectRange);
            Gizmos.color = new Color(1f, 0.5f, 0f);
            Gizmos.DrawWireSphere(transform.position, _definition.LoseRange);
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(home, _definition.LeashDistance);
        }
    }
}
