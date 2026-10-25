using System;
using BrokenSovereign.Core;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 플레이어 쪽 웨이포인트 사용: 가까이 간 웨이포인트를 발견하고, 상호작용 거리 안에서 F 키로 목록 창을 엽니다.
    /// 목록에서 고른 웨이포인트 앞으로 순간 이동(NavMeshAgent.Warp)하고 카메라를 바로 옮깁니다.
    /// 창이 열려 있는 동안 전투 입력을 막습니다 (GameplayInput). HudView가 플레이어에 붙입니다.
    /// </summary>
    public class WaypointTraveler : MonoBehaviour
    {
        private const float NAVMESH_SAMPLE = 4f;

        private InputAction _interact;
        private Waypoint _focus;
        private Waypoint _current;

        /// <summary>F 키로 열 수 있는 웨이포인트 (없으면 null)</summary>
        public Waypoint Focus => _focus;

        /// <summary>목록 창을 연 웨이포인트 (닫혀 있으면 null)</summary>
        public Waypoint Current => _current;

        public bool IsOpen => _current != null;

        public event Action<Waypoint> Opened;
        public event Action Closed;
        public event Action<Waypoint> Traveled;

        private void Awake()
        {
            _interact = new InputAction("WaypointInteract", InputActionType.Button, "<Keyboard>/f");
        }

        private void OnEnable()
        {
            _interact.Enable();
        }

        private void OnDisable()
        {
            Close();
            _interact.Disable();
        }

        private void OnDestroy()
        {
            _interact.Dispose();
        }

        private void Update()
        {
            DiscoverNearby();

            if (_current != null)
            {
                Keyboard keyboard = Keyboard.current;

                if (_interact.WasPressedThisFrame() || (keyboard != null && keyboard.escapeKey.wasPressedThisFrame))
                {
                    Close();
                }

                return;
            }

            if (GameplayInput.IsBlocked)
            {
                _focus = null;
                return;
            }

            _focus = FindNearest(true);

            if (_focus != null && _interact.WasPressedThisFrame())
            {
                Open(_focus);
            }
        }

        public void Open(Waypoint waypoint)
        {
            if (waypoint == null || _current != null)
            {
                return;
            }

            _current = waypoint;
            _focus = null;

            if (TryGetComponent(out PlayerController player))
            {
                player.StopMoving();
            }

            GameplayInput.IsBlocked = true;
            Opened?.Invoke(waypoint);
        }

        public void Close()
        {
            if (_current == null)
            {
                return;
            }

            _current = null;
            GameplayInput.IsBlocked = false;
            Closed?.Invoke();
        }

        /// <summary>발견한 웨이포인트로 이동. 성공하면 창을 닫음</summary>
        public bool TravelTo(Waypoint target)
        {
            if (target == null || !target.IsDiscovered || target == _current)
            {
                return false;
            }

            Vector3 destination = target.ArrivePosition;

            if (NavMesh.SamplePosition(destination, out NavMeshHit hit, NAVMESH_SAMPLE, NavMesh.AllAreas))
            {
                destination = hit.position;
            }

            if (TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.Warp(destination);
            }
            else
            {
                transform.position = destination;
            }

            transform.rotation = Quaternion.LookRotation(target.transform.forward);
            Close();

            Camera camera = Camera.main;

            if (camera != null && camera.TryGetComponent(out QuarterViewCamera follow))
            {
                follow.Snap();
            }

            Traveled?.Invoke(target);
            return true;
        }

        private void DiscoverNearby()
        {
            Vector3 position = transform.position;

            foreach (Waypoint waypoint in Waypoint.All)
            {
                if (waypoint.IsDiscovered)
                {
                    continue;
                }

                Vector3 offset = waypoint.transform.position - position;
                offset.y = 0f;

                if (offset.sqrMagnitude <= waypoint.DiscoverRadius * waypoint.DiscoverRadius)
                {
                    waypoint.Discover();
                }
            }
        }

        private Waypoint FindNearest(bool interactRange)
        {
            Waypoint nearest = null;
            float best = float.MaxValue;
            Vector3 position = transform.position;

            foreach (Waypoint waypoint in Waypoint.All)
            {
                Vector3 offset = waypoint.transform.position - position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                float radius = interactRange ? waypoint.InteractRadius : waypoint.DiscoverRadius;

                if (distance <= radius * radius && distance < best)
                {
                    best = distance;
                    nearest = waypoint;
                }
            }

            return nearest;
        }
    }
}
