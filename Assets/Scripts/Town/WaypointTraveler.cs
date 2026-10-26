using System;
using BrokenSovereign.Core;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 플레이어 쪽 웨이포인트 사용: 가까이 간 웨이포인트를 발견하고, 석단 위(Waypoint.TriggerRadius)에 올라서면 목록 창(지도)을 엽니다.
    /// (10/25: F 키 대신 트리거 진입. 순간 이동으로 도착했거나 창을 닫은 뒤에는 거리 밖으로 나갔다 와야 다시 열림)
    /// 목록에서 고른 웨이포인트 정중앙으로 순간 이동(NavMeshAgent.Warp)하고 카메라를 바로 옮깁니다.
    /// 창이 열려 있는 동안 전투 입력을 막습니다 (GameplayInput). HudView가 플레이어에 붙입니다.
    /// </summary>
    public class WaypointTraveler : MonoBehaviour
    {
        private Waypoint _current;
        private Waypoint _inside;   // 지금 상호작용 거리 안에 있는 웨이포인트 (진입 순간에만 창을 엶)
        private bool _started;

        /// <summary>안내 문구용 (트리거 방식이라 항상 null)</summary>
        public Waypoint Focus => null;

        /// <summary>목록 창을 연 웨이포인트 (닫혀 있으면 null)</summary>
        public Waypoint Current => _current;

        public bool IsOpen => _current != null;

        public event Action<Waypoint> Opened;
        public event Action Closed;
        public event Action<Waypoint> Traveled;

        private void OnDisable()
        {
            Close();
        }

        private void Update()
        {
            DiscoverNearby();

            if (_current != null)
            {
                Keyboard keyboard = Keyboard.current;

                if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
                {
                    Close();
                }

                return;
            }

            Waypoint nearest = FindNearest(true);

            // 시작할 때 이미 서 있던 웨이포인트는 열지 않음
            if (!_started)
            {
                _started = true;
                _inside = nearest;
                return;
            }

            if (nearest != null && nearest != _inside && !GameplayInput.IsBlocked)
            {
                _inside = nearest;
                Open(nearest);
                return;
            }

            if (nearest != _inside && !GameplayInput.IsBlocked)
            {
                _inside = nearest;
            }
        }

        public void Open(Waypoint waypoint)
        {
            if (waypoint == null || _current != null)
            {
                return;
            }

            _current = waypoint;
            _inside = waypoint;

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

        /// <summary>
        /// 부활 도착 (10/26): 웨이포인트 정중앙으로 옮기되 목록 창은 열지 않음 (밖으로 나갔다 다시 들어오면 열림)
        /// </summary>
        public void ArriveAt(Waypoint target)
        {
            if (target == null)
            {
                return;
            }

            Close();
            PlayerWarp.Warp(transform, target.transform.position, target.transform.forward);
            _inside = target;
        }

        /// <summary>발견한 웨이포인트로 이동. 성공하면 창을 닫음</summary>
        public bool TravelTo(Waypoint target)
        {
            if (target == null || !target.IsDiscovered || target == _current)
            {
                return false;
            }

            // 10/25: 웨이포인트 정중앙에 도착. 도착한 웨이포인트는 밖으로 나갔다 다시 들어오기 전까지 열리지 않음
            PlayerWarp.Warp(transform, target.transform.position, target.transform.forward);
            Close();
            _inside = target;

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
                float radius = interactRange ? waypoint.TriggerRadius : waypoint.DiscoverRadius;   // 10/25: 석단 위에 섰을 때

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
