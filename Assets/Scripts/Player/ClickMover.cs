using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// NavMeshAgent 기반 클릭 이동. 이동·정지·회전만 담당합니다.
    /// CharacterStats가 있으면 이동 속도를 능력치(MoveSpeed)에서 읽습니다.
    /// </summary>
    [RequireComponent(typeof(NavMeshAgent))]
    public class ClickMover : MonoBehaviour
    {
        [Tooltip("CharacterStats가 없을 때 사용하는 이동 속도")]
        [SerializeField] private float _moveSpeed = 6f;
        [SerializeField] private float _angularSpeed = 720f;
        [SerializeField] private float _acceleration = 60f;
        [SerializeField] private float _repathDistance = 0.3f;

        private NavMeshAgent _agent;
        private CharacterStats _stats;
        private float _speedMultiplier = 1f;

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
        }

        public void MoveTo(Vector3 point)
        {
            if (!_agent.isOnNavMesh)
            {
                return;
            }

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
