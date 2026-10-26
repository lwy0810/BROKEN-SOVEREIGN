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
        [Tooltip("이 레이어의 대상(적·NPC)이 가까이 붙어 있으면 밀려서 느려진 것은 막힘으로 보지 않음 (몸막기는 유지). 비우면 Enemy·NPC")]
        [SerializeField] private LayerMask _crowdMask;
        [Tooltip("붙어 있다고 보는 거리: 내 반지름 + 이 값 (m)")]
        [SerializeField, Min(0f)] private float _crowdMargin = 1f;
        [Tooltip("바닥 클릭 이동 중 길찾기 회피 우선순위 (낮을수록 우선). 적(50)보다 낮으면 이동 중에는 적이 비켜 줘서 붙은 적에게 갇히지 않음. 멈춰 있을 때는 원래 값(몸막기)")]
        [SerializeField, Range(0, 99)] private int _movePriority = 20;
        [Tooltip("막혀서 멈출 때 Console에 이유 출력 (디버그)")]
        [SerializeField] private bool _logBumps = true;
        [Tooltip("움찔 거리 (m)와 시간 (초)")]
        [SerializeField] private float _flinchDistance = 0.12f;
        [SerializeField] private float _flinchTime = 0.14f;

        private NavMeshAgent _agent;
        private CharacterStats _stats;
        private float _speedMultiplier = 1f;
        private bool _checkBlocked;
        private float _blockedFor;
        private Transform _model;
        private Coroutine _flinch;
        private NavMeshPath _probe;
        private readonly Collider[] _crowdHits = new Collider[4];
        private int _basePriority;
        private float _lastGroundRequest = -1f;

        /// <summary>바닥 이동 요청 간격이 이보다 짧으면 누른 채 끄는 중으로 봄 (초)</summary>
        private const float HELD_INTERVAL = 0.25f;

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
            _basePriority = _agent.avoidancePriority;

            if (_crowdMask.value == 0)
            {
                _crowdMask = LayerMask.GetMask("Enemy", "NPC");
            }
        }

        /// <summary>적·NPC가 몸에 붙어 있음 (서로 밀어내는 회피 때문에 느려지거나 방향이 틀어질 수 있음)</summary>
        private bool IsCrowded()
        {
            int count = Physics.OverlapSphereNonAlloc(transform.position, _agent.radius + _crowdMargin, _crowdHits,
                _crowdMask, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (_crowdHits[i] != null && !_crowdHits[i].transform.IsChildOf(transform))
                {
                    return true;
                }
            }

            return false;
        }

        // 이동 중 부딪힘 감지: 원하는 방향으로 못 가고 느려지거나 옆으로 밀리면 정지
        private void Update()
        {
            // 도착해 멈추면 원래 회피 우선순위로 (서 있을 때는 몸막기)
            if (_agent.isOnNavMesh && !_agent.pathPending && !_agent.hasPath && _agent.avoidancePriority != _basePriority)
            {
                _agent.avoidancePriority = _basePriority;
            }

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

            // 10/26: 적이 붙어 밀려서 느려진 것은 벽에 부딪힌 게 아니므로 움찔·정지하지 않음 (몸막기 회피는 그대로)
            if ((slow || sliding) && IsCrowded())
            {
                _blockedFor = 0f;
                return;
            }

            _blockedFor = slow || sliding ? _blockedFor + Time.deltaTime : 0f;

            if (_blockedFor >= _blockedTime)
            {
                Bump(desired, "이동 중 부딪힘");
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

            // 감속 도착: 클릭 한 번·교전 접근은 부드럽게 멈춤. 누른 채 끄는 중에는 끔 (아래)
            _agent.autoBraking = true;

            if (checkBlocked)
            {
                Vector3 offset = point - transform.position;
                offset.y = 0f;

                // 누른 채 끄는 중인지 (직전 요청이 0.25초 안): 10/26 끌기 버벅임 수정
                bool held = Time.time - _lastGroundRequest <= HELD_INTERVAL;
                _lastGroundRequest = Time.time;

                // 끄는 중: 커서가 가까우면 목표도 가까워 매번 감속 → 멈칫 반복. 감속 없이 같은 속도로 따라감
                _agent.autoBraking = !held;

                // 발밑: 새 클릭은 정지, 끄는 중에는 멈췄다 출발을 반복하지 않도록 지금 경로 유지
                if (offset.magnitude < _deadZone)
                {
                    if (!held)
                    {
                        Stop();
                    }

                    return;
                }

                // 끄는 중: 갈 수 없는 곳을 지나가도 멈추거나 움찔하지 않고, 커서 쪽으로 갈 수 있는 가장 가까운 지점으로 이동
                if (held)
                {
                    if (!TryResolveHeldTarget(point, out Vector3 target))
                    {
                        return;
                    }

                    point = target;
                }
                else
                {
                    // 새로 누른 클릭: 찍은 곳까지 못 가면 갈 수 있는 가장 가까운 지점까지 이동 (10/26 오판 수정).
                    // 그 지점이 발밑과 다를 바 없을 때만 움찔 후 정지
                    bool sameDestination = _agent.hasPath && (_agent.destination - point).sqrMagnitude < _repathDistance * _repathDistance;

                    if (!sameDestination && !IsReachable(point, out string reason))
                    {
                        if (TryResolveHeldTarget(point, out Vector3 nearest))
                        {
                            point = nearest;
                        }
                        else
                        {
                            Bump(offset, $"갈 수 없는 곳 클릭 ({reason})");
                            return;
                        }
                    }
                }
            }

            _checkBlocked = checkBlocked;
            _blockedFor = 0f;
            _agent.isStopped = false;

            // 10/26: 바닥 클릭 이동 중에는 적보다 우선 (붙은 적에게 밀려 갇히지 않음). 교전 접근은 원래 값
            _agent.avoidancePriority = checkBlocked ? _movePriority : _basePriority;

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
            _agent.avoidancePriority = _basePriority;
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
        /// <summary>
        /// 끄는 중 목표: 커서 지점이 NavMesh 위면 그곳까지의 경로 끝(이어지지 않은 곳이면 가장 가까운 지점),
        /// NavMesh 밖이면 커서 방향으로 NavMesh 가장자리. 발밑과 다를 바 없으면 false (지금 경로 유지)
        /// </summary>
        private bool TryResolveHeldTarget(Vector3 point, out Vector3 target)
        {
            // 경로 시작: 에이전트의 NavMesh 위 위치 (경사에서 transform과 높이가 어긋나 계산이 실패하던 문제)
            Vector3 from = _agent.nextPosition;
            target = point;

            if (NavMesh.SamplePosition(point, out NavMeshHit hit, _reachTolerance, _agent.areaMask))
            {
                _probe ??= new NavMeshPath();

                if (NavMesh.CalculatePath(from, hit.position, _agent.areaMask, _probe) &&
                    _probe.status != NavMeshPathStatus.PathInvalid && _probe.corners.Length > 0)
                {
                    target = _probe.corners[_probe.corners.Length - 1];
                }
                else
                {
                    return false;
                }
            }
            else if (NavMesh.Raycast(from, new Vector3(point.x, from.y, point.z), out NavMeshHit edge, _agent.areaMask))
            {
                target = edge.position;
            }
            else if (NavMesh.SamplePosition(point, out NavMeshHit far, _reachTolerance * 3f, _agent.areaMask) &&
                NavMesh.CalculatePath(from, far.position, _agent.areaMask, _probe ??= new NavMeshPath()) &&
                _probe.status != NavMeshPathStatus.PathInvalid && _probe.corners.Length > 0)
            {
                // 가장자리를 못 찾으면 조금 더 넓게 찾은 가장 가까운 NavMesh 지점으로
                target = _probe.corners[_probe.corners.Length - 1];
            }
            else
            {
                return false;
            }

            Vector3 offset = target - from;
            offset.y = 0f;
            return offset.magnitude >= _deadZone * 0.5f;
        }

        private bool IsReachable(Vector3 point, out string reason)
        {
            if (!NavMesh.SamplePosition(point, out NavMeshHit hit, _reachTolerance, _agent.areaMask))
            {
                reason = $"{_reachTolerance}m 안에 NavMesh 없음";
                return false;
            }

            _probe ??= new NavMeshPath();

            if (!NavMesh.CalculatePath(_agent.nextPosition, hit.position, _agent.areaMask, _probe))
            {
                reason = "경로 계산 실패";
                return false;
            }

            reason = _probe.status.ToString();
            return _probe.status == NavMeshPathStatus.PathComplete;
        }

        // 막힘: 그 자리에 멈추고, 가려던 방향으로 살짝 움찔 (모델만 앞뒤로)
        private void Bump(Vector3 direction, string reason)
        {
            if (_logBumps)
            {
                Debug.Log($"ClickMover: 막혀서 정지 — {reason}", this);
            }

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
