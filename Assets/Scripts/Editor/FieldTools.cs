using System.Linq;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 천계 필드 도구 (GDD 13장: 부유 석판 + 구름 바다 + Procedural 하늘). 구역 지형(Terrain)은 LevelTools가 담당.
    /// 메뉴: BrokenSovereign > Field
    /// - 필드 킷 생성: 머티리얼(하늘·구름·석판), 구름 바다 메시, 석판·다리 프리팹(ProBuilder)
    /// - 현재 씬에 분위기 적용: 하늘·태양·주변광·안개·구름 바다·후처리 볼륨
    /// - 분위기 테스트 씬 생성: Scenes/Sandbox/FieldLookTest.unity (석판·소품·성기사 배치, 쿼터뷰 카메라)
    /// 분위기 값은 Assets/Settings/FieldLookProfile.asset에서 조정합니다.
    /// </summary>
    public static class FieldTools
    {
        private const string PROFILE_PATH = "Assets/Settings/FieldLookProfile.asset";
        private const string VOLUME_PATH = "Assets/Settings/FieldVolumeProfile.asset";
        private const string MATERIAL_ROOT = "Assets/Materials/Field";
        private const string PREFAB_ROOT = "Assets/Prefabs/Field";
        private const string MESH_ROOT = "Assets/Models/Field";
        private const string SCENE_PATH = "Assets/Scenes/Sandbox/FieldLookTest.unity";
        private const string CLOUD_SHADER = "BrokenSovereign/Field/CloudSea";
        private const string SKY_SHADER = "Skybox/Procedural";
        private const string CLOUD_NAME = "CloudSea";
        private const string VOLUME_NAME = "FieldVolume";
        private const string SUN_NAME = "Sun";
        private const int CLOUD_SEGMENTS = 120;

        /// <summary>석판 1종의 치수. 새 모양은 이 목록에 추가합니다.</summary>
        private readonly struct SlabSpec
        {
            public readonly string Name;
            public readonly bool Round;
            public readonly Vector3 Size;       // 둥근 석판은 x = 지름
            public readonly int Sides;
            public readonly float RockRadius;
            public readonly float RockHeight;

            public SlabSpec(string name, bool round, Vector3 size, int sides, float rockRadius, float rockHeight)
            {
                Name = name;
                Round = round;
                Size = size;
                Sides = sides;
                RockRadius = rockRadius;
                RockHeight = rockHeight;
            }
        }

        private static readonly SlabSpec[] SLABS =
        {
            new SlabSpec("FieldSlabRound8", true, new Vector3(8f, 1f, 8f), 16, 3.7f, 4.5f),
            new SlabSpec("FieldSlabRound4", true, new Vector3(4f, 0.8f, 4f), 12, 1.8f, 2.6f),
            new SlabSpec("FieldSlabSquare10", false, new Vector3(10f, 1f, 10f), 8, 4.9f, 5f),
        };

        private const string BRIDGE_NAME = "FieldBridge6";
        private static readonly Vector3 BRIDGE_SIZE = new Vector3(2.6f, 0.4f, 6f);
        private const string RAILING_PREFAB = "Assets/Prefabs/Props/PropCloudRailing.prefab";

        // ------------------------------------------------------------------
        // 메뉴
        // ------------------------------------------------------------------

        [MenuItem("BrokenSovereign/Field/필드 킷 생성 (머티리얼·석판 프리팹)")]
        private static void BuildKit()
        {
            FieldLookProfile profile = LoadOrCreateProfile();
            BuildMaterials(profile);
            BuildCloudMesh(profile);
            BuildPrefabs();
            AssetDatabase.SaveAssets();
            Debug.Log($"FieldTools: 필드 킷 생성 완료 ({PREFAB_ROOT})");
        }

        [MenuItem("BrokenSovereign/Field/현재 씬에 분위기 적용")]
        private static void ApplyToCurrentScene()
        {
            FieldLookProfile profile = LoadOrCreateProfile();
            BuildMaterials(profile);
            BuildCloudMesh(profile);
            ApplyAtmosphere(profile);
            AssetDatabase.SaveAssets();
            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            Debug.Log("FieldTools: 현재 씬에 분위기 적용. 씬을 저장하세요.");
        }

        /// <summary>
        /// 머티리얼·구름 메시를 갱신하고 현재 씬에 분위기(하늘·조명·안개·구름 바다·후처리)를 적용 (LevelTools용).
        /// 구름 바다가 맵보다 작으면 프로필의 CloudSize를 minCloudSize로 늘림
        /// </summary>
        internal static void PrepareAtmosphere(float minCloudSize)
        {
            FieldLookProfile profile = LoadOrCreateProfile();

            if (profile.CloudSize < minCloudSize)
            {
                profile.CloudSize = minCloudSize;
                EditorUtility.SetDirty(profile);
            }

            BuildMaterials(profile);
            BuildCloudMesh(profile);
            ApplyAtmosphere(profile);
        }

        [MenuItem("BrokenSovereign/Field/분위기 테스트 씬 생성")]
        private static void CreateTestScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            {
                return;
            }

            BuildKit();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            FieldLookProfile profile = LoadOrCreateProfile();
            ApplyAtmosphere(profile);
            PlaceSampleLayout();
            CreateQuarterViewCamera(new Vector3(0f, 0f, 3f));

            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene(), SCENE_PATH);
            Debug.Log($"FieldTools: 테스트 씬 저장 ({SCENE_PATH})");
        }

        // ------------------------------------------------------------------
        // 에셋
        // ------------------------------------------------------------------

        private static FieldLookProfile LoadOrCreateProfile()
        {
            FieldLookProfile profile = AssetDatabase.LoadAssetAtPath<FieldLookProfile>(PROFILE_PATH);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<FieldLookProfile>();
                AssetDatabase.CreateAsset(profile, PROFILE_PATH);
            }

            return profile;
        }

        private static Material GetOrCreateMaterial(string name, string shaderName)
        {
            ModelAssetUtility.EnsureFolder(MATERIAL_ROOT);
            string path = $"{MATERIAL_ROOT}/{name}.mat";
            Shader shader = Shader.Find(shaderName);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void BuildMaterials(FieldLookProfile p)
        {
            Material sky = GetOrCreateMaterial("M_FieldSkybox", SKY_SHADER);
            sky.SetFloat("_SunDisk", 2f);
            sky.SetFloat("_SunSize", p.SunSize);
            sky.SetFloat("_SunSizeConvergence", 5f);
            sky.SetFloat("_AtmosphereThickness", p.AtmosphereThickness);
            sky.SetColor("_SkyTint", p.SkyTint);
            sky.SetColor("_GroundColor", p.SkyGround);
            sky.SetFloat("_Exposure", p.SkyExposure);

            Material cloud = GetOrCreateMaterial("M_CloudSea", CLOUD_SHADER);
            cloud.SetColor("_ColorHigh", p.CloudHigh);
            cloud.SetColor("_ColorLow", p.CloudLow);
            cloud.SetColor("_ColorGlow", p.CloudGlow);

            Material stone = GetOrCreateMaterial("M_FieldStone", ModelAssetUtility.URP_LIT);
            stone.SetColor("_BaseColor", p.StoneTop);
            stone.SetFloat("_Smoothness", p.StoneSmoothness);

            Material rock = GetOrCreateMaterial("M_FieldRock", ModelAssetUtility.URP_LIT);
            rock.SetColor("_BaseColor", p.StoneRock);
            rock.SetFloat("_Smoothness", 0.15f);
        }

        private static Mesh BuildCloudMesh(FieldLookProfile p)
        {
            ModelAssetUtility.EnsureFolder(MESH_ROOT);
            string path = $"{MESH_ROOT}/CloudSeaGrid.asset";
            Mesh mesh = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (mesh == null)
            {
                mesh = new Mesh { name = "CloudSeaGrid" };
                AssetDatabase.CreateAsset(mesh, path);
            }

            int n = CLOUD_SEGMENTS;
            float size = p.CloudSize;
            var vertices = new Vector3[(n + 1) * (n + 1)];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[n * n * 6];

            for (int z = 0; z <= n; z++)
            {
                for (int x = 0; x <= n; x++)
                {
                    int i = z * (n + 1) + x;
                    vertices[i] = new Vector3((x / (float)n - 0.5f) * size, 0f, (z / (float)n - 0.5f) * size);
                    uvs[i] = new Vector2(x / (float)n, z / (float)n);
                }
            }

            int t = 0;

            for (int z = 0; z < n; z++)
            {
                for (int x = 0; x < n; x++)
                {
                    int i = z * (n + 1) + x;
                    triangles[t++] = i;
                    triangles[t++] = i + n + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + n + 1;
                    triangles[t++] = i + n + 2;
                }
            }

            mesh.Clear();
            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            // 정점이 셰이더에서 위아래로 움직이므로 경계를 넉넉히
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(size, 6f, size));
            EditorUtility.SetDirty(mesh);
            return mesh;
        }

        private static VolumeProfile BuildVolumeProfile(FieldLookProfile p)
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(VOLUME_PATH);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, VOLUME_PATH);
            }

            Bloom bloom = GetOrAdd<Bloom>(profile);
            bloom.threshold.Override(p.BloomThreshold);
            bloom.intensity.Override(p.BloomIntensity);
            bloom.scatter.Override(0.65f);
            bloom.tint.Override(p.BloomTint);

            Tonemapping tonemapping = GetOrAdd<Tonemapping>(profile);
            tonemapping.mode.Override(TonemappingMode.Neutral);

            ColorAdjustments color = GetOrAdd<ColorAdjustments>(profile);
            color.postExposure.Override(p.PostExposure);
            color.saturation.Override(p.Saturation);

            Vignette vignette = GetOrAdd<Vignette>(profile);
            vignette.intensity.Override(p.Vignette);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        // 볼륨 컴포넌트는 프로파일 에셋의 하위 에셋으로 저장해야 유지됨
        private static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet(out T component))
            {
                return component;
            }

            component = profile.Add<T>(true);
            component.name = typeof(T).Name;
            AssetDatabase.AddObjectToAsset(component, profile);
            return component;
        }

        // ------------------------------------------------------------------
        // 석판 프리팹 (ProBuilder)
        // ------------------------------------------------------------------

        private static void BuildPrefabs()
        {
            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);
            Material stone = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIAL_ROOT}/M_FieldStone.mat");
            Material rock = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIAL_ROOT}/M_FieldRock.mat");

            for (int i = 0; i < SLABS.Length; i++)
            {
                SaveSlab(SLABS[i], stone, rock, 1000 + i);
            }

            SaveBridge(stone);
        }

        private static void SaveSlab(SlabSpec spec, Material stone, Material rock, int seed)
        {
            GameObject root = new GameObject(spec.Name);

            ProBuilderMesh top = spec.Round
                ? ShapeGenerator.GenerateCylinder(PivotLocation.Center, spec.Sides, spec.Size.x * 0.5f, spec.Size.y, 0)
                : ShapeGenerator.GenerateCube(PivotLocation.Center, spec.Size);
            Finish(top, "Top", root.transform, new Vector3(0f, -spec.Size.y * 0.5f, 0f), Quaternion.identity, stone);
            AddWalkCollider(top.gameObject);

            // 밑면 바위: 원뿔을 뒤집어 붙이고 정점을 흔들어 자연스러운 바위 모양
            ProBuilderMesh rockMesh = ShapeGenerator.GenerateCone(PivotLocation.Center, spec.RockRadius, spec.RockHeight, spec.Sides);
            Jitter(rockMesh, spec.RockRadius * 0.18f, seed);
            Finish(rockMesh, "Rock", root.transform,
                new Vector3(0f, -spec.Size.y - spec.RockHeight * 0.5f, 0f), Quaternion.Euler(180f, 0f, 0f), rock);

            // 바위는 걸을 수 없으므로 NavMesh 굽기에서 제외
            rockMesh.gameObject.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

            SavePrefab(root);
        }

        private static void SaveBridge(Material stone)
        {
            GameObject root = new GameObject(BRIDGE_NAME);
            ProBuilderMesh deck = ShapeGenerator.GenerateCube(PivotLocation.Center, BRIDGE_SIZE);
            Finish(deck, "Deck", root.transform, new Vector3(0f, -BRIDGE_SIZE.y * 0.5f, 0f), Quaternion.identity, stone);
            AddWalkCollider(deck.gameObject);

            // 양옆 난간 (구름 난간 소품 2m × 3개씩, 다리 방향 Z에 맞춰 90° 회전)
            GameObject railing = AssetDatabase.LoadAssetAtPath<GameObject>(RAILING_PREFAB);

            if (railing != null)
            {
                foreach (float side in new[] { -1f, 1f })
                {
                    for (int i = -1; i <= 1; i++)
                    {
                        GameObject r = (GameObject)PrefabUtility.InstantiatePrefab(railing);
                        r.transform.SetParent(root.transform, false);
                        r.transform.localPosition = new Vector3(side * (BRIDGE_SIZE.x * 0.5f - 0.15f), 0f, i * 2f);
                        r.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                    }
                }
            }

            SavePrefab(root);
        }

        private static void Finish(ProBuilderMesh mesh, string name, Transform parent, Vector3 position, Quaternion rotation, Material material)
        {
            mesh.gameObject.name = name;
            mesh.transform.SetParent(parent, false);
            mesh.transform.localPosition = position;
            mesh.transform.localRotation = rotation;
            mesh.SetMaterial(mesh.faces, material);
            mesh.ToMesh();
            mesh.Refresh();
            GameObjectUtility.SetStaticEditorFlags(mesh.gameObject, StaticEditorFlags.BatchingStatic);
        }

        private static void AddWalkCollider(GameObject go)
        {
            MeshCollider collider = go.AddComponent<MeshCollider>();
            collider.sharedMesh = go.GetComponent<MeshFilter>().sharedMesh;
        }

        // 공유 정점 단위로 같은 오프셋을 줘서 면이 갈라지지 않게 함. 원뿔 밑면(석판에 붙는 쪽)은 고정
        private static void Jitter(ProBuilderMesh mesh, float amount, int seed)
        {
            var random = new System.Random(seed);
            Vector3[] positions = mesh.positions.ToArray();
            float minY = positions.Min(v => v.y);
            float maxY = positions.Max(v => v.y);

            foreach (SharedVertex shared in mesh.sharedVertices)
            {
                int first = shared[0];
                float t = Mathf.InverseLerp(minY, maxY, positions[first].y);

                if (t < 0.01f)
                {
                    continue;
                }

                Vector3 offset = new Vector3(
                    (float)(random.NextDouble() * 2 - 1),
                    (float)(random.NextDouble() * 2 - 1) * 0.5f,
                    (float)(random.NextDouble() * 2 - 1)) * amount * Mathf.Sin(t * Mathf.PI);

                foreach (int index in shared)
                {
                    positions[index] += offset;
                }
            }

            mesh.positions = positions;
        }

        private static void SavePrefab(GameObject root)
        {
            PrefabUtility.SaveAsPrefabAsset(root, $"{PREFAB_ROOT}/{root.name}.prefab");
            Object.DestroyImmediate(root);
        }

        // ------------------------------------------------------------------
        // 씬
        // ------------------------------------------------------------------

        private static void ApplyAtmosphere(FieldLookProfile p)
        {
            Material sky = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIAL_ROOT}/M_FieldSkybox.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = p.AmbientSky;
            RenderSettings.ambientEquatorColor = p.AmbientEquator;
            RenderSettings.ambientGroundColor = p.AmbientGround;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = p.FogColor;
            RenderSettings.fogStartDistance = p.FogStart;
            RenderSettings.fogEndDistance = p.FogEnd;

            Light sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None)
                .FirstOrDefault(l => l.type == LightType.Directional);

            if (sun == null)
            {
                sun = new GameObject(SUN_NAME).AddComponent<Light>();
                sun.type = LightType.Directional;
                Undo.RegisterCreatedObjectUndo(sun.gameObject, "Create Sun");
            }

            Undo.RecordObject(sun, "Field Sun");
            Undo.RecordObject(sun.transform, "Field Sun");
            sun.color = p.SunColor;
            sun.intensity = p.SunIntensity;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(p.SunEuler);
            RenderSettings.sun = sun;

            GameObject cloud = GameObject.Find(CLOUD_NAME);

            if (cloud == null)
            {
                cloud = new GameObject(CLOUD_NAME, typeof(MeshFilter), typeof(MeshRenderer));
                Undo.RegisterCreatedObjectUndo(cloud, "Create Cloud Sea");
            }

            cloud.transform.position = new Vector3(0f, p.CloudHeight, 0f);
            cloud.GetComponent<MeshFilter>().sharedMesh = AssetDatabase.LoadAssetAtPath<Mesh>($"{MESH_ROOT}/CloudSeaGrid.asset");
            MeshRenderer cloudRenderer = cloud.GetComponent<MeshRenderer>();
            cloudRenderer.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>($"{MATERIAL_ROOT}/M_CloudSea.mat");
            cloudRenderer.shadowCastingMode = ShadowCastingMode.Off;
            cloudRenderer.receiveShadows = false;

            GameObject volumeObject = GameObject.Find(VOLUME_NAME);

            if (volumeObject == null)
            {
                volumeObject = new GameObject(VOLUME_NAME, typeof(Volume));
                Undo.RegisterCreatedObjectUndo(volumeObject, "Create Field Volume");
            }

            Volume volume = volumeObject.GetComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 1f;
            volume.sharedProfile = BuildVolumeProfile(p);

            DynamicGI.UpdateEnvironment();
        }

        private static void PlaceSampleLayout()
        {
            var root = new GameObject("FieldLayout").transform;
            Place($"{PREFAB_ROOT}/FieldSlabSquare10.prefab", root, new Vector3(0f, 0f, 0f), 0f);
            Place($"{PREFAB_ROOT}/{BRIDGE_NAME}.prefab", root, new Vector3(0f, 0f, 8f), 0f);
            Place($"{PREFAB_ROOT}/FieldSlabRound8.prefab", root, new Vector3(0f, 0f, 15f), 0f);
            Place($"{PREFAB_ROOT}/{BRIDGE_NAME}.prefab", root, new Vector3(8f, 0f, 0f), 90f);
            Place($"{PREFAB_ROOT}/FieldSlabRound4.prefab", root, new Vector3(13f, 0f, 0f), 0f);

            // 멀리 떠 있는 장식 석판 (걸어갈 수 없음)
            Place($"{PREFAB_ROOT}/FieldSlabRound4.prefab", root, new Vector3(-14f, -2.5f, 10f), 30f);
            Place($"{PREFAB_ROOT}/FieldSlabRound8.prefab", root, new Vector3(16f, -5f, -14f), 60f);

            var props = new GameObject("Props").transform;
            Place("Assets/Prefabs/Props/PropPaifang.prefab", props, new Vector3(0f, 0f, 4.3f), 0f);
            Place("Assets/Prefabs/Props/PropStonePillar.prefab", props, new Vector3(-4f, 0f, -4f), 0f);
            Place("Assets/Prefabs/Props/PropStonePillar.prefab", props, new Vector3(4f, 0f, -4f), 0f);
            Place("Assets/Prefabs/Props/PropStoneLantern.prefab", props, new Vector3(-2f, 0f, 13f), 0f);
            Place("Assets/Prefabs/Props/PropStoneLantern.prefab", props, new Vector3(2f, 0f, 13f), 0f);

            // 크기 비교용 성기사
            Place("Assets/Prefabs/Characters/PaladinModel.prefab", props, new Vector3(0f, 0f, 1f), 180f);
        }

        private static void Place(string path, Transform parent, Vector3 position, float yaw)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning($"FieldTools: 프리팹 없음 ({path})");
                return;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(parent, false);
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        // 게임 카메라(QuarterViewCamera)와 같은 각도: 피치 50°, 요 45°, 거리 18
        internal static void CreateQuarterViewCamera(Vector3 target)
        {
            var go = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            go.tag = "MainCamera";
            Quaternion rotation = Quaternion.Euler(50f, 45f, 0f);
            go.transform.SetPositionAndRotation(target - rotation * Vector3.forward * 18f, rotation);

            Camera camera = go.GetComponent<Camera>();
            camera.clearFlags = CameraClearFlags.Skybox;
            camera.farClipPlane = 400f;

            UniversalAdditionalCameraData data = go.GetComponent<UniversalAdditionalCameraData>();

            if (data == null)
            {
                data = go.AddComponent<UniversalAdditionalCameraData>();
            }

            data.renderPostProcessing = true;
        }
    }
}
