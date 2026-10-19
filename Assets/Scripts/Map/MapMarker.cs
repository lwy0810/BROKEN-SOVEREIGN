using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Map
{
    /// <summary>미니맵·전체 지도 아이콘 종류</summary>
    public enum MapMarkerKind
    {
        Merchant,  // 잡화상
        Smith,     // 대장간
        Healer,    // 치유소
        Waypoint,  // 웨이포인트
        Event,     // 이벤트 (직업 선택 등)
        Trial,     // 시련 (정예)
        Boss,      // 보스 제단
        Enemy      // 적 (미니맵에만, 가까운 것만)
    }

    /// <summary>
    /// 지도에 아이콘으로 표시할 오브젝트. 활성 목록(All)을 미니맵·전체 지도가 매 프레임 읽습니다.
    /// 마을 NPC·표시 오브젝트·적 프리팹에 에디터 도구가 붙입니다.
    /// </summary>
    public class MapMarker : MonoBehaviour
    {
        private static readonly List<MapMarker> _all = new List<MapMarker>();

        [SerializeField] private MapMarkerKind _kind = MapMarkerKind.Waypoint;

        public static IReadOnlyList<MapMarker> All => _all;

        public MapMarkerKind Kind => _kind;

        /// <summary>에디터 도구에서 종류 지정</summary>
        public void SetKind(MapMarkerKind kind)
        {
            _kind = kind;
        }

        private void OnEnable()
        {
            _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }
    }
}
