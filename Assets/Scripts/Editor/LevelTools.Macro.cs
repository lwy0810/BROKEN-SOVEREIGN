using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 실사 지형 패키지 연동 (2026-10-19, Asset Store "3D Realistic Terrain Free" by Bad Raccoon).
    /// 패키지의 1km 침식 지형(높이 2049², 세로 400m)과 8K 색·노멀을 그대로 오픈 필드로 씁니다 (사용자 결정: 패키지 그대로, 강만 유지).
    /// 길·방은 지형을 흐린 바닥 높이로 평탄하게(방은 중심 높이, 가장자리 10m에 걸쳐 원래 지형), 강은 바닥 높이로 파냄.
    /// 산맥 벽·코드 생성 풀 텍스처·바위·높이 변형은 쓰지 않습니다.
    /// 패키지가 없으면(저장소에는 포함하지 않음, .gitignore) 기존 코드 생성 산맥으로 동작합니다.
    /// </summary>
    public static partial class LevelTools
    {
        private const string MACRO_ROOT = "Assets/Bad_Raccoon/3D Realistic Terrain Free";
        private const string MACRO_TERRAIN = MACRO_ROOT + "/Terrains/Terrain_A.asset";
        private const string MACRO_DIFFUSE = MACRO_ROOT + "/Textures/Diffuse_8K_Flat_C.png";  // 패키지 예제(Terrain_A.mat)와 같은 초록 계열
        private const string MACRO_MATERIAL = MACRO_ROOT + "/Terrains/Terrain_A.mat";
        private const string MACRO_SKYBOX = MACRO_ROOT + "/Textures/Skybox.mat";
        private const string MACRO_NORMAL = MACRO_ROOT + "/Textures/Normal_8K_Flat_A.png";
        private const string MACRO_DETAIL_NORMAL = MACRO_ROOT + "/Textures/Detail_Bump_01.png";
        private const string MACRO_GRASS = MACRO_ROOT + "/Textures/Grass_Sprite_01.psd";

        private const int MACRO = 6;                    // 7번째 지형 레이어 (패키지 색)
        private const float MACRO_FLOOR_CELL = 4f;      // 계곡 바닥 격자 (m)
        private const float MACRO_FLOOR_BLUR = 50f;     // 길 바닥 흐림 반경 (가우스 시그마, m)
        private const float MACRO_FLOOR_RELIEF = 0.7f;  // 길 바닥 기복 비율 (1 = 흐린 지형 그대로)
        private const float MACRO_ROOM_BLEND = 40f;     // 방 바깥 이 거리까지 방 중심 높이로 평평하게
        private const bool MACRO_CENTER_PACKAGE_MATERIAL = true;  // 가운데 지형도 패키지 머티리얼 (길 색 대신 패키지와 동일한 모습)
        private const float MACRO_SHOULDER = 10f;       // 길·방 가장자리에서 원래 지형으로 돌아가는 폭 (m)

        private static bool HasMacroPackage => AssetDatabase.LoadAssetAtPath<TerrainData>(MACRO_TERRAIN) != null;

        /// <summary>패키지 지형 높이 표본 (월드 좌표, 미터)</summary>
        private sealed class MacroTerrain
        {
            private readonly float[,] _height;   // [z, x] 미터
            private readonly float[,] _floor;    // [z, x] 미터 (흐린 계곡 바닥)
            private readonly int _res;
            private readonly int _floorRes;
            private readonly float _size;

            /// <summary>패키지 지형 최저 높이 (기복 키울 때 기준)</summary>
            public float Base { get; private set; }

            private MacroTerrain(float[,] height, float[,] floor, float size)
            {
                _height = height;
                _floor = floor;
                _res = height.GetLength(0);
                _floorRes = floor.GetLength(0);
                _size = size;
            }

            /// <summary>패키지가 없으면 null. worldSize = 우리 지형 크기 (패키지 1km를 그대로 겹침)</summary>
            public static MacroTerrain Load(float worldSize)
            {
                TerrainData source = AssetDatabase.LoadAssetAtPath<TerrainData>(MACRO_TERRAIN);

                if (source == null)
                {
                    return null;
                }

                int res = source.heightmapResolution;
                float[,] normalized = source.GetHeights(0, 0, res, res);
                float scale = source.size.y;
                var height = new float[res, res];

                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        height[z, x] = normalized[z, x] * scale;
                    }
                }

                float min = float.MaxValue;

                foreach (float h in height)
                {
                    min = Mathf.Min(min, h);
                }

                var macro = new MacroTerrain(height, new float[1, 1], worldSize);
                float[,] floor = BuildFloor(macro, worldSize);
                Debug.Log($"LevelTools: 실사 지형 패키지 사용 ({res}², 세로 {scale}m, 길·방만 평탄)");
                return new MacroTerrain(height, floor, worldSize) { Base = min };
            }

            public float HeightAt(float x, float z) => Bilinear(_height, _res, x, z);

            public float FloorAt(float x, float z) => Bilinear(_floor, _floorRes, x, z);

            private float Bilinear(float[,] grid, int res, float x, float z)
            {
                float u = Mathf.Clamp01(x / _size + 0.5f) * (res - 1);
                float v = Mathf.Clamp01(z / _size + 0.5f) * (res - 1);
                int x0 = Mathf.Min((int)u, res - 2);
                int z0 = Mathf.Min((int)v, res - 2);
                float fx = u - x0;
                float fz = v - z0;
                float a = Mathf.Lerp(grid[z0, x0], grid[z0, x0 + 1], fx);
                float b = Mathf.Lerp(grid[z0 + 1, x0], grid[z0 + 1, x0 + 1], fx);
                return Mathf.Lerp(a, b, fz);
            }

            // 계곡 바닥: 패키지 높이를 4m 격자로 줄여 가우스 흐림 → 평균 쪽으로 기복 축소 (길 경사 완만하게)
            private static float[,] BuildFloor(MacroTerrain source, float size)
            {
                int n = Mathf.RoundToInt(size / MACRO_FLOOR_CELL) + 1;
                var grid = new float[n, n];
                float half = size * 0.5f;

                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        grid[z, x] = source.HeightAt(-half + size * x / (n - 1), -half + size * z / (n - 1));
                    }
                }

                float sigma = MACRO_FLOOR_BLUR / MACRO_FLOOR_CELL;
                int radius = Mathf.CeilToInt(sigma * 3f);
                var kernel = new float[radius * 2 + 1];
                float sum = 0f;

                for (int i = -radius; i <= radius; i++)
                {
                    kernel[i + radius] = Mathf.Exp(-i * i / (2f * sigma * sigma));
                    sum += kernel[i + radius];
                }

                for (int i = 0; i < kernel.Length; i++)
                {
                    kernel[i] /= sum;
                }

                var temp = new float[n, n];
                var blurred = new float[n, n];

                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float acc = 0f;

                        for (int k = -radius; k <= radius; k++)
                        {
                            acc += grid[z, Mathf.Clamp(x + k, 0, n - 1)] * kernel[k + radius];
                        }

                        temp[z, x] = acc;
                    }
                }

                double total = 0;

                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        float acc = 0f;

                        for (int k = -radius; k <= radius; k++)
                        {
                            acc += temp[Mathf.Clamp(z + k, 0, n - 1), x] * kernel[k + radius];
                        }

                        blurred[z, x] = acc;
                        total += acc;
                    }
                }

                float mean = (float)(total / (n * n));

                for (int z = 0; z < n; z++)
                {
                    for (int x = 0; x < n; x++)
                    {
                        blurred[z, x] = mean + (blurred[z, x] - mean) * MACRO_FLOOR_RELIEF;
                    }
                }

                return blurred;
            }
        }

        /// <summary>패키지 색·노멀 레이어 (지형 크기만큼 한 장, 오프셋 0 = 패키지 지형과 같은 배치). 없으면 null</summary>
        private static TerrainLayer EnsureMacroLayer(float size)
        {
            Texture2D diffuse = AssetDatabase.LoadAssetAtPath<Texture2D>(MACRO_DIFFUSE);

            if (diffuse == null)
            {
                return null;
            }

            // 8K 원본은 메모리 부담 → 4K로 가져오기 (원본 파일은 그대로)
            LimitTexture(MACRO_DIFFUSE, 4096, false);
            LimitTexture(MACRO_NORMAL, 4096, true);

            string layerPath = $"{LAYER_ROOT}/TL_Macro.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);

            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = diffuse;
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(MACRO_NORMAL);
            layer.normalScale = 0.5f;   // 패키지 머티리얼 _BumpScale
            layer.tileSize = new Vector2(size, size);
            layer.tileOffset = Vector2.zero;
            layer.smoothness = 0.05f;
            layer.metallic = 0f;
            EditorUtility.SetDirty(layer);
            return layer;
        }

        /// <summary>코드 생성 레이어의 미세 굴곡 노멀 (패키지 Detail_Bump). 없으면 null</summary>
        private static Texture2D MacroDetailNormal()
        {
            if (AssetDatabase.LoadAssetAtPath<Texture2D>(MACRO_DETAIL_NORMAL) == null)
            {
                return null;
            }

            LimitTexture(MACRO_DETAIL_NORMAL, 2048, true);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(MACRO_DETAIL_NORMAL);
        }

        /// <summary>풀 디테일 그림: 패키지 풀 스프라이트, 없으면 null (코드 생성 풀 사용)</summary>
        private static Texture2D MacroGrassTexture()
        {
            if (!(AssetImporter.GetAtPath(MACRO_GRASS) is TextureImporter importer))
            {
                return null;
            }

            if (!importer.alphaIsTransparency || importer.wrapMode != TextureWrapMode.Clamp)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(MACRO_GRASS);
        }

        /// <summary>
        /// 패키지 예제 씬(Example_01)과 같은 분위기: 패키지 하늘, 옅은 하늘색 선형 안개 200~1000m, 따뜻한 햇빛 2.
        /// 주변 8칸에 패키지 지형을 그대로 깔아 맵 끝 대신 산이 이어져 보이게 함 (패키지 지형은 이음새 없이 반복 가능).
        /// 주변 지형은 Level 밖(Scenery)에 두어 NavMesh·걷기에서 제외.
        /// </summary>
        private static void ApplyMacroLook(LevelSpec spec, Terrain center)
        {
            Material sky = AssetDatabase.LoadAssetAtPath<Material>(MACRO_SKYBOX);

            if (sky != null)
            {
                RenderSettings.skybox = sky;
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
            }

            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.749f, 0.89f, 1f);
            RenderSettings.fogStartDistance = 200f;
            RenderSettings.fogEndDistance = 1000f;

            if (RenderSettings.sun != null)
            {
                RenderSettings.sun.color = new Color(0.981f, 0.946f, 0.884f);
                RenderSettings.sun.intensity = 2f;
            }

            center.detailObjectDistance = 140f;
            center.heightmapPixelError = 5f;
            center.basemapDistance = 1000f;

            TerrainData data = AssetDatabase.LoadAssetAtPath<TerrainData>(MACRO_TERRAIN);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(MACRO_MATERIAL);

            // 가운데(마을·길이 있는 칸)도 주변과 똑같이 패키지 머티리얼로 그림 (사용자 요청 10/20).
            // 이 머티리얼은 텍스처 칠하기(Splat)를 쓰지 않으므로 길 돌바닥·흙길·강 자갈 색은 보이지 않음.
            // 칠하기 데이터는 그대로 남아 있어 MACRO_CENTER_PACKAGE_MATERIAL = false로 되돌릴 수 있음
            if (MACRO_CENTER_PACKAGE_MATERIAL && material != null)
            {
                center.materialTemplate = material;
            }

            if (data == null)
            {
                return;
            }

            var root = new GameObject("Scenery").transform;
            float half = spec.Size * 0.5f;

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0)
                    {
                        continue;
                    }

                    GameObject go = Terrain.CreateTerrainGameObject(data);
                    go.name = $"Scenery_{dx}_{dz}";
                    go.transform.SetParent(root, false);
                    go.transform.position = new Vector3(-half + dx * spec.Size, 0f, -half + dz * spec.Size);
                    Object.DestroyImmediate(go.GetComponent<TerrainCollider>());

                    Terrain terrain = go.GetComponent<Terrain>();
                    terrain.materialTemplate = material;
                    terrain.heightmapPixelError = 8f;
                    terrain.drawTreesAndFoliage = false;
                    terrain.castShadows = true;
                    GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
                }
            }
        }

        private static void LimitTexture(string path, int maxSize, bool normalMap)
        {
            if (!(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                return;
            }

            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;

            if (importer.maxTextureSize == maxSize && importer.textureType == type && importer.wrapMode == TextureWrapMode.Repeat)
            {
                return;
            }

            importer.textureType = type;
            importer.maxTextureSize = maxSize;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.SaveAndReimport();
        }
    }
}
