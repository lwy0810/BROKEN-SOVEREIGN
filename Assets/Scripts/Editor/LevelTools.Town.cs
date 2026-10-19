using System.Collections.Generic;
using BrokenSovereign.Map;
using BrokenSovereign.Player;
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

        // 집: 격자 칸마다 길·광장·상점과 겹치지 않으면 배치, 가장 가까운 길을 바라봄 (90° 단위)
        private static void PlaceTownHouses(LevelSpec spec, Room town, Ground ground, Transform root)
        {
            var houses = new[]
            {
                (prefab: EnsureHouse("TownHouseA", new Vector3(8f, 4.5f, 6f)), half: new Vector2(4f, 3f)),
                (prefab: EnsureHouse("TownHouseB", new Vector3(6f, 3.5f, 5f)), half: new Vector2(3f, 2.5f)),
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
                    var kind = houses[rng.NextDouble() < 0.55 ? 0 : 1];
                    float clearance = kind.half.magnitude + 0.8f;

                    if (TownStreetDistance(local) < clearance || local.magnitude < PLAZA_RADIUS + 10f)
                    {
                        continue;
                    }

                    Vector2 world = town.Center + local;

                    if (s_footprints.Exists(f => Vector2.Distance(f.Center, world) < 9f))
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
            player.transform.position = start.transform.position + Vector3.up * 0.05f;
            EditorSceneManager.MarkSceneDirty(player.gameObject.scene);
            Debug.Log($"LevelTools: 플레이어를 PlayerStart로 이동 ({start.transform.position}). 씬을 저장하세요.");
        }
    }
}
