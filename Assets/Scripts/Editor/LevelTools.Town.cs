using System.Collections.Generic;
using BrokenSovereign.Map;
using BrokenSovereign.Player;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Random = System.Random;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 마을 (2026-10-19 변경, 참고: 디아블로4 마을 지도). 성벽으로 둘러싼 124m 정사각형 마을.
    /// 가운데 원형 광장(웨이포인트·석주) → 광장 둘레길 → 동서남북 큰길(성문 4개) → 안쪽 네모 길(±36) → 성벽 안쪽 길(±54),
    /// 북동·남서 대각선 길. 길 사이 구역은 집으로 채우고, 상점 3곳은 광장 둘레길을 바라봅니다.
    /// 길은 돌바닥, 구역은 다진 흙. 지도 그림에는 건물 자리를 어둡게 그립니다 (Footprints).
    /// </summary>
    public static partial class LevelTools
    {
        public const float TOWN_HALF = 62f;          // 성벽 위치 (마을 중심 기준 ±)
        private const float PLAZA_RADIUS = 14f;
        private const float MAIN_STREET = 9f;
        private const float SIDE_STREET = 6f;
        private const float GATE_HALF = 8f;          // 성문 열린 폭의 절반
        private const float WALL_SEGMENT = 6f;
        private const float HOUSE_CELL = 9.5f;
        private const float SHOP_DISTANCE = 28f;

        // 상점 방향(도, 0 = 북쪽 +z): 잡화상·대장간·치유소. 큰길·대각선 길을 피한 구역
        private static readonly float[] SHOP_ANGLES = { 300f, 135f, 330f };

        /// <summary>지도에 어둡게 그릴 건물 자리 (마을 생성 때 기록)</summary>
        private struct Footprint
        {
            public Vector2 Center;
            public Vector2 Half;
            public float Yaw;

            public bool Contains(Vector2 p) => Distance(p) <= 0f;

            /// <summary>사각형 가장자리까지 거리 (안쪽 음수). Yaw = 유니티 y 회전 (도)</summary>
            public float Distance(Vector2 p)
            {
                float r = Yaw * Mathf.Deg2Rad;
                Vector2 d = p - Center;
                // 월드 (x, z) → 건물 기준 (오른쪽, 앞쪽)
                float x = d.x * Mathf.Cos(r) - d.y * Mathf.Sin(r);
                float z = d.x * Mathf.Sin(r) + d.y * Mathf.Cos(r);
                return Mathf.Max(Mathf.Abs(x) - Half.x, Mathf.Abs(z) - Half.y);
            }
        }

        private static readonly List<Footprint> s_footprints = new List<Footprint>();

        private struct Street
        {
            public Vector2 A;
            public Vector2 B;
            public float HalfWidth;
        }

        private static List<Street> s_streets;

        /// <summary>마을 길 (마을 중심 기준 좌표)</summary>
        private static List<Street> TownStreets()
        {
            if (s_streets != null)
            {
                return s_streets;
            }

            var streets = new List<Street>();

            void Add(float ax, float az, float bx, float bz, float width)
            {
                streets.Add(new Street { A = new Vector2(ax, az), B = new Vector2(bx, bz), HalfWidth = width * 0.5f });
            }

            // 동서남북 큰길 (성문 밖 6m까지)
            float end = TOWN_HALF + 6f;
            Add(0f, 0f, 0f, end, MAIN_STREET);
            Add(0f, 0f, 0f, -end, MAIN_STREET);
            Add(0f, 0f, end, 0f, MAIN_STREET);
            Add(0f, 0f, -end, 0f, MAIN_STREET);

            // 광장 둘레길 (반지름 17, 20각형)
            const int ringSegments = 20;
            const float ringRadius = PLAZA_RADIUS + 3f;

            for (int i = 0; i < ringSegments; i++)
            {
                float a0 = i * Mathf.PI * 2f / ringSegments;
                float a1 = (i + 1) * Mathf.PI * 2f / ringSegments;
                Add(Mathf.Sin(a0) * ringRadius, Mathf.Cos(a0) * ringRadius, Mathf.Sin(a1) * ringRadius, Mathf.Cos(a1) * ringRadius, SIDE_STREET);
            }

            // 안쪽 네모 길, 성벽 안쪽 길
            foreach (float s in new[] { 36f, 54f })
            {
                Add(-s, s, s, s, SIDE_STREET);
                Add(s, s, s, -s, SIDE_STREET);
                Add(s, -s, -s, -s, SIDE_STREET);
                Add(-s, -s, -s, s, SIDE_STREET);
            }

            // 북동·남서 대각선 길
            Add(12f, 12f, 36f, 36f, SIDE_STREET);
            Add(-12f, -12f, -36f, -36f, SIDE_STREET);

            s_streets = streets;
            return streets;
        }

        /// <summary>마을 길·광장까지 거리 (안쪽 음수). local = 마을 중심 기준</summary>
        private static float TownStreetDistance(Vector2 local)
        {
            float d = local.magnitude - PLAZA_RADIUS;

            foreach (Street street in TownStreets())
            {
                d = Mathf.Min(d, SegmentDistance(local, street.A, street.B) - street.HalfWidth);
            }

            return d;
        }

        /// <summary>마을 성벽 안 (정사각형) 판정 가중치 (1 = 안)</summary>
        private static float InsideTownSquare(Vector2 local, float margin = 0f)
        {
            float edge = Mathf.Max(Mathf.Abs(local.x), Mathf.Abs(local.y));
            return 1f - SmoothStep(TOWN_HALF + margin, TOWN_HALF + margin + 4f, edge);
        }

        // ------------------------------------------------------------------
        // 배치
        // ------------------------------------------------------------------

        private static void PlaceTown(LevelSpec spec, Ground ground, Transform parent)
        {
            s_footprints.Clear();
            int townIndex = System.Array.FindIndex(spec.Rooms, r => r.Kind == RoomKind.Town);

            if (townIndex < 0)
            {
                return;
            }

            Room town = spec.Rooms[townIndex];
            var root = new GameObject("Town").transform;
            root.SetParent(parent, false);

            PlaceTownShops(town, ground, root);
            PlaceTownWalls(town, ground, root);
            PlaceTownLandmarks(town, ground, root);   // 종루·기원소·우물·객잔·저택 대문 (10/22, VARCO)
            PlaceTownHouses(spec, town, ground, root);

            GameObject pillar = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStonePillar.prefab");
            GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStoneLantern.prefab");
            Ring(pillar, root, ground, town, 4, 9f, "Pillar_Plaza");
            Ring(lantern, root, ground, new Room("Plaza", town.Center.x, town.Center.y, 0f, RoomKind.Node), 4, 12.5f, "Lantern_Plaza");
        }

        // 상점 3곳: 광장 둘레길을 바라봄. NPC는 건물 앞
        private static void PlaceTownShops(Room town, Ground ground, Transform root)
        {
            for (int i = 0; i < TownSetupTools.SHOPS.Length; i++)
            {
                TownSetupTools.ShopSpec shop = TownSetupTools.SHOPS[i];
                Vector3 offset = Polar(SHOP_DISTANCE, SHOP_ANGLES[i]);
                Vector2 p = town.Center + new Vector2(offset.x, offset.z);
                Quaternion facing = Yaw(town.Center - p);
                TownSetupTools.PlaceShop(shop, root, ground.At(p), facing, position => ground.At(new Vector2(position.x, position.z)));
                s_footprints.Add(new Footprint { Center = p, Half = new Vector2(shop.Size.x * 0.5f, shop.Size.z * 0.5f), Yaw = facing.eulerAngles.y });
            }
        }

        // 성벽: 네 변을 6m 조각으로, 성문(큰길) 자리는 비우고 패루 + 석등. 모서리 석주
        private static void PlaceTownWalls(Room town, Ground ground, Transform root)
        {
            GameObject wall = EnsureTownWall();
            GameObject paifang = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropPaifang.prefab");
            GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStoneLantern.prefab");
            GameObject pillar = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStonePillar.prefab");
            var walls = new GameObject("Walls").transform;
            walls.SetParent(root, false);
            Vector2[] directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

            foreach (Vector2 dir in directions)
            {
                var along = new Vector2(dir.y, -dir.x);
                int count = Mathf.RoundToInt(TOWN_HALF * 2f / WALL_SEGMENT);

                for (int i = 0; i < count; i++)
                {
                    float t = -TOWN_HALF + (i + 0.5f) * WALL_SEGMENT;

                    if (Mathf.Abs(t) < GATE_HALF)
                    {
                        continue;
                    }

                    Vector2 p = town.Center + dir * TOWN_HALF + along * t;
                    Place(wall, walls, ground.At(p), Yaw(dir), $"Wall_{i}");
                }

                Vector2 gate = town.Center + dir * TOWN_HALF;
                Place(paifang, walls, ground.At(gate), Yaw(dir), $"Gate_{dir}");

                foreach (float s in new[] { -1f, 1f })
                {
                    Place(lantern, walls, ground.At(gate - dir * 3f + along * (GATE_HALF + 1f) * s), Quaternion.identity, "Lantern_Gate");
                }
            }

            foreach (Vector2 corner in new[] { new Vector2(1f, 1f), new Vector2(1f, -1f), new Vector2(-1f, -1f), new Vector2(-1f, 1f) })
            {
                Place(pillar, walls, ground.At(town.Center + corner * TOWN_HALF), Quaternion.identity, "Pillar_Corner");
            }
        }

        // ------------------------------------------------------------------
        // 마을 확충 (10/22): VARCO 건물. 프리팹이 없으면 건너뜀 (집은 ProBuilder 상자로 대체)
        // half = 바닥 반 크기 (x 폭, z 깊이), 앞면 = +z
        // ------------------------------------------------------------------

        private const string TOWN_HOUSE_TILE = "TownHouseTile";
        private const string TOWN_COTTAGE = "TownCottage";
        private const string TOWN_GRANARY = "TownGranary";

        /// <summary>(프리팹, 마을 중심 기준 위치, 바닥 반 크기, 바라볼 방향: null = 가장 가까운 길)</summary>
        private static readonly (string prefab, Vector2 local, Vector2 half, float? yaw)[] TOWN_LANDMARKS =
        {
            ("TownBellTower", new Vector2(-9.6f, -26.3f), new Vector2(3.4f, 3.4f), null),   // 광장 남쪽 (200°)
            ("PropShrine", new Vector2(9.6f, 26.3f), new Vector2(2.5f, 2.3f), null),       // 광장 북쪽 (20°)
            ("PropWell", new Vector2(21.8f, 10.1f), new Vector2(1.3f, 1.3f), 0f),          // 광장 둘레 우물 3곳
            ("PropWell", new Vector2(-22.6f, -8.2f), new Vector2(1.3f, 1.3f), 0f),
            ("PropWell", new Vector2(8.2f, -22.6f), new Vector2(1.3f, 1.3f), 0f),
            ("TownInn", new Vector2(45f, 11f), new Vector2(5f, 5f), 180f),                 // 동쪽 큰길가
            ("TownInn", new Vector2(-45f, -11f), new Vector2(5f, 5f), 0f),                 // 서쪽 큰길가
            ("TownManorGate", new Vector2(45f, 45f), new Vector2(5f, 3.4f), 225f),         // 대각선 길 끝
            ("TownManorGate", new Vector2(-45f, -45f), new Vector2(5f, 3.4f), 45f),
        };

        private static GameObject TownPrefab(string name)
        {
            return AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/{name}.prefab");
        }

        private static void PlaceTownLandmarks(Room town, Ground ground, Transform root)
        {
            var group = new GameObject("Landmarks").transform;
            group.SetParent(root, false);
            int placed = 0;

            foreach (var landmark in TOWN_LANDMARKS)
            {
                GameObject prefab = TownPrefab(landmark.prefab);

                if (prefab == null)
                {
                    continue;
                }

                Vector2 world = town.Center + landmark.local;
                float yaw = landmark.yaw ?? NearestStreetYaw(landmark.local);
                Place(prefab, group, ground.At(world), Quaternion.Euler(0f, yaw, 0f), $"{landmark.prefab}_{++placed}");
                s_footprints.Add(new Footprint { Center = world, Half = landmark.half, Yaw = yaw });
            }
        }

        // 집: 격자 칸마다 길·광장·상점과 겹치지 않으면 배치, 가장 가까운 길을 바라봄 (90° 단위)
        // VARCO 민가(기와 55%·초가 35%)·창고(성벽 쪽만 10%), 없으면 ProBuilder 상자
        private static void PlaceTownHouses(LevelSpec spec, Room town, Ground ground, Transform root)
        {
            GameObject tile = TownPrefab(TOWN_HOUSE_TILE);
            GameObject cottage = TownPrefab(TOWN_COTTAGE);
            GameObject granary = TownPrefab(TOWN_GRANARY);
            var houses = new[]
            {
                (prefab: tile != null ? tile : EnsureHouse("TownHouseA", new Vector3(8f, 4.5f, 6f)), half: tile != null ? new Vector2(4f, 3.5f) : new Vector2(4f, 3f)),
                (prefab: cottage != null ? cottage : EnsureHouse("TownHouseB", new Vector3(6f, 3.5f, 5f)), half: cottage != null ? new Vector2(3f, 2.6f) : new Vector2(3f, 2.5f)),
            };
            var group = new GameObject("Houses").transform;
            group.SetParent(root, false);
            var rng = new Random(spec.Seed + 21);
            float limit = TOWN_HALF - 5f;
            int placed = 0;

            for (float z = -limit; z <= limit; z += HOUSE_CELL)
            {
                for (float x = -limit; x <= limit; x += HOUSE_CELL)
                {
                    var local = new Vector2(x + ((float)rng.NextDouble() - 0.5f) * 1.5f, z + ((float)rng.NextDouble() - 0.5f) * 1.5f);
                    double roll = rng.NextDouble();
                    var kind = houses[roll < 0.6 ? 0 : 1];
                    bool outer = Mathf.Max(Mathf.Abs(x), Mathf.Abs(z)) > 40f;

                    if (granary != null && outer && roll > 0.88)
                    {
                        kind = (granary, new Vector2(3.4f, 4f));
                    }

                    float clearance = kind.half.magnitude + 0.8f;

                    if (TownStreetDistance(local) < clearance || local.magnitude < PLAZA_RADIUS + 10f)
                    {
                        continue;
                    }

                    Vector2 world = town.Center + local;

                    // 다른 건물 바닥 사각형과 간격 (객잔·대문 같은 큰 건물도 겹치지 않게, 10/22)
                    if (s_footprints.Exists(f => f.Distance(world) < Mathf.Max(kind.half.x, kind.half.y) + 0.5f))
                    {
                        continue;
                    }

                    float yaw = NearestStreetYaw(local);
                    Place(kind.prefab, group, ground.At(world), Quaternion.Euler(0f, yaw, 0f), $"House_{++placed}");
                    s_footprints.Add(new Footprint { Center = world, Half = kind.half, Yaw = yaw });
                }
            }
        }

        // 가장 가까운 길 쪽을 바라보는 방향 (90° 단위로 맞춤)
        private static float NearestStreetYaw(Vector2 local)
        {
            float best = float.MaxValue;
            Vector2 target = Vector2.zero;

            foreach (Street street in TownStreets())
            {
                Vector2 ab = street.B - street.A;
                float t = Mathf.Clamp01(Vector2.Dot(local - street.A, ab) / ab.sqrMagnitude);
                Vector2 q = street.A + ab * t;
                float d = Vector2.Distance(local, q) - street.HalfWidth;

                if (d < best)
                {
                    best = d;
                    target = q;
                }
            }

            Vector2 dir = target - local;
            float yaw = Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg;
            return Mathf.Round(yaw / 90f) * 90f;
        }

        // 성벽 조각 (ProBuilder 돌 상자 6 × 2.6 × 1.2 + 위 테두리)
        private static GameObject EnsureTownWall()
        {
            string path = $"{FIELD_PREFABS}/TownWall.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                return existing;
            }

            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE_MATERIAL);
            Material roof = RoofMaterial();
            var root = new GameObject("TownWall");
            var body = UnityEngine.ProBuilder.ShapeGenerator.GenerateCube(UnityEngine.ProBuilder.PivotLocation.Center, new Vector3(WALL_SEGMENT, 2.6f, 1.2f));
            FinishMesh(body, "Body", root.transform, new Vector3(0f, 1.3f, 0f), Quaternion.identity, stone);
            var cap = UnityEngine.ProBuilder.ShapeGenerator.GenerateCube(UnityEngine.ProBuilder.PivotLocation.Center, new Vector3(WALL_SEGMENT, 0.35f, 1.7f));
            FinishMesh(cap, "Cap", root.transform, new Vector3(0f, 2.75f, 0f), Quaternion.identity, roof);

            ModelAssetUtility.EnsureFolder(FIELD_PREFABS);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        // ------------------------------------------------------------------
        // 메뉴: 마을만 다시 배치 (오픈 필드 전체 재생성 없이, 10/22)
        // ------------------------------------------------------------------

        [MenuItem("BrokenSovereign/Levels/마을 건물 다시 배치 (현재 씬)")]
        private static void RebuildTownMenu()
        {
            Terrain terrain = FindLevelTerrain();
            GameObject level = GameObject.Find("Level");

            if (terrain == null || level == null)
            {
                EditorUtility.DisplayDialog("마을 다시 배치", "현재 씬에 Level/Terrain이 없습니다. World 씬을 열고 실행하세요.", "확인");
                return;
            }

            if (!EditorUtility.DisplayDialog("마을 다시 배치",
                    "Level/Town(성벽·상점·NPC·집·랜드마크)을 지우고 다시 만든 뒤 NavMesh를 다시 굽습니다.\n마을 안에서 손으로 옮긴 오브젝트는 사라집니다.",
                    "진행", "취소"))
            {
                return;
            }

            try
            {
                Transform old = level.transform.Find("Town");

                if (old != null)
                {
                    Object.DestroyImmediate(old.gameObject);
                }

                var ground = new Ground(terrain);
                PlaceTown(WORLD, ground, level.transform);

                EditorUtility.DisplayProgressBar("마을 다시 배치", "NavMesh 굽기", 0.6f);

                foreach (NavMeshSurface surface in level.GetComponents<NavMeshSurface>())
                {
                    Object.DestroyImmediate(surface);
                }

                BakeNavMesh(WORLD, level);
                PlaceWaypoints(WORLD, ground);   // 웨이포인트 모형도 최신 프리팹으로

                EditorSceneManager.MarkSceneDirty(level.scene);
                EditorSceneManager.SaveScene(level.scene);
                Debug.Log($"LevelTools: 마을 다시 배치 (건물 자리 {s_footprints.Count}곳). 지도 그림의 건물 자리는 오픈 필드를 다시 생성할 때 갱신됩니다.");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------
        // 메뉴: 선택한 플레이어를 PlayerStart(월드 위치)로
        // ------------------------------------------------------------------

        [MenuItem("BrokenSovereign/Levels/선택한 플레이어를 시작 위치로")]
        private static void MovePlayerToStart()
        {
            PlayerController player = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PlayerController>()
                : null;
            GameObject start = GameObject.Find("PlayerStart");

            if (player == null || start == null)
            {
                EditorUtility.DisplayDialog("시작 위치로", "Hierarchy에서 플레이어를 선택하고, 씬에 PlayerStart가 있는지 확인하세요.", "확인");
                return;
            }

            Undo.RecordObject(player.transform, "Move Player To Start");
            float footOffset = PivotToFeet(player.gameObject);
            player.transform.position = start.transform.position + Vector3.up * (footOffset + 0.02f);

            if (player.GetComponent<UnityEngine.AI.NavMeshAgent>() is UnityEngine.AI.NavMeshAgent agent)
            {
                Undo.RecordObject(agent, "Move Player To Start");
                agent.baseOffset = footOffset;
            }
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log($"LevelTools: 플레이어를 PlayerStart로 이동 ({start.transform.position}). 씬을 저장하세요.");
        }
    }
}
