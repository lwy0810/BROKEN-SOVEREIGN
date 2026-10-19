using System;
using BrokenSovereign.Map;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 지도 아이콘 그림 (종류별 스프라이트) 과 지도 확대 단계별 표시 조건.
    /// MaxViewWidth: 지도에 보이는 가로 폭(m)이 이 값 이하일 때만 표시 (0 = 항상). 상점처럼 작은 표시는 확대했을 때만 보이게 합니다.
    /// 에셋: Assets/Settings/MapIconSet.asset (BrokenSovereign > UI > HUD 아트 연결 메뉴가 생성)
    /// </summary>
    [CreateAssetMenu(menuName = "BrokenSovereign/UI/Map Icon Set", fileName = "MapIconSet")]
    public class MapIconSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public MapMarkerKind Kind;
            public Sprite Sprite;
            public float MaxViewWidth;
        }

        public Entry[] Entries = Array.Empty<Entry>();

        public Entry Find(MapMarkerKind kind)
        {
            foreach (Entry entry in Entries)
            {
                if (entry.Kind == kind)
                {
                    return entry;
                }
            }

            return null;
        }
    }
}
