using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 플레이어 순간 이동 공용 처리 (웨이포인트·귀환 포탈, 10/25).
    /// 목적지를 NavMesh 위로 맞추고 NavMeshAgent.Warp, 바라볼 방향 지정, 쿼터뷰 카메라를 바로 옮깁니다.
    /// </summary>
    public static class PlayerWarp
    {
        private const float NAVMESH_SAMPLE = 4f;

        /// <summary>NavMesh 위 가장 가까운 점 (없으면 그대로)</summary>
        public static Vector3 OnNavMesh(Vector3 point)
        {
            return NavMesh.SamplePosition(point, out NavMeshHit hit, NAVMESH_SAMPLE, NavMesh.AllAreas) ? hit.position : point;
        }

        public static void Warp(Transform player, Vector3 destination, Vector3 forward)
        {
            destination = OnNavMesh(destination);

            if (player.TryGetComponent(out PlayerController controller))
            {
                controller.StopMoving();
            }

            if (player.TryGetComponent(out NavMeshAgent agent) && agent.isOnNavMesh)
            {
                agent.ResetPath();
                agent.Warp(destination);
            }
            else
            {
                player.position = destination;
            }

            forward.y = 0f;

            if (forward.sqrMagnitude > 0.0001f)
            {
                player.rotation = Quaternion.LookRotation(forward.normalized);
            }

            Camera camera = Camera.main;

            if (camera != null && camera.TryGetComponent(out QuarterViewCamera follow))
            {
                follow.Snap();
            }
        }
    }
}
