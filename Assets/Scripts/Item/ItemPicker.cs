using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Item
{
    /// <summary>
    /// 이름표를 클릭한 바닥 아이템·상자까지 걸어가서 줍기/열기 (10/26). 플레이어에 붙음 (HudView가 없으면 추가).
    /// 가는 도중 다른 곳을 클릭해 목적지가 바뀌면 취소합니다.
    /// </summary>
    public class ItemPicker : MonoBehaviour
    {
        private PlayerController _controller;
        private NavMeshAgent _agent;
        private WorldInteractable _target;
        private Vector3 _requested;

        public WorldInteractable Target => _target;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _agent = GetComponent<NavMeshAgent>();
        }

        public void Go(WorldInteractable target)
        {
            if (target == null || _controller == null)
            {
                return;
            }

            _target = target;

            if (InRange(target))
            {
                Arrive();
                return;
            }

            _requested = target.transform.position;
            _controller.MoveTo(_requested);
        }

        private void Update()
        {
            if (_target == null)
            {
                return;
            }

            if (InRange(_target))
            {
                Arrive();
                return;
            }

            // 다른 곳으로 이동 명령이 들어오면 (목적지가 바뀜) 줍기 취소
            bool movingThere = _agent != null && _agent.hasPath && (_agent.destination - _requested).sqrMagnitude < 1.5f * 1.5f;

            if (!movingThere && (_agent == null || !_agent.pathPending))
            {
                _target = null;
            }
        }

        private bool InRange(WorldInteractable target)
        {
            Vector3 offset = target.transform.position - transform.position;
            offset.y = 0f;
            return offset.magnitude <= target.InteractRange;
        }

        private void Arrive()
        {
            WorldInteractable target = _target;
            _target = null;
            _controller.StopMoving();
            target.Interact(gameObject);
        }
    }
}
