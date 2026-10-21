using System;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 지도 핀 포인트 (10/21): 지도 화면 우클릭 [핀 포인트 설정]으로 1개. 미니맵·지도에 표시되고,
    /// 새로 찍으면 교체, 플레이어가 도착하면(ARRIVE_DISTANCE 안) 자동 삭제, 우클릭 메뉴 [핀 해제]로도 삭제.
    /// </summary>
    public static class MapPin
    {
        /// <summary>도착으로 보는 거리 (m)</summary>
        public const float ARRIVE_DISTANCE = 3f;

        public static bool Has { get; private set; }

        /// <summary>월드 (x, z)</summary>
        public static Vector2 Position { get; private set; }

        public static event Action Changed;

        public static void Set(Vector2 worldXZ)
        {
            Has = true;
            Position = worldXZ;
            Changed?.Invoke();
        }

        public static void Clear()
        {
            if (!Has)
            {
                return;
            }

            Has = false;
            Changed?.Invoke();
        }

        /// <summary>플레이어 위치로 도착 확인 (HudView가 매 프레임 호출)</summary>
        public static void CheckArrival(Vector3 playerPosition)
        {
            if (Has && (new Vector2(playerPosition.x, playerPosition.z) - Position).sqrMagnitude <= ARRIVE_DISTANCE * ARRIVE_DISTANCE)
            {
                Clear();
            }
        }
    }
}
