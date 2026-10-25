using System.Collections;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// NavMeshAgent 기반 클릭 이동. 이동·정지·회전만 담당합니다.
    /// CharacterStats가 있으면 이동 속도를 능력치(MoveSpeed)에서 읽습니다.
    /// 10/26 막힘 처리 (바닥 클릭 이동만): 비비며 미끄러지지 않고 '움찔' 후 제자리 정지
    /// - 갈 수 없는 곳(벽·절벽, NavMesh 밖·끊긴 길)을 찍으면 가장자리를 따라 문지르지 않고 정지
    /// - 적·NPC와 몸이 부딪혀 밀리거나 옆으로 미끄러지면 정지
    /// - 커서가 발밑 가까이면 이동하지 않음 (마우스를 누른 채 이동할 때 방향이 계속 바뀌며 떨리는 현상)
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ClickMover : MonoBehaviour, ISpeedMultiplierTarget
    {
        [Tooltip("CharacterStats가 없을 때 사용하는 이동 속도")]
        [SerializeField] private float _moveSpeed = 6f;
        [Tooltip("켜면 이동 방향으로 즉시 회전 (ARPG 방식). 끄면 NavMeshAgent 회전 속도로 서서히 회전")]
        [SerializeField] private bool _instantTurn = true;
        [SerializeField] private float _angularSpeed = 720f;
        [SerializeField] private float _acceleration = 60f;
        [SerializeField] private float _repathDistance = 0.3f;

        [Header("막힘 처리 (10/26)")]
        [Tooltip("찍은 지점에서 이 거리 안에 NavMesh가 없으면 갈 수 없는 곳으로 봄 (m)")]
        [SerializeField, Min(0.1f)] private float _reachTolerance = 1.2f;
        [Tooltip("커서가 발밑에서 이 거리 안이면 이동하지 않음 (m)")]
        [SerializeField, Min(0f)] private float _deadZone = 0.7f;
        [Tooltip("실제 속도가 원하는 속도의 이 비율보다 느리거나 방향이 벗어난 채로 이 시간이 지나면 막힘 (초)")]
        [SerializeField, Min(0.02f)] private float _blockedTime = 0.15f;
        [SerializeField, Range(0.05f, 0.9f)] private float _blockedSpeedRatio = 0.35f;
        [SerializeField, Range(10f, 90f)] private float _blockedAngle = 55f;
        [Tooltip("움찔 거리 (m)와 시간 (초)")]
        [SerializeField] private float _flinchDistance = 0.12f;
        [SerializeField] private float _flinchTime = 0.14f;

        private NavMeshAgent _agent;
        private CharacterStats _stats;
        private float _speedMultiplier = 1f;
        private bool _checkBlocked;
        private float _blockedFor;
        private Vector3? _blockedPoint;
        private Transform _model;
        private Coroutine _flinch;
        private NavMeshPath _probe;
        private float _lastGroundRequest = -1f;

        /// <summary>막혀서 멈췄을 때 (움찔)</summary>
        public event System.Action Bumped;

        public bool HasArrived =>
            !_agent.isOnNavMesh
            || (!_agent.pathPending && _agent.remainingDistance <= _agent.stoppingDistance);

        private void Awake()
        {
            _agent = GetComponent<NavMeshAgent>();
            _stats = GetComponent<CharacterStats>();

            if (_stats != null)
            {
                _stats.Sheet.Changed += ApplySpeed;
            }

            ApplySpeed();
            _agent.angularSpeed = _angularSpeed;
            _agent.acceleration = _acceleration;
            _agent.updateRotation = !_instantTurn;
        }

        // 이동 중 부딪힘 감지: 원하는 방향으로 못 가고 느려지거나 옆으로 밀리면 정지
        private void Update()
        {
            if (!_checkBlocked || !_agent.isOnNavMesh || _agent.isStopped || !_agent.hasPath || _agent.pathPending)
            {
                _blockedFor = 0f;
                return;
            }

            if (_agent.remainingDistance <= _agent.stoppingDistance + 0.3f)
            {
                _blockedFor = 0f;
                return;
            }

            Vector3 desired = _agent.desiredVelocity;
            Vector3 actual = _agent.velocity;
            desired.y = 0f;
            actual.y = 0f;

            // 출발 가속 중(원하는 속도가 아직 작을 때)은 판정하지 않음
            if (desired.magnitude < _agent.speed * 0.5f)
            {
                _blockedFor = 0f;
                return;
            }

            bool slow = actual.magnitude < desired.magnitude * _blockedSpeedRatio;
            bool sliding = actual.sqrMagnitude > 0.01f && Vector3.Angle(actual, desired) > _blockedAngle;
            _blockedFor = slow || sliding ? _blockedFor + Time.deltaTime : 0f;

            if (_blockedFor >= _blockedTime)
            {
                Bump(desired, _agent.destination);
            }
        }

        // 즉시 회전: 에이전트가 가려는 방향(desiredVelocity)을 바로 바라봄. 출발 직후 가속 중에도 방향은 즉시 전환
        private void LateUpdate()
        {
            if (!_instantTurn || !_agent.isOnNavMesh || _agent.isStopped)
            {
                return;
            }

            Vector3 direction = _agent.desiredVelocity;
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }
        }

        // Play 중 Inspector 변경 반영
        private void OnValidate()
        {
            if (_agent != null)
            {
                _agent.updateRotation = !_instantTurn;
                _agent.angularSpeed = _angularSpeed;
            }
        }

        public void MoveTo(Vector3 point)
        {
            MoveTo(point, false);
        }

        /// <summary>
        /// checkBlocked = 바닥 클릭 이동: 갈 수 없는 곳·발밑·부딪힘이면 움찔 후 정지 (10/26).
        /// 교전 접근·기술 이동은 false (대상에게 붙어야 하므로)
        /// </summary>
        public void MoveTo(Vector3 point, bool checkBlocked)
        {
            if (!_agent.isOnNavMesh)
            {
                return;
            }

            if (checkBlocked)
            {
                Vector3 offset = point - transform.position;
                offset.y = 0f;

                // 발밑: 이동하지 않음 (누른 채 이동할 때 떨림 방지)
                if (offset.magnitude < _deadZone)
                {
                    Stop();
                    return;
                }

                // 새로 누른 클릭이면 (누른 채 이어지는 요청이 아니면) 막혔던 지점 기억 해제 → 다시 판정·움찔
                if (Time.time - _lastGroundRequest > 0.25f)
                {
                    _blockedPoint = null;
                }

                _lastGroundRequest = Time.time;

                // 방금 막혔던 지점 근처를 계속 누르고 있으면 다시 움찔하지 않고 그대로 멈춰 있음
                if (_blockedPoint.HasValue && (_blockedPoint.Value - point).sqrMagnitude < 1.5f * 1.5f)
                {
                    return;
                }

                bool sameDestination = _agent.hasPath && (_agent.destination - point).sqrMagnitude < _repathDistance * _repathDistance;

                if (!sameDestination && !IsReachable(point))
                {
                    Bump(offset, point);
                    return;
                }

                _blockedPoint = null;
            }

            _checkBlocked = checkBlocked;
            _blockedFor = 0f;
            _agent.isStopped = false;

            // 같은 지점을 매 프레임 다시 요청하지 않도록 거리 차이가 작으면 경로 유지
            if (_agent.hasPath && (_agent.destination - point).sqrMagnitude < _repathDistance * _repathDistance)
            {
                return;
            }

            _agent.SetDestination(point);
        }

        public void Stop()
        {
            _checkBlocked = false;
            _blockedFor = 0f;

            if (!_agent.isOnNavMesh)
            {
                return;
            }

            _agent.isStopped = true;
            _agent.ResetPath();
        }

        public void FaceTowards(Vector3 point)
        {
            Vector3 direction = point - transform.position;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.LookRotation(direction);
        }

        /// <summary>
        /// 경로 탐색 없이 NavMesh 위에서 직접 이동 (회피 등).
        /// </summary>
        public void MoveBy(Vector3 delta)
        {
            if (!_agent.isOnNavMesh)
            {
                return;
            }

            _agent.Move(delta);
        }

        // 찍은 지점 근처에 NavMesh가 있고, 끊기지 않은 길로 갈 수 있는지
        private bool IsReachable(Vector3 point)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, _reachTolerance, _agent.areaMask))
            {
                return false;
            }

            _probe ??= new NavMeshPath();

            if (!NavMesh.CalculatePath(transform.position, hit.position, _agent.areaMask, _probe))
            {
                return false;
            }

            return _probe.status == NavMeshPathStatus.PathComplete;
        }

        // 막힘: 그 자리에 멈추고, 가려던 방향으로 살짝 움찔 (모델만 앞뒤로)
        private void Bump(Vector3 direction, Vector3 point)
        {
            _blockedPoint = point;
            Stop();
            direction.y = 0f;

            if (direction.sqrMagnitude > 0.0001f)
            {
                transform.rotation = Quaternion.LookRotation(direction);
            }

            if (_model == null)
            {
                Animator animator = GetComponentInChildren<Animator>();
                _model = animator != null && animator.transform != transform ? animator.transform : null;
            }

            if (_model != null && _flinch == null)
            {
                _flinch = StartCoroutine(Flinch());
            }

            Bumped?.Invoke();
        }

        private IEnumerator Flinch()
        {
            Vector3 rest = _model.localPosition;

            for (float t = 0f; t < 1f; t += Time.deltaTime / _flinchTime)
            {
                // 앞으로 살짝 → 원위치 (모델 기준 앞 = 캐릭터 정면)
                _model.localPosition = rest + Vector3.forward * (_flinchDistance * Mathf.Sin(t * Mathf.PI));
                yield return null;
            }

            _model.localPosition = rest;
            _flinch = null;
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = multiplier;
            ApplySpeed();
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.Sheet.Changed -= ApplySpeed;
            }
        }

        private void ApplySpeed()
        {
            float baseSpeed = _stats != null ? _stats.Sheet.Get(StatType.MoveSpeed) : _moveSpeed;
            _agent.speed = Mathf.Max(0f, baseSpeed) * _speedMultiplier;
        }
    }
}
