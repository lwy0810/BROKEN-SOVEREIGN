using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 마을 배치도 (10/25): Figma "12 · 마을 배치도"(1m = 10px)에서 읽은 길·광장·건물을 Assets/Data/Town/TownLayout.json으로 저장해 두면
    /// 마을 생성이 자동 배치 대신 이 배치도를 따릅니다. 파일이 없으면 기존 자동 배치(LevelTools.Town).
    /// 좌표 = 마을 중심 기준 (x = 동쪽, y = 북쪽 = 유니티 +z), yaw = 유니티 y 회전 (도, 0 = 정면이 북쪽).
    /// 건물 type = 프리팹 이름 (Props/{type}.prefab) 또는 상점 ShopGoods·ShopForge·ShopClinic.
    /// 새 건물 종류: Figma 부품 1개 + VARCO 프리팹만 추가 (코드 표 없음, 이름이 곧 프리팹).
    /// </summary>
    public static partial class LevelTools
    {
        public const string TOWN_LAYOUT_PATH = "Assets/Data/Town/TownLayout.json";

        // 10/25: 자동 배치 마을 유지 결정 → 배치도는 꺼 둠 (파일은 보관). 다시 쓰려면 true
        private const bool TOWN_LAYOUT_ENABLED = false;
        private const string SHOP_PREFIX = "Shop";

        [Serializable]
        private class TownLayoutData
        {
            public int version = 1;
            public LayoutStreet[] streets = Array.Empty<LayoutStreet>();
            public LayoutPlaza[] plazas = Array.Empty<LayoutPlaza>();
            public LayoutBuilding[] buildings = Array.Empty<LayoutBuilding>();
            public bool hasWaypoint;
            public Vector2 waypoint;
            public float wallHalf;   // 성벽 위치 ± (0이면 자동 배치 값)
        }

        [Serializable]
        private class LayoutStreet
        {
            public Vector2 a;
            public Vector2 b;
            public float width = 6f;
        }

        [Serializable]
        private class LayoutPlaza
        {
            public Vector2 center;
            public float radius = 7f;
        }

        [Serializable]
        private class LayoutBuilding
        {
            public string type;
            public Vector2 position;
            public float yaw;
            public Vector2 size;   // 바닥 가로(x) × 세로(정면 방향) m
        }

        private static TownLayoutData s_layout;
        private static DateTime s_layoutStamp = DateTime.MinValue;
        private static double s_layoutChecked = -10.0;
        private const double LAYOUT_CHECK_INTERVAL = 2.0;   // 지형·지도 칠하기가 칸마다 부르므로 파일 확인은 2초에 한 번

        /// <summary>배치도 (파일이 없으면 null). 파일이 바뀌면 다시 읽음</summary>
        private static TownLayoutData TownLayout()
        {
            double now = EditorApplication.timeSinceStartup;

            if (now - s_layoutChecked < LAYOUT_CHECK_INTERVAL && now >= s_layoutChecked)
            {
                return s_layout;
            }

            s_layoutChecked = now;

            if (!TOWN_LAYOUT_ENABLED || !File.Exists(TOWN_LAYOUT_PATH))
            {
                s_layout = null;
                s_layoutStamp = DateTime.MinValue;
                return null;
            }

            DateTime stamp = File.GetLastWriteTimeUtc(TOWN_LAYOUT_PATH);

            if (s_layout == null || stamp != s_layoutStamp)
            {
                s_layout = JsonUtility.FromJson<TownLayoutData>(File.ReadAllText(TOWN_LAYOUT_PATH));
                s_layoutStamp = stamp;
                s_streets = null;   // 길 캐시 다시 만들기
                Debug.Log($"LevelTools: 마을 배치도 사용 ({TOWN_LAYOUT_PATH}, 길 {s_layout.streets.Length} · 광장 {s_layout.plazas.Length} · 건물 {s_layout.buildings.Length})");
            }

            return s_layout;
        }

        // 배치도 건물: 상점은 TownSetupTools(NPC 포함), 나머지는 Props/{type}.prefab. 모두 바닥 자리 기록 + 집 둘레 차단
        private static void PlaceLayoutBuildings(TownLayoutData layout, Room town, Ground ground, Transform root)
        {
            var group = new GameObject("Buildings").transform;
            group.SetParent(root, false);
            int placed = 0;

            foreach (LayoutBuilding building in layout.buildings)
            {
                Vector2 world = town.Center + building.position;
                Quaternion facing = Quaternion.Euler(0f, building.yaw, 0f);
                Vector2 half = building.size * 0.5f;

                if (building.type.StartsWith(SHOP_PREFIX, StringComparison.Ordinal))
                {
                    string kind = building.type.Substring(SHOP_PREFIX.Length);
                    int index = Array.FindIndex(TownSetupTools.SHOPS, s => s.Kind.ToString() == kind);

                    if (index < 0)
                    {
                        Debug.LogWarning($"LevelTools: 배치도 상점 종류 없음 ({building.type})");
                        continue;
                    }

                    TownSetupTools.PlaceShop(TownSetupTools.SHOPS[index], root, ground.At(world), facing,
                        position => ground.At(new Vector2(position.x, position.z)));
                }
                else
                {
                    GameObject prefab = TownPrefab(building.type);

                    if (prefab == null)
                    {
                        Debug.LogWarning($"LevelTools: 배치도 건물 프리팹 없음 (Props/{building.type}.prefab), 자리만 기록");
                    }
                    else
                    {
                        Place(prefab, group, ground.At(world), facing, $"{building.type}_{++placed}");
                    }
                }

                var footprint = new Footprint { Center = world, Half = half, Yaw = building.yaw };
                s_footprints.Add(footprint);

                AddLotBlocker(group, ground, footprint, s_footprints.Count);   // 건물 둘레 0.5m 포함 차단 (우물도)
            }

            PlaceLayoutBlockers(town, ground, root);

            // 광장 장식: 반지름 8m 이상 광장 둘레 석주·석등
            GameObject pillar = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStonePillar.prefab");
            GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStoneLantern.prefab");

            foreach (LayoutPlaza plaza in layout.plazas)
            {
                if (plaza.radius < 8f)
                {
                    continue;
                }

                var room = new Room("Plaza", town.Center.x + plaza.center.x, town.Center.y + plaza.center.y, plaza.radius, RoomKind.Node);
                Ring(pillar, root, ground, room, 4, plaza.radius * 0.65f, "Pillar_Plaza");
                Ring(lantern, root, ground, room, 4, plaza.radius * 0.9f, "Lantern_Plaza");
            }
        }

        // 배치도: 성벽 안에서 길·광장 밖 = 이동 불가. 1m 칸을 큰 사각형으로 묶은 보이지 않는 상자 (Ignore Raycast, NavMesh 굽기에서 빠짐)
        private const float LAYOUT_CELL = 1f;
        private const float LAYOUT_STREET_CLEAR = 0.6f;   // 길 가장자리에서 이만큼은 막지 않음 (칸 반 대각선 ≈ 0.7)

        /// <summary>배치도 기준 이동 불가 땅 (마을 중심 기준). 지도 색에도 사용</summary>
        private static bool IsLayoutBlocked(Vector2 local, float streetClear)
        {
            return Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y)) <= TOWN_HALF && TownStreetDistance(local) > streetClear;
        }

        private static void PlaceLayoutBlockers(Room town, Ground ground, Transform root)
        {
            int n = Mathf.CeilToInt(TOWN_HALF * 2f / LAYOUT_CELL);
            var blocked = new bool[n, n];
            var used = new bool[n, n];
            Vector2 Cell(int i, int j) => new Vector2(-TOWN_HALF + (i + 0.5f) * LAYOUT_CELL, -TOWN_HALF + (j + 0.5f) * LAYOUT_CELL);

            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    blocked[i, j] = IsLayoutBlocked(Cell(i, j), LAYOUT_STREET_CLEAR);
                }
            }

            var group = new GameObject("GroundBlockers").transform;
            group.SetParent(root, false);
            int count = 0;

            // 막힌 칸을 만날 때마다 가로로 최대한, 그다음 세로로 최대한 늘린 사각형 하나
            for (int j = 0; j < n; j++)
            {
                for (int i = 0; i < n; i++)
                {
                    if (!blocked[i, j] || used[i, j])
                    {
                        continue;
                    }

                    int w = 1;

                    while (i + w < n && blocked[i + w, j] && !used[i + w, j])
                    {
                        w++;
                    }

                    int h = 1;

                    while (j + h < n)
                    {
                        bool full = true;

                        for (int k = 0; k < w && full; k++)
                        {
                            full = blocked[i + k, j + h] && !used[i + k, j + h];
                        }

                        if (!full)
                        {
                            break;
                        }

                        h++;
                    }

                    for (int y = 0; y < h; y++)
                    {
                        for (int x = 0; x < w; x++)
                        {
                            used[i + x, j + y] = true;
                        }
                    }

                    Vector2 local = (Cell(i, j) + Cell(i + w - 1, j + h - 1)) * 0.5f;
                    var blocker = new GameObject($"GroundBlocker_{++count}");
                    blocker.layer = 2;
                    blocker.transform.SetParent(group, false);
                    blocker.transform.position = ground.At(town.Center + local);
                    BoxCollider box = blocker.AddComponent<BoxCollider>();
                    box.size = new Vector3(w * LAYOUT_CELL, 6f, h * LAYOUT_CELL);
                    box.center = new Vector3(0f, 2f, 0f);
                    GameObjectUtility.SetStaticEditorFlags(blocker, StaticEditorFlags.BatchingStatic);
                }
            }

            Debug.Log($"LevelTools: 배치도 이동 불가 땅 상자 {count}개");
        }

        private static List<Street> LayoutStreets(TownLayoutData layout)
        {
            var streets = new List<Street>();

            foreach (LayoutStreet street in layout.streets)
            {
                streets.Add(new Street { A = street.a, B = street.b, HalfWidth = street.width * 0.5f, Box = true });
            }

            return streets;
        }

        /// <summary>마을 웨이포인트 위치 (배치도에 있으면, 마을 중심 기준)</summary>
        private static bool TryLayoutWaypoint(out Vector2 local)
        {
            TownLayoutData layout = TownLayout();
            local = layout != null && layout.hasWaypoint ? layout.waypoint : Vector2.zero;
            return layout != null && layout.hasWaypoint;
        }
    }
}
