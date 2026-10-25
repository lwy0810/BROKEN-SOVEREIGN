using System;
using System.IO;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using BrokenSovereign.Map;
using UnityEngine;
using UnityEngine.ProBuilder;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 맵 생성 도구 (GDD 13장). 메뉴: BrokenSovereign > Levels
    ///
    /// 오픈 필드(World): 1km 계곡 분지 한 장 (2026-10-19 변경, 참고: 티베트 고원 계곡). 가운데 마을, 남서쪽 구역 1,
    /// 북동쪽 구역 2(협곡 석판 다리), 산맥 안쪽 보스 제단(→ BossArena 씬). 길 주변은 초원 계곡, 그 밖은 높은 산, 가장자리는 산맥 벽.
    /// 여러 갈래 자갈 강 2줄기, 길이 지나는 곳만 여울. 세부는 LevelTools.Valley.cs
    /// 보스 방(BossArena): 구름 위 입구 섬 + 석판 다리 + 원형 아레나 (부유 섬 방식 유지).
    ///
    /// 지형 텍스처 레이어 6종(풀·돌바닥·흙길·바위·강 자갈·마른 풀)은 코드로 생성해 자동으로 칠함.
    /// 생성 후 세부는 Terrain 브러시로 다듬습니다. 다시 생성하면 다듬은 내용은 사라집니다.
    /// 결과: Scenes/Levels/{이름}.unity, Terrain/{이름}TerrainData.asset, NavMesh, 집 프리팹(Prefabs/Field/TownHouse*), 상점·NPC(TownSetupTools), 표시 오브젝트
    /// 좌표: m, x = 오른쪽, z = 앞 (쿼터뷰 화면 위쪽 = +x+z)
    /// </summary>
    public static partial class LevelTools
    {
        private const string SCENE_ROOT = "Assets/Scenes/Levels";
        private const string TERRAIN_ROOT = "Assets/Terrain";
        private const string LAYER_ROOT = TERRAIN_ROOT + "/Layers";
        private const string TERRAIN_MATERIAL = "Assets/Materials/Field/M_Terrain.mat";
        private const string ROOF_MATERIAL = "Assets/Materials/Field/M_TownRoof.mat";
        private const string STONE_MATERIAL = "Assets/Materials/Field/M_FieldStone.mat";
        private const string TERRAIN_SHADER = "Universal Render Pipeline/Terrain/Lit";
        private const string FIELD_PREFABS = "Assets/Prefabs/Field";
        private const string PROP_PREFABS = "Assets/Prefabs/Props";

        private const float BRIDGE_LENGTH = 6f;  // FieldBridge6 길이
        private const float PACK_SPACING = 40f;  // 긴 길 중간 적 무리 간격
        private const int TEXTURE_SIZE = 512;

        private const int GRASS = 0;
        private const int STONE = 1;
        private const int DIRT = 2;
        private const int ROCK = 3;

        private enum RoomKind
        {
            Town,   // 마을 (안전 지대, 플레이어 시작)
            Start,  // 플레이어 시작 (보스 방 입구)
            Combat, // 전투 방
            Event,  // 이벤트 지점 (직업 선택 등)
            Trial,  // 시련 방 (정예)
            Gate,   // 보스 제단 (→ BossArena)
            Arena,  // 보스 아레나
            Node,   // 길 꺾임 지점 (표시 없음)
            Side    // 곁길 막다른 방 (적 + 보상 지점)
        }

        private readonly struct Room
        {
            public readonly string Name;
            public readonly Vector2 Center;
            public readonly float Radius;
            public readonly RoomKind Kind;

            public Room(string name, float x, float z, float radius, RoomKind kind)
            {
                Name = name;
                Center = new Vector2(x, z);
                Radius = radius;
                Kind = kind;
            }

            public bool StoneFloor => Kind == RoomKind.Town || Kind == RoomKind.Combat || Kind == RoomKind.Trial ||
                Kind == RoomKind.Arena || Kind == RoomKind.Gate || Kind == RoomKind.Side;
        }

        private readonly struct Link
        {
            public readonly int From;
            public readonly int To;
            public readonly float Width;
            public readonly bool IsBridge;

            public Link(int from, int to, float width, bool isBridge = false)
            {
                From = from;
                To = to;
                Width = width;
                IsBridge = isBridge;
            }
        }

        private sealed class LevelSpec
        {
            public string Name;
            public string Title;
            public float Size;
            public int Seed;
            public Room[] Rooms;
            public Link[] Links;

            // 높이 (m)
            public float BaseDepth = 20f;       // 땅 밖 바닥 = -BaseDepth (구름 바다 -7m 아래)
            public float HeightRange = 30f;     // -BaseDepth ~ HeightRange - BaseDepth
            public float Rim = 16f;             // 걷는 곳 바깥 섬 테두리 폭 (대륙이 아닐 때)
            public float HillHeight = 5f;       // 걷는 곳 가장자리 언덕

            // 대륙 (오픈 필드)
            public bool Continent;
            public float ContinentRadius;       // 해안선 반지름
            public float MacroHeight;           // 대륙 큰 기복 0 ~ MacroHeight
            public float MountainHeight;        // 걷는 곳에서 먼 산줄기
            public float GorgeWidth = 30f;      // 다리 아래 협곡 반폭

            // 계곡 (오픈 필드, LevelTools.Valley.cs)
            public bool Valley;
            public River[] Rivers = Array.Empty<River>();
            public float ValleyWidth = 20f;           // 길 밖 초원 계곡 폭 (m)
            public float ValleyWidthVariation = 22f;  // 계곡 폭 노이즈 추가분
            public float FloorHeight = 12f;           // 계곡 바닥 기복 0 ~ FloorHeight
            public float MountainBase = 40f;          // 계곡 밖 산 기본 높이
            public float RingStart = 445f;            // 산맥 벽 시작 (맵 가장자리 거리 max(|x|,|z|))
            public float RingEnd = 500f;
            public float RingHeight = 170f;

            public int HeightRes => Size > 700f ? 2049 : Size > 250f ? 1025 : 513;
            public int AlphaRes => HeightRes - 1;
        }

        // ------------------------------------------------------------------
        // 레이아웃
        // ------------------------------------------------------------------

        // 오픈 필드: 가운데 마을, 남서 구역 1(범인 시련까지), 북동 구역 2(반신 시련, 보스 제단)
        private static readonly LevelSpec WORLD = new LevelSpec
        {
            Name = "World",
            Title = "오픈 필드",
            Size = 1000f,
            Seed = 777,
            BaseDepth = 8f,
            HeightRange = 420f,
            Valley = true,
            MountainHeight = 120f,
            Rivers = Array.Empty<River>(),   // 강 제거 (10/20 사용자 요청). 되살리려면 River(반폭, 꺾은선 점...) 추가
            Rooms = new[]
            {
                new Room("Town", 0f, 0f, 90f, RoomKind.Town),               // 0 마을 (성벽 ±62, LevelTools.Town)
                // 구역 1: 남서 (서문)
                new Room("Gate_W", -105f, 0f, 7f, RoomKind.Node),           // 1 서문 밖
                new Room("Z1_Tutorial", -120f, -100f, 22f, RoomKind.Combat), // 2 튜토리얼 전투
                new Room("Z1_JobSelect", -185f, -80f, 12f, RoomKind.Event), // 3 격의 잔재 → 직업 선택
                new Room("Z1_N2", -235f, -130f, 7f, RoomKind.Node),         // 4
                new Room("Z1_CombatA", -265f, -200f, 24f, RoomKind.Combat), // 5
                new Room("Z1_SideA", -345f, -150f, 17f, RoomKind.Side),     // 6 곁길
                new Room("Z1_N3", -230f, -265f, 7f, RoomKind.Node),         // 7
                new Room("Z1_CombatB", -160f, -290f, 24f, RoomKind.Combat), // 8
                new Room("Z1_N4", -95f, -255f, 7f, RoomKind.Node),          // 9
                new Room("Z1_CombatC", -55f, -330f, 23f, RoomKind.Combat),  // 10
                new Room("Z1_SideB", 10f, -395f, 17f, RoomKind.Side),       // 11 곁길
                new Room("Z1_Trial", -150f, -385f, 28f, RoomKind.Trial),    // 12 범인 시련 (정예)
                // 구역 2: 북동 (북문)
                new Room("Gate_N", 0f, 105f, 7f, RoomKind.Node),            // 13 북문 밖
                new Room("Z2_CombatD", 115f, 125f, 24f, RoomKind.Combat),   // 14
                new Room("Z2_N2", 175f, 105f, 7f, RoomKind.Node),           // 15
                new Room("Z2_CombatE", 245f, 150f, 24f, RoomKind.Combat),   // 16
                new Room("Z2_SideC", 320f, 85f, 17f, RoomKind.Side),        // 17 곁길
                new Room("Z2_CombatF", 265f, 240f, 22f, RoomKind.Combat),   // 18 협곡 건너
                new Room("Z2_N3", 195f, 300f, 7f, RoomKind.Node),           // 19
                new Room("Z2_CombatG", 125f, 350f, 24f, RoomKind.Combat),   // 20
                new Room("Z2_Trial", 210f, 400f, 28f, RoomKind.Trial),      // 21 반신 돌파 시련
                new Room("BossGate", 330f, 350f, 15f, RoomKind.Gate),       // 22 보스 제단 (해안 절벽)
                // 동문·남문 (지름길)
                new Room("Gate_E", 105f, 0f, 7f, RoomKind.Node),            // 23 동문 밖 → 구역 2
                new Room("Gate_S", 0f, -105f, 7f, RoomKind.Node),           // 24 남문 밖 → 구역 1
            },
            Links = new[]
            {
                new Link(0, 1, 10f), new Link(1, 2, 9f), new Link(2, 3, 8f), new Link(3, 4, 8f), new Link(4, 5, 9f),
                new Link(5, 6, 7f), new Link(5, 7, 9f), new Link(7, 8, 9f), new Link(8, 9, 9f), new Link(9, 10, 9f),
                new Link(10, 11, 7f), new Link(10, 12, 9f),
                new Link(0, 13, 10f), new Link(13, 14, 9f), new Link(14, 15, 9f), new Link(15, 16, 9f),
                new Link(16, 17, 7f), new Link(16, 18, 2.6f, true), new Link(18, 19, 9f), new Link(19, 20, 9f),
                new Link(20, 21, 9f), new Link(21, 22, 8f),
                new Link(0, 23, 9f), new Link(23, 15, 8f), new Link(0, 24, 9f), new Link(24, 9, 8f),
            },
        };

        private static readonly LevelSpec BOSS = new LevelSpec
        {
            Name = "BossArena",
            Title = "보스 방",
            Size = 180f,
            Seed = 303,
            Rooms = new[]
            {
                new Room("Entry", 0f, -62f, 8f, RoomKind.Start),
                new Room("Arena", 0f, 18f, 26f, RoomKind.Arena),            // 이랑진군
            },
            Links = new[] { new Link(0, 1, 2.6f, true) },
        };

        // ------------------------------------------------------------------
        // 메뉴
        // ------------------------------------------------------------------

        [MenuItem("BrokenSovereign/Levels/오픈 필드 생성 (마을·구역 1·구역 2)")]
        private static void BuildWorld() => BuildWithConfirm(WORLD);

        [MenuItem("BrokenSovereign/Levels/보스 방 생성")]
        private static void BuildBoss() => BuildWithConfirm(BOSS);

        [MenuItem("BrokenSovereign/Levels/지형 텍스처 다시 만들기")]
        private static void RebuildTextures()
        {
            EnsureLayers(true);
            Debug.Log($"LevelTools: 지형 텍스처·레이어 갱신 ({LAYER_ROOT})");
        }

        private static void BuildWithConfirm(LevelSpec spec)
        {
            string path = ScenePath(spec);

            if (File.Exists(path) && !EditorUtility.DisplayDialog($"{spec.Title} 생성",
                    $"{path}가 이미 있습니다.\n다시 만들면 지형·배치(Level·Scenery·GateEnemies)가 새로 만들어집니다.\n플레이어·HUD·카메라는 유지(없으면 ClickMoveTest에서 옮겨 옴)하고 시작 위치로 옮깁니다.", "다시 생성", "취소"))
            {
                return;
            }

            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            try
            {
                Build(spec);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        // 생성 결과 루트 (다시 생성할 때 이것만 지우고 새로 만듦)
        private static readonly string[] GENERATED_ROOTS = { "Level", "Scenery", "GateEnemies", "Waypoints" };

        /// <summary>
        /// 기존 씬이 있으면 열어서 생성 결과(GENERATED_ROOTS)만 지움 → 플레이어·HUD·카메라 등 직접 넣은 오브젝트 유지 (10/20).
        /// 없으면 빈 씬으로 시작.
        /// </summary>
        private static void OpenOrCreateLevelScene(LevelSpec spec)
        {
            string path = ScenePath(spec);

            if (!File.Exists(path))
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                return;
            }

            UnityEngine.SceneManagement.Scene scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);

            foreach (GameObject root in scene.GetRootGameObjects())
            {
                if (Array.IndexOf(GENERATED_ROOTS, root.name) >= 0)
                {
                    Object.DestroyImmediate(root);
                }
            }
        }

        private static string ScenePath(LevelSpec spec) => $"{SCENE_ROOT}/{spec.Name}.unity";

        // ------------------------------------------------------------------
        // 생성
        // ------------------------------------------------------------------

        private static void Build(LevelSpec spec)
        {
            OpenOrCreateLevelScene(spec);
            FieldTools.PrepareAtmosphere(spec.Size * 1.8f);

            if (spec.Valley)
            {
                // 산맥 분지: 구름 바다 없음 (하늘 구름만)
                GameObject cloudSea = GameObject.Find("CloudSea");

                if (cloudSea != null)
                {
                    Object.DestroyImmediate(cloudSea);
                }
            }

            TerrainLayer[] layers = EnsureLayers(true, spec.Size);
            var level = new GameObject("Level");
            ITerrainShape shape = spec.Valley ? new ValleyShape(spec) : new IslandShape(spec);

            Terrain terrain = BuildTerrain(spec, shape, layers, level.transform);
            var ground = new Ground(terrain);
            PlaceBridges(spec, ground, level.transform);
            PlaceProps(spec, ground, level.transform);
            PlaceTown(spec, ground, level.transform);
            PlaceMarkers(spec, ground, level.transform);
            PlacePackMarkers(spec, ground, level.transform);
            PlaceGateEnemies(spec, ground);   // 마을 성문 밖 적 무리 (LevelTools.Roads)
            EnsureEnemyMapMarkers();

            if (shape is ValleyShape valley)
            {
                BuildValleyExtras(spec, valley, terrain, ground, level);

                // 실사 지형 패키지: 패키지 예제와 같은 하늘·안개·햇빛 + 주변 8칸 지형(맵 끝이 보이지 않게)
                if (valley.UsesMacro)
                {
                    ApplyMacroLook(spec, terrain);

                    // 실사 반복 텍스처(Assets/Terrain/Ground)가 있으면 지면·풀·분위기 교체 (LevelTools.Ground)
                    if (HasGroundTextures)
                    {
                        ApplyGroundLook(terrain);
                    }
                }
            }

            if (spec.Valley)
            {
                PlaceWaypoints(spec, ground);   // LevelTools.Waypoints
            }

            EditorUtility.DisplayProgressBar(spec.Title, "NavMesh 굽기", 0.95f);
            BakeNavMesh(spec, level);

            Room start = Array.Find(spec.Rooms, r => r.Kind == RoomKind.Town || r.Kind == RoomKind.Start);
            // 플레이어·HUD·카메라: 없으면 기준 씬에서 옮겨 오고, 시작 위치로 (LevelTools.Player)
            EnsurePlayerRig(ground.At(start.Center));

            if (Camera.main == null)
            {
                FieldTools.CreateQuarterViewCamera(ground.At(start.Center));
            }

            ModelAssetUtility.EnsureFolder(SCENE_ROOT);
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), ScenePath(spec));
            AssetDatabase.SaveAssets();
            Debug.Log($"LevelTools: {spec.Title} 생성 ({ScenePath(spec)}, {spec.Size}m, 방 {spec.Rooms.Length}개)");
        }

        /// <summary>지형 높이 조회 (배치·다리 높이용)</summary>
        private readonly struct Ground
        {
            private readonly Terrain _terrain;

            public Ground(Terrain terrain)
            {
                _terrain = terrain;
            }

            public Vector3 At(Vector2 p)
            {
                var world = new Vector3(p.x, 0f, p.y);
                world.y = _terrain.SampleHeight(world) + _terrain.transform.position.y;
                return world;
            }
        }

        private static Terrain BuildTerrain(LevelSpec spec, ITerrainShape shape, TerrainLayer[] layers, Transform parent)
        {
            ModelAssetUtility.EnsureFolder(TERRAIN_ROOT);
            string dataPath = $"{TERRAIN_ROOT}/{spec.Name}TerrainData.asset";
            AssetDatabase.DeleteAsset(dataPath);

            int res = spec.HeightRes;
            var data = new TerrainData
            {
                heightmapResolution = res,
                alphamapResolution = spec.AlphaRes,
            };
            data.size = new Vector3(spec.Size, spec.HeightRange, spec.Size);
            data.terrainLayers = layers;

            float half = spec.Size * 0.5f;
            var heights = new float[res, res];

            for (int z = 0; z < res; z++)
            {
                if (z % 64 == 0)
                {
                    EditorUtility.DisplayProgressBar(spec.Title, "지형 높이 계산", 0.5f * z / res);
                }

                for (int x = 0; x < res; x++)
                {
                    float wx = -half + spec.Size * x / (res - 1);
                    float wz = -half + spec.Size * z / (res - 1);
                    heights[z, x] = Mathf.Clamp01((shape.Height(wx, wz) + spec.BaseDepth) / spec.HeightRange);
                }
            }

            data.SetHeights(0, 0, heights);
            data.SetAlphamaps(0, 0, Paint(spec, shape, data, layers.Length));
            AssetDatabase.CreateAsset(data, dataPath);

            GameObject go = Terrain.CreateTerrainGameObject(data);
            go.name = "Terrain";
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(-half, -spec.BaseDepth, -half);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic | StaticEditorFlags.NavigationStatic);

            Terrain terrain = go.GetComponent<Terrain>();
            terrain.materialTemplate = TerrainMaterial();
            terrain.basemapDistance = 300f;
            terrain.heightmapPixelError = 3f;
            terrain.Flush();
            return terrain;
        }

        // 길·방 바닥·경사에 따라 텍스처 가중치 계산 (합 = 1). 레이어 규칙은 모양(IslandShape·ValleyShape)이 결정
        private static float[,,] Paint(LevelSpec spec, ITerrainShape shape, TerrainData data, int layerCount)
        {
            int res = spec.AlphaRes;
            var map = new float[res, res, layerCount];
            var weights = new float[layerCount];
            float half = spec.Size * 0.5f;

            for (int z = 0; z < res; z++)
            {
                if (z % 64 == 0)
                {
                    EditorUtility.DisplayProgressBar(spec.Title, "텍스처 칠하기", 0.5f + 0.25f * z / res);
                }

                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;
                    float v = (z + 0.5f) / res;
                    float steep = data.GetSteepness(u, v);
                    float worldY = data.GetInterpolatedHeight(u, v) - spec.BaseDepth;
                    shape.Weights(-half + spec.Size * u, -half + spec.Size * v, worldY, steep, weights);

                    for (int l = 0; l < layerCount; l++)
                    {
                        map[z, x, l] = weights[l];
                    }
                }
            }

            return map;
        }

        // 다리: 양 끝을 방 안쪽 2m까지 겹쳐 NavMesh가 끊기지 않게 하고, 양 끝 높이 차는 기울여 잇기
        private static void PlaceBridges(LevelSpec spec, Ground ground, Transform parent)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>($"{FIELD_PREFABS}/FieldBridge6.prefab");

            if (prefab == null)
            {
                Debug.LogWarning("LevelTools: FieldBridge6 프리팹 없음. Field > 필드 킷 생성을 먼저 실행하세요.");
                return;
            }

            var root = new GameObject("Bridges").transform;
            root.SetParent(parent, false);

            foreach (Link link in spec.Links)
            {
                if (!link.IsBridge)
                {
                    continue;
                }

                Room a = spec.Rooms[link.From];
                Room b = spec.Rooms[link.To];
                Vector2 dir = (b.Center - a.Center).normalized;
                Vector3 p0 = ground.At(a.Center + dir * (a.Radius - 2f));
                Vector3 p1 = ground.At(b.Center - dir * (b.Radius - 2f));
                float length = Vector3.Distance(p0, p1);
                int count = Mathf.CeilToInt(length / BRIDGE_LENGTH);
                Quaternion rotation = Quaternion.LookRotation(p1 - p0);

                for (int i = 0; i < count; i++)
                {
                    Vector3 p = Vector3.Lerp(p0, p1, (i + 0.5f) / count);
                    Place(prefab, root, p, rotation, $"Bridge_{a.Name}_{b.Name}_{i}");
                }
            }
        }

        private static void PlaceProps(LevelSpec spec, Ground ground, Transform parent)
        {
            GameObject lantern = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStoneLantern.prefab");
            GameObject pillar = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropStonePillar.prefab");
            GameObject paifang = AssetDatabase.LoadAssetAtPath<GameObject>($"{PROP_PREFABS}/PropPaifang.prefab");
            var root = new GameObject("Props").transform;
            root.SetParent(parent, false);

            foreach (Link link in spec.Links)
            {
                Room from = spec.Rooms[link.From];
                Room to = spec.Rooms[link.To];
                Vector2 dir = (to.Center - from.Center).normalized;

                // 방 입구 양옆 석등 (들어오는 방향 기준)
                if (to.Kind == RoomKind.Arena || to.Kind == RoomKind.Node || to.Kind == RoomKind.Gate)
                {
                    continue;
                }

                Vector2 side = new Vector2(dir.y, -dir.x);
                Vector2 entrance = to.Center - dir * (to.Radius - 1.5f);

                foreach (float s in new[] { -1f, 1f })
                {
                    Vector2 p = entrance + side * (link.Width * 0.5f + 1.5f) * s;
                    Place(lantern, root, ground.At(p), Quaternion.identity, $"Lantern_{to.Name}");
                }
            }

            foreach (Room room in spec.Rooms)
            {
                switch (room.Kind)
                {
                    case RoomKind.Trial:
                        Ring(pillar, root, ground, room, 6, room.Radius - 1.5f, $"Pillar_{room.Name}");
                        break;
                    case RoomKind.Arena:
                        Ring(pillar, root, ground, room, 10, room.Radius - 1f, $"Pillar_{room.Name}");
                        Place(paifang, root, ground.At(room.Center - IncomingDirection(spec, room) * (room.Radius - 3f)),
                            Yaw(IncomingDirection(spec, room)), $"Paifang_{room.Name}");
                        break;
                    case RoomKind.Event:
                        Ring(lantern, root, ground, room, 4, room.Radius * 0.6f, $"Lantern_{room.Name}");
                        break;
                    case RoomKind.Gate:
                        // 보스 제단: 석주 원형 + 들어오는 방향 패루
                        Ring(pillar, root, ground, room, 4, room.Radius * 0.6f, $"Pillar_{room.Name}");
                        Place(paifang, root, ground.At(room.Center), Yaw(IncomingDirection(spec, room)), $"Paifang_{room.Name}");
                        break;
                }
            }
        }

        // 시작·적·정예·보스·이벤트·마을 NPC 위치 표시 (레벨 흐름 연결 때 사용)
        private static void PlaceMarkers(LevelSpec spec, Ground ground, Transform parent)
        {
            var root = new GameObject("Markers").transform;
            root.SetParent(parent, false);

            foreach (Room room in spec.Rooms)
            {
                if (room.Kind == RoomKind.Node)
                {
                    continue;
                }

                var group = new GameObject($"Room_{room.Name}").transform;
                group.SetParent(root, false);
                group.position = ground.At(room.Center);

                switch (room.Kind)
                {
                    case RoomKind.Town:
                        Marker(group, ground, "PlayerStart", room, Vector3.zero, 3);
                        // 웨이포인트는 LevelTools.Waypoints가 실제 모형으로 배치 (10/22)
                        break;
                    case RoomKind.Start:
                        Marker(group, ground, "PlayerStart", room, Vector3.zero, 3);
                        break;
                    case RoomKind.Combat:
                        SpawnRing(group, ground, room, Mathf.Clamp(Mathf.RoundToInt(room.Radius / 3.5f), 3, 7));
                        break;
                    case RoomKind.Side:
                        SpawnRing(group, ground, room, 4);
                        Marker(group, ground, "RewardSpot", room, Vector3.zero, 2);
                        break;
                    case RoomKind.Trial:
                        AddMapMarker(Marker(group, ground, "EliteSpawn", room, Vector3.zero, 4), MapMarkerKind.Trial);
                        Marker(group, ground, "EnemySpawn_1", room, Polar(room.Radius * 0.5f, 60f), 6);
                        Marker(group, ground, "EnemySpawn_2", room, Polar(room.Radius * 0.5f, 240f), 6);
                        break;
                    case RoomKind.Arena:
                        Marker(group, ground, "BossSpawn", room, Vector3.zero, 7);
                        break;
                    case RoomKind.Gate:
                        AddMapMarker(Marker(group, ground, "BossPortal", room, Vector3.zero, 7), MapMarkerKind.Boss);
                        break;
                    case RoomKind.Event:
                        AddMapMarker(Marker(group, ground, "JobSelectEvent", room, Vector3.zero, 5), MapMarkerKind.Event);
                        break;
                }
            }
        }

        private static void SpawnRing(Transform group, Ground ground, Room room, int count)
        {
            for (int i = 0; i < count; i++)
            {
                Marker(group, ground, $"EnemySpawn_{i + 1}", room, Polar(room.Radius * 0.5f, 360f * i / count + 30f), 6);
            }
        }

        // 긴 길 중간 적 무리 (양 끝 방 근처 제외, PACK_SPACING 간격)
        private static void PlacePackMarkers(LevelSpec spec, Ground ground, Transform parent)
        {
            var root = new GameObject("Corridors").transform;
            root.SetParent(parent, false);

            foreach (Link link in spec.Links)
            {
                Room a = spec.Rooms[link.From];
                Room b = spec.Rooms[link.To];

                if (link.IsBridge || a.Kind == RoomKind.Town || b.Kind == RoomKind.Town)
                {
                    continue;
                }

                Vector2 dir = (b.Center - a.Center).normalized;
                Vector2 p0 = a.Center + dir * (a.Radius + 8f);
                Vector2 p1 = b.Center - dir * (b.Radius + 8f);
                float length = Vector2.Dot(p1 - p0, dir);
                int count = Mathf.FloorToInt(length / PACK_SPACING);

                for (int i = 0; i < count; i++)
                {
                    Vector2 p = p0 + dir * (length * (i + 1f) / (count + 1f));
                    var marker = new GameObject($"PackSpawn_{a.Name}_{b.Name}_{i + 1}");
                    marker.transform.SetParent(root, false);
                    marker.transform.position = ground.At(p);
                    SetIcon(marker, 6);
                }
            }
        }

        private static void BakeNavMesh(LevelSpec spec, GameObject level)
        {
            NavMeshSurface surface = level.AddComponent<NavMeshSurface>();
            surface.collectObjects = CollectObjects.Children;
            surface.useGeometry = UnityEngine.AI.NavMeshCollectGeometry.PhysicsColliders;
            surface.BuildNavMesh();

            string folder = $"{SCENE_ROOT}/{spec.Name}";
            ModelAssetUtility.EnsureFolder(folder);
            string path = $"{folder}/NavMesh-Level.asset";
            AssetDatabase.DeleteAsset(path);

            if (surface.navMeshData != null)
            {
                AssetDatabase.CreateAsset(surface.navMeshData, path);
            }
        }

        // ------------------------------------------------------------------
        // 마을 집 (ProBuilder 임시 건물: 돌 몸체 + 사각뿔 기와 지붕). VARCO 모델 확보 후 교체
        // ------------------------------------------------------------------

        private static GameObject EnsureHouse(string name, Vector3 body)
        {
            string path = $"{FIELD_PREFABS}/{name}.prefab";
            GameObject existing = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (existing != null)
            {
                return existing;
            }

            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE_MATERIAL);
            Material roof = RoofMaterial();
            var root = new GameObject(name);

            ProBuilderMesh walls = ShapeGenerator.GenerateCube(PivotLocation.Center, body);
            FinishMesh(walls, "Walls", root.transform, new Vector3(0f, body.y * 0.5f, 0f), Quaternion.identity, stone);

            // 사각뿔: 반지름 = 대각선 절반 + 처마, 45° 돌려 벽과 모서리 맞춤
            float radius = Mathf.Sqrt(body.x * body.x + body.z * body.z) * 0.5f + 1.2f;
            ProBuilderMesh top = ShapeGenerator.GenerateCone(PivotLocation.Center, radius, body.y * 0.7f, 4);
            FinishMesh(top, "Roof", root.transform, new Vector3(0f, body.y + body.y * 0.35f, 0f), Quaternion.Euler(0f, 45f, 0f), roof);

            ModelAssetUtility.EnsureFolder(FIELD_PREFABS);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void FinishMesh(ProBuilderMesh mesh, string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.transform.localRotation = rotation;
            mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();
            MeshCollider collider = mesh.gameObject.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh.GetComponent<MeshFilter>().sharedMesh;
            GameObjectUtility.SetStaticEditorFlags(mesh.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static Material RoofMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(ROOF_MATERIAL);

            if (material == null)
            {
                material = new Material(Shader.Find(ModelAssetUtility.URP_LIT));
                AssetDatabase.CreateAsset(material, ROOF_MATERIAL);
            }

            material.SetColor("_BaseColor", new Color(0.24f, 0.36f, 0.42f)); // 청록 기와
            material.SetFloat("_Smoothness", 0.45f);
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------
        // 땅 모양 (높이·바닥 종류)
        // ------------------------------------------------------------------

        private sealed class IslandShape : ITerrainShape
        {
            private readonly LevelSpec _spec;
            private readonly float _offset;

            public IslandShape(LevelSpec spec)
            {
                _spec = spec;
                _offset = spec.Seed * 13.37f;
            }

            /// <summary>월드 높이(m). 걷는 곳 = 대륙 기복을 따라 평탄, 그 밖은 언덕·산줄기, 땅 밖은 -BaseDepth</summary>
            public float Height(float x, float z)
            {
                var p = new Vector2(x, z);
                float walk = 0f;
                float nearest = float.MaxValue;
                float corridor = 0f;
                float gorge = 0f;

                foreach (Room room in _spec.Rooms)
                {
                    float d = Vector2.Distance(p, room.Center) - room.Radius;
                    walk = Mathf.Max(walk, 1f - SmoothStep(-1f, 1f, d));
                    nearest = Mathf.Min(nearest, d);
                }

                foreach (Link link in _spec.Links)
                {
                    float d = SegmentDistance(p, _spec.Rooms[link.From].Center, _spec.Rooms[link.To].Center) - link.Width * 0.5f;

                    if (link.IsBridge)
                    {
                        corridor = Mathf.Max(corridor, 1f - SmoothStep(0f, 2f, d));
                        gorge = Mathf.Max(gorge, 1f - SmoothStep(_spec.GorgeWidth * 0.6f, _spec.GorgeWidth, d));
                        continue;
                    }

                    walk = Mathf.Max(walk, 1f - SmoothStep(-1f, 1f, d));
                    nearest = Mathf.Min(nearest, d);
                }

                float macro = 0f;
                float mountain = 0f;
                float land;
                float cut = _spec.Continent ? gorge : corridor;

                if (_spec.Continent)
                {
                    // 대륙: 큰 기복(0 ~ MacroHeight) + 걷는 곳에서 먼 산줄기, 해안선은 노이즈로 불규칙
                    macro = Fbm(x * 0.0035f, z * 0.0035f, 3) * _spec.MacroHeight;
                    mountain = Ridge(x * 0.008f, z * 0.008f) * _spec.MountainHeight * SmoothStep(8f, 50f, nearest);
                    float coast = (Mathf.PerlinNoise(x * 0.006f + _offset, z * 0.006f - _offset) - 0.5f) * 70f;
                    float continent = 1f - SmoothStep(_spec.ContinentRadius - 60f, _spec.ContinentRadius, p.magnitude + coast);
                    float rooms = 1f - SmoothStep(_spec.Rim * 0.45f, _spec.Rim, nearest);
                    land = Mathf.Max(continent, rooms);
                }
                else
                {
                    float coast = (Mathf.PerlinNoise(x * 0.035f + _offset, z * 0.035f + _offset) - 0.5f) * 9f;
                    land = 1f - SmoothStep(_spec.Rim * 0.45f, _spec.Rim, nearest + coast);
                }

                land *= 1f - cut * (1f - walk);

                float hill = Fbm(x * 0.04f, z * 0.04f, 3) * _spec.HillHeight * SmoothStep(0f, 6f, nearest) * (1f - corridor) + 0.2f;
                float floor = (Fbm(x * 0.15f, z * 0.15f, 2) - 0.5f) * 0.25f;
                float top = macro + Mathf.Lerp(hill + mountain, floor, walk);
                return Mathf.Lerp(-_spec.BaseDepth, top, Mathf.Pow(land, 0.7f));
            }

            /// <summary>부유 섬: 풀 + 길·돌바닥, 가파르거나 구름 아래면 바위 (자갈·마른 풀 없음)</summary>
            public void Weights(float x, float z, float worldY, float steep, float[] w)
            {
                float rock = Mathf.Max(SmoothStep(28f, 40f, steep), 1f - SmoothStep(-3f, -0.8f, worldY));
                Surface(x, z, out float stone, out float dirt);
                float grass = (1f - stone) * (1f - dirt);
                dirt *= 1f - stone;
                Array.Clear(w, 0, w.Length);
                w[GRASS] = grass * (1f - rock);
                w[STONE] = stone * (1f - rock);
                w[DIRT] = dirt * (1f - rock);
                w[ROCK] = rock;
            }

            /// <summary>돌바닥(마을·전투·시련·아레나·제단·곁길)과 흙길(길·이벤트) 가중치</summary>
            public void Surface(float x, float z, out float stone, out float dirt)
            {
                var p = new Vector2(x, z);
                float edge = (Mathf.PerlinNoise(x * 0.3f + _offset, z * 0.3f) - 0.5f) * 1.5f;
                stone = 0f;
                dirt = 0f;

                foreach (Room room in _spec.Rooms)
                {
                    float d = Vector2.Distance(p, room.Center) - room.Radius + edge;

                    if (room.StoneFloor)
                    {
                        stone = Mathf.Max(stone, 1f - SmoothStep(-2.5f, -0.5f, d));
                    }
                    else
                    {
                        dirt = Mathf.Max(dirt, 1f - SmoothStep(-2f, 0f, d));
                    }
                }

                foreach (Link link in _spec.Links)
                {
                    if (link.IsBridge)
                    {
                        continue;
                    }

                    float d = SegmentDistance(p, _spec.Rooms[link.From].Center, _spec.Rooms[link.To].Center) - link.Width * 0.5f + edge;
                    dirt = Mathf.Max(dirt, 1f - SmoothStep(-0.5f, 1f, d));
                }
            }

            private float Fbm(float x, float z, int octaves)
            {
                float sum = 0f;
                float amp = 0.5f;
                float total = 0f;

                for (int i = 0; i < octaves; i++)
                {
                    sum += Mathf.PerlinNoise(x + _offset + i * 31.7f, z - _offset + i * 17.3f) * amp;
                    total += amp;
                    x *= 2f;
                    z *= 2f;
                    amp *= 0.5f;
                }

                return sum / total;
            }

            // 능선 노이즈 (0 ~ 1): 날카로운 산등성이
            private float Ridge(float x, float z)
            {
                float sum = 0f;
                float amp = 0.6f;
                float total = 0f;

                for (int i = 0; i < 3; i++)
                {
                    float n = 1f - Mathf.Abs(Mathf.PerlinNoise(x + _offset * 0.5f + i * 11.1f, z + i * 7.7f) * 2f - 1f);
                    sum += n * n * amp;
                    total += amp;
                    x *= 2.1f;
                    z *= 2.1f;
                    amp *= 0.45f;
                }

                return sum / total;
            }
        }

        // ------------------------------------------------------------------
        // 텍스처 레이어 (코드 생성, 이음새 없는 반복)
        // ------------------------------------------------------------------

        // 실사 지형 패키지가 있으면 7번째 레이어(MACRO, 패키지 색·노멀) 추가 + 풀·흙·바위·자갈에 미세 굴곡 노멀 (LevelTools.Macro)
        private static TerrainLayer[] EnsureLayers(bool rebuild, float macroSize = 1000f)
        {
            ModelAssetUtility.EnsureFolder(LAYER_ROOT);
            Texture2D detail = MacroDetailNormal();
            var layers = new System.Collections.Generic.List<TerrainLayer>
            {
                EnsureLayer("Grass", 8f, 0.1f, rebuild, GrassTexture, detail),
                EnsureLayer("Stone", 6f, 0.3f, rebuild, StoneTexture),
                EnsureLayer("Dirt", 5f, 0.05f, rebuild, DirtTexture, detail),
                EnsureLayer("Rock", 10f, 0.15f, rebuild, RockTexture, detail),
                EnsureLayer("Gravel", 6f, 0.2f, rebuild, GravelTexture, detail),
                EnsureLayer("DryGrass", 9f, 0.05f, rebuild, DryGrassTexture, detail),
            };
            TerrainLayer macro = EnsureMacroLayer(macroSize);

            if (macro != null)
            {
                layers.Add(macro);
            }

            return layers.ToArray();
        }

        private static TerrainLayer EnsureLayer(string name, float tile, float smoothness, bool rebuild, Func<Color[]> generator, Texture2D normal = null)
        {
            string texturePath = $"{LAYER_ROOT}/T_Terrain{name}.png";
            string layerPath = $"{LAYER_ROOT}/TL_{name}.terrainlayer";

            if (rebuild || !File.Exists(texturePath))
            {
                var texture = new Texture2D(TEXTURE_SIZE, TEXTURE_SIZE, TextureFormat.RGB24, false);
                texture.SetPixels(generator());
                File.WriteAllBytes(texturePath, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(texturePath);

                if (AssetImporter.GetAtPath(texturePath) is TextureImporter importer)
                {
                    importer.wrapMode = TextureWrapMode.Repeat;
                    importer.sRGBTexture = true;
                    importer.maxTextureSize = 1024;
                    importer.SaveAndReimport();
                }
            }

            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);

            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            layer.normalMapTexture = normal;
            layer.normalScale = 0.5f;
            layer.tileSize = new Vector2(tile, tile);
            layer.smoothness = smoothness;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static Material TerrainMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(TERRAIN_MATERIAL);

            if (material == null)
            {
                material = new Material(Shader.Find(TERRAIN_SHADER));
                ModelAssetUtility.EnsureFolder(Path.GetDirectoryName(TERRAIN_MATERIAL)?.Replace('\\', '/'));
                AssetDatabase.CreateAsset(material, TERRAIN_MATERIAL);
            }

            // 레이어 노멀맵 사용 (셰이더 기능 키워드는 인스펙터를 열어야 켜지므로 코드에서 켬)
            if (!material.IsKeywordEnabled("_NORMALMAP"))
            {
                material.EnableKeyword("_NORMALMAP");
                EditorUtility.SetDirty(material);
            }

            return material;
        }

        // 고원 초원: 진초록 풀 + 노란 들꽃점 (2026-10-19 계곡 지형으로 색 변경)
        private static Color[] GrassTexture()
        {
            float[] large = TileFbm(8, 4, 11);
            float[] fine = TileFbm(64, 2, 12);
            var rng = new Random(13);
            var dark = new Color(0.24f, 0.4f, 0.14f);
            var light = new Color(0.45f, 0.6f, 0.24f);
            var flower = new Color(0.95f, 0.88f, 0.45f);
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = Color.Lerp(dark, light, large[i]) * (0.88f + 0.24f * fine[i]);
                pixels[i] = rng.NextDouble() < 0.0015 ? flower : c;
            }

            return pixels;
        }

        // 흰 돌바닥: 1.5m 판석, 줄눈, 판마다 색 차이, 엇갈린 줄
        private static Color[] StoneTexture()
        {
            const int tiles = 4;
            const int tilePx = TEXTURE_SIZE / tiles;
            const int grout = 3;
            float[] wear = TileFbm(16, 4, 21);
            var rng = new Random(22);
            var tint = new float[tiles * tiles];

            for (int i = 0; i < tint.Length; i++)
            {
                tint[i] = 0.92f + (float)rng.NextDouble() * 0.12f;
            }

            var stone = new Color(0.88f, 0.85f, 0.80f);
            var groutColor = new Color(0.58f, 0.54f, 0.54f);
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                int row = y / tilePx;
                int shift = row % 2 == 1 ? tilePx / 2 : 0;

                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    int sx = (x + shift) % TEXTURE_SIZE;
                    int col = sx / tilePx;
                    bool isGrout = sx % tilePx < grout || y % tilePx < grout;
                    int i = y * TEXTURE_SIZE + x;
                    Color c = isGrout ? groutColor : stone * tint[row * tiles + col];
                    pixels[i] = c * (0.9f + 0.15f * wear[i]);
                }
            }

            return pixels;
        }

        // 따뜻한 베이지 흙길 + 자갈
        private static Color[] DirtTexture()
        {
            float[] large = TileFbm(8, 4, 31);
            float[] fine = TileFbm(64, 2, 32);
            var rng = new Random(33);
            var dark = new Color(0.60f, 0.50f, 0.41f);
            var light = new Color(0.76f, 0.66f, 0.54f);
            var pebble = new Color(0.84f, 0.80f, 0.74f);
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            for (int i = 0; i < pixels.Length; i++)
            {
                Color c = Color.Lerp(dark, light, large[i]) * (0.9f + 0.2f * fine[i]);
                pixels[i] = rng.NextDouble() < 0.004 ? pebble : c;
            }

            return pixels;
        }

        // 회갈색 산 바위, 결 무늬 (2026-10-19 계곡 지형으로 색 변경)
        private static Color[] RockTexture()
        {
            float[] large = TileFbm(4, 5, 41);
            float[] fine = TileFbm(32, 3, 42);
            var dark = new Color(0.38f, 0.35f, 0.32f);
            var light = new Color(0.66f, 0.62f, 0.56f);
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            for (int y = 0; y < TEXTURE_SIZE; y++)
            {
                for (int x = 0; x < TEXTURE_SIZE; x++)
                {
                    int i = y * TEXTURE_SIZE + x;
                    float strata = 0.5f + 0.5f * Mathf.Sin((y / (float)TEXTURE_SIZE * 6f + large[i] * 1.5f) * Mathf.PI * 2f);
                    float t = Mathf.Clamp01(large[i] * 0.7f + strata * 0.3f);
                    pixels[i] = Color.Lerp(dark, light, t) * (0.85f + 0.3f * fine[i]);
                }
            }

            return pixels;
        }

        // 강 자갈: 밝은 회백색 자갈 + 젖은 모래
        private static Color[] GravelTexture()
        {
            float[] large = TileFbm(8, 3, 51);
            float[] pebbles = TileFbm(48, 2, 52);
            var rng = new Random(53);
            var sand = new Color(0.72f, 0.7f, 0.64f);
            var light = new Color(0.88f, 0.86f, 0.8f);
            var dark = new Color(0.52f, 0.5f, 0.47f);
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            for (int i = 0; i < pixels.Length; i++)
            {
                float p = pebbles[i];
                Color c = Color.Lerp(sand, light, large[i]);
                c = p > 0.62f ? Color.Lerp(c, light, (p - 0.62f) * 3f) : p < 0.35f ? Color.Lerp(c, dark, (0.35f - p) * 1.5f) : c;
                pixels[i] = rng.NextDouble() < 0.01 ? dark : c;
            }

            return pixels;
        }

        // 마른 풀 경사: 올리브·황갈색 얼룩
        private static Color[] DryGrassTexture()
        {
            float[] large = TileFbm(6, 4, 61);
            float[] fine = TileFbm(64, 2, 62);
            var olive = new Color(0.46f, 0.47f, 0.28f);
            var tan = new Color(0.62f, 0.56f, 0.38f);
            var pixels = new Color[TEXTURE_SIZE * TEXTURE_SIZE];

            for (int i = 0; i < pixels.Length; i++)
            {
                pixels[i] = Color.Lerp(olive, tan, large[i]) * (0.85f + 0.3f * fine[i]);
            }

            return pixels;
        }

        /// <summary>이음새 없이 반복되는 값 노이즈 fBm (0 ~ 1). basePeriod = 가장 큰 무늬 개수</summary>
        private static float[] TileFbm(int basePeriod, int octaves, int seed)
        {
            var rng = new Random(seed);
            var result = new float[TEXTURE_SIZE * TEXTURE_SIZE];
            float amp = 0.5f;
            float total = 0f;

            for (int o = 0; o < octaves; o++)
            {
                int period = basePeriod << o;
                var grid = new float[period * period];

                for (int i = 0; i < grid.Length; i++)
                {
                    grid[i] = (float)rng.NextDouble();
                }

                for (int y = 0; y < TEXTURE_SIZE; y++)
                {
                    float gy = y * (float)period / TEXTURE_SIZE;
                    int y0 = (int)gy;
                    float ty = Smooth(gy - y0);
                    int ya = y0 % period;
                    int yb = (y0 + 1) % period;

                    for (int x = 0; x < TEXTURE_SIZE; x++)
                    {
                        float gx = x * (float)period / TEXTURE_SIZE;
                        int x0 = (int)gx;
                        float tx = Smooth(gx - x0);
                        int xa = x0 % period;
                        int xb = (x0 + 1) % period;
                        float top = Mathf.Lerp(grid[ya * period + xa], grid[ya * period + xb], tx);
                        float bottom = Mathf.Lerp(grid[yb * period + xa], grid[yb * period + xb], tx);
                        result[y * TEXTURE_SIZE + x] += Mathf.Lerp(top, bottom, ty) * amp;
                    }
                }

                total += amp;
                amp *= 0.5f;
            }

            for (int i = 0; i < result.Length; i++)
            {
                result[i] /= total;
            }

            return result;
        }

        // ------------------------------------------------------------------
        // 유틸리티
        // ------------------------------------------------------------------

        private static void Ring(GameObject prefab, Transform parent, Ground ground, Room room, int count, float radius, string name)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 offset = Polar(radius, 360f * i / count);
                Vector2 p = room.Center + new Vector2(offset.x, offset.z);
                Place(prefab, parent, ground.At(p), Quaternion.identity, $"{name}_{i}");
            }
        }

        private static Vector2 IncomingDirection(LevelSpec spec, Room room)
        {
            int index = Array.IndexOf(spec.Rooms, room);

            foreach (Link link in spec.Links)
            {
                if (link.To == index)
                {
                    return (room.Center - spec.Rooms[link.From].Center).normalized;
                }
            }

            return Vector2.up;
        }

        private static Quaternion Yaw(Vector2 dir)
        {
            return Quaternion.Euler(0f, Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg, 0f);
        }

        private static void Place(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, string name)
        {
            if (prefab == null)
            {
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, false);
            instance.transform.SetPositionAndRotation(position, rotation);
        }

        // 씬 뷰에서 보이도록 색 라벨 아이콘 (0 ~ 7), 위치는 지형 높이에 맞춤
        private static GameObject Marker(Transform parent, Ground ground, string name, Room room, Vector3 offset, int label)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.position = ground.At(room.Center + new Vector2(offset.x, offset.z));
            SetIcon(go, label);
            return go;
        }

        private static void SetIcon(GameObject go, int label)
        {
            if (EditorGUIUtility.IconContent($"sv_label_{label}").image is Texture2D icon)
            {
                EditorGUIUtility.SetIconForObject(go, icon);
            }
        }

        private static Vector3 Polar(float radius, float degrees)
        {
            float r = degrees * Mathf.Deg2Rad;
            return new Vector3(Mathf.Sin(r) * radius, 0f, Mathf.Cos(r) * radius);
        }

        private static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        private static float SmoothStep(float edge0, float edge1, float x)
        {
            return Smooth(Mathf.Clamp01((x - edge0) / (edge1 - edge0)));
        }

        private static float Smooth(float t) => t * t * (3f - 2f * t);
    }
}
