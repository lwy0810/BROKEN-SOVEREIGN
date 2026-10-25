using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 오픈 필드 지면 실사화 (2026-10-22, 참고: 디아블로 4 마른 땅 바닥).
    ///
    /// 문제: 가운데 지형이 1km 전체에 텍스처 한 장(4K)을 늘려 씌운 패키지 머티리얼이라 카메라 거리(10~28m)에서
    /// 약 0.24m당 1픽셀 → 바닥이 흐린 색 얼룩으로 보임. 풀은 평면 빌보드라 위에서 얇게 보임.
    ///
    /// 해결:
    /// 1. 지면: 실사 반복 텍스처 4종(Poly Haven CC0, Assets/Terrain/Ground/{이름})을 4~8m 간격으로 반복하는
    ///    URP Terrain Lit(높이 기반 경계 섞기, 마스크 맵 = AO·높이·매끈함)로 교체.
    ///    칠하기: 흙먼지 바탕 / 갈라진 땅(큰 얼룩·길 어깨·강가) / 자갈 흙길(길·강바닥) / 바위(경사 32~44°).
    ///    길·강 위치는 LevelTools가 칠한 원래 레이어에서 읽어 WorldSourceMask.png(R 흙길, G 돌바닥, B 강 자갈)로 보관 → 다시 적용해도 같은 결과.
    /// 2. 풀·잔해: 빌보드 → 코드 생성 메시(풀 다발 2색, 조약돌, 나뭇가지)를 Terrain 디테일(인스턴싱)로 촘촘히.
    /// 3. 분위기: 밝은 마른 땅 톤 (따뜻한 햇빛, 모래빛 안개, 약한 비네트) + 길 덮개의 흙길은 끄고 마을 돌바닥만 유지.
    ///    풀은 작은 무더기로 드문드문 (참고 이미지 2차, 10/22).
    ///
    /// 메뉴: BrokenSovereign > Levels > 오픈 필드 지면 실사화 (현재 씬). 오픈 필드를 다시 생성할 때도 텍스처가 있으면 자동 적용.
    /// </summary>
    public static partial class LevelTools
    {
        private const string GROUND_ROOT = TERRAIN_ROOT + "/Ground";
        private const string GROUND_SOURCE_MASK = TERRAIN_ROOT + "/WorldSourceMask.png";
        private const string GROUND_MATERIAL = "Assets/Materials/Field/M_TerrainGround.mat";
        private const string CLUTTER_MESH_ROOT = "Assets/Models/Field/Clutter";
        private const string CLUTTER_PREFAB_ROOT = FIELD_PREFABS + "/Clutter";
        private const string CLUTTER_MATERIAL_ROOT = "Assets/Materials/Field/Clutter";
        private const string WORLD_VOLUME = "Assets/Settings/WorldVolumeProfile.asset";
        private const string FIELD_VOLUME = "Assets/Settings/FieldVolumeProfile.asset";
        private const float GROUND_NOISE = 37.1f;     // 칠하기 노이즈 오프셋
        private const float GROUND_HEIGHT_BLEND = 0.25f;
        private const float DETAIL_DISTANCE = 80f;
        private const float CRACK_PATCH = 0.5f;     // 갈라진 땅 얼룩 기준 (낮출수록 갈라진 땅이 넓어짐)
        private const float GRASS_PATCH = 0.6f;     // 풀 무더기 기준 (높을수록 무더기가 적음, 0.6 ≈ 면적 10% 안팎)
        private const float GRASS_STRAY = 0.01f;    // 무더기 밖 외톨이 풀 확률 (칸당)

        private const int G_BASE = 0;
        private const int G_CRACK = 1;
        private const int G_TRAIL = 2;
        private const int G_ROCK = 3;

        /// <summary>
        /// 레이어 순서 = G_BASE, G_CRACK, G_TRAIL, G_ROCK. 폴더 후보를 앞에서부터 찾고, 없으면 역할 이름 폴더.
        /// 10/22 참고 이미지(2차): 바탕은 갈라짐 없는 옅은 흙먼지, 그 위에 갈라진 땅이 큰 얼룩으로 군데군데.
        /// - 바탕 dust_ground: 코드로 만든 반복 텍스처(옅은 황토, 고운 입자·작은 알갱이), 이 프로젝트 생성물
        /// - 갈라진 땅: TextureCan Ground 0041 (CC0). 원본이 주황빛이 강해 색 보정(tint)으로 채도를 낮추고 밝게
        /// tint = 레이어 색 배율 (diffuseRemapMax)
        /// </summary>
        private static readonly (string[] folders, string role, float tile, Vector3 tint)[] GROUND_SETS =
        {
            (new[] { "dust_ground", "forrest_ground_01" }, "Ground", 8f, Vector3.one),
            (new[] { "ground_0041", "Ground_0041", "ground_0013", "Ground_0013" }, "Crack", 3f, new Vector3(0.97f, 1.1f, 1.22f)),
            (new[] { "rocky_trail" }, "Trail", 5f, Vector3.one),
            (new[] { "aerial_rocks_02" }, "Rock", 8f, Vector3.one),
        };

        [MenuItem("BrokenSovereign/Levels/오픈 필드 지면 실사화 (현재 씬)")]
        private static void ApplyGroundLookMenu()
        {
            Terrain terrain = FindLevelTerrain();

            if (terrain == null)
            {
                EditorUtility.DisplayDialog("지면 실사화", "현재 씬에 Level/Terrain이 없습니다. World 씬을 열고 실행하세요.", "확인");
                return;
            }

            if (!FindGroundSets(out _, out string missing))
            {
                EditorUtility.DisplayDialog("지면 실사화", $"텍스처가 없습니다.\n{GROUND_ROOT} 아래에 다음 폴더가 필요합니다:\n{missing}", "확인");
                return;
            }

            try
            {
                if (ApplyGroundLook(terrain))
                {
                    EditorSceneManager.MarkSceneDirty(terrain.gameObject.scene);
                    EditorSceneManager.SaveScene(terrain.gameObject.scene);
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private static Terrain FindLevelTerrain()
        {
            GameObject level = GameObject.Find("Level");
            Transform t = level != null ? level.transform.Find("Terrain") : null;
            return t != null ? t.GetComponent<Terrain>() : null;
        }

        private static bool HasGroundTextures => FindGroundSets(out _, out _);

        /// <summary>오픈 필드 지면 적용. 실패하면 false (지형은 그대로)</summary>
        private static bool ApplyGroundLook(Terrain terrain)
        {
            if (!FindGroundSets(out string[] folders, out string missing))
            {
                Debug.LogWarning($"LevelTools: 지면 텍스처 없음 ({missing.Replace('\n', ' ')}) → 지면 실사화 건너뜀");
                return false;
            }

            TerrainData data = terrain.terrainData;
            EditorUtility.DisplayProgressBar("지면 실사화", "길·강 위치 읽기", 0.05f);
            Color[] source = EnsureSourceMask(data, out int sourceRes);

            if (source == null)
            {
                return false;
            }

            EditorUtility.DisplayProgressBar("지면 실사화", "텍스처 레이어", 0.15f);
            var layers = new TerrainLayer[GROUND_SETS.Length];

            for (int i = 0; i < layers.Length; i++)
            {
                layers[i] = EnsureGroundLayer(folders[i], GROUND_SETS[i].role, GROUND_SETS[i].tile, GROUND_SETS[i].tint);
            }

            data.terrainLayers = layers;
            float[,,] alpha = PaintGround(terrain, source, sourceRes);
            data.SetAlphamaps(0, 0, alpha);

            terrain.materialTemplate = GroundMaterial();
            // 지형 인스턴싱 끔: 켜면 "Instancing: Unhandled property unity_ObjectToWorldArray·unity_SH*" 경고가 반복됨 (10/22)
            terrain.drawInstanced = false;
            terrain.basemapDistance = 250f;
            EditorUtility.SetDirty(data);

            EditorUtility.DisplayProgressBar("지면 실사화", "풀·잔해", 0.7f);
            PaintClutter(terrain, alpha, source, sourceRes);

            EditorUtility.DisplayProgressBar("지면 실사화", "분위기", 0.9f);
            ApplyGroundAtmosphere();
            DisableRoadDirt();

            AssetDatabase.SaveAssets();
            Debug.Log($"LevelTools: 지면 실사화 적용 (레이어 {layers.Length}종, 알파 {data.alphamapResolution}², 디테일 {data.detailResolution}²)");
            return true;
        }

        // ------------------------------------------------------------------
        // 텍스처 찾기·레이어
        // ------------------------------------------------------------------

        private static bool FindGroundSets(out string[] folders, out string missing)
        {
            folders = new string[GROUND_SETS.Length];
            var lacking = new List<string>();

            for (int i = 0; i < GROUND_SETS.Length; i++)
            {
                string folder = GROUND_SETS[i].folders.Append(GROUND_SETS[i].role)
                    .Select(name => $"{GROUND_ROOT}/{name}")
                    .FirstOrDefault(path => Directory.Exists(path) && FindMap(path, "diff") != null && FindMap(path, "nor") != null);

                if (folder == null)
                {
                    lacking.Add($"- {string.Join(" / ", GROUND_SETS[i].folders)} (또는 {GROUND_SETS[i].role}): Diffuse·Normal(GL) 필요");
                    continue;
                }

                folders[i] = folder;
            }

            missing = string.Join("\n", lacking);
            return lacking.Count == 0;
        }

        /// <summary>폴더 안에서 역할별 맵 찾기 (Poly Haven 파일명 규칙: name_diff_2k.png, name_nor_gl_2k.png ...)</summary>
        private static string FindMap(string folder, string role)
        {
            string[] files = Directory.GetFiles(folder)
                .Where(f => f.EndsWith(".png") || f.EndsWith(".jpg") || f.EndsWith(".jpeg") || f.EndsWith(".tga")
                    || (role == "nor" && f.EndsWith(".exr")))   // 노멀은 EXR 그대로 가져오기 가능, 나머지는 직접 읽어야 해서 PNG·JPG만
                .Where(f => !Path.GetFileName(f).Contains("_bs_"))   // 이 도구가 만든 마스크 제외
                .Select(f => f.Replace('\\', '/'))
                .ToArray();

            bool Has(string f, params string[] keys) => keys.Any(k => Path.GetFileNameWithoutExtension(f).ToLowerInvariant().Contains(k));

            switch (role)
            {
                case "diff":
                    return files.FirstOrDefault(f => Has(f, "_diff", "diffuse", "albedo", "basecolor", "base_color", "_col"));
                case "nor":
                    return files.FirstOrDefault(f => Has(f, "nor_gl", "normal_gl", "normalgl", "opengl"))
                        ?? files.FirstOrDefault(f => Has(f, "_nor", "normal") && !Has(f, "_dx", "normaldx", "directx"));
                case "rough":
                    return files.FirstOrDefault(f => Has(f, "rough"));
                case "disp":
                    return files.FirstOrDefault(f => Has(f, "disp", "height"));
                case "ao":
                    return files.FirstOrDefault(f => Has(f, "_ao", "occlusion"));
                default:
                    return null;
            }
        }

        private static TerrainLayer EnsureGroundLayer(string folder, string role, float tile, Vector3 tint)
        {
            string diffPath = FindMap(folder, "diff");
            string normalPath = FindMap(folder, "nor");
            ConfigureGroundTexture(diffPath, TextureImporterType.Default, true);
            ConfigureGroundTexture(normalPath, TextureImporterType.NormalMap, false);
            string maskPath = EnsureGroundMask(folder, role);

            string layerPath = $"{LAYER_ROOT}/TL_G_{role}.terrainlayer";
            TerrainLayer layer = AssetDatabase.LoadAssetAtPath<TerrainLayer>(layerPath);

            if (layer == null)
            {
                layer = new TerrainLayer();
                AssetDatabase.CreateAsset(layer, layerPath);
            }

            layer.diffuseTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(diffPath);
            layer.normalMapTexture = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            layer.maskMapTexture = maskPath != null ? AssetDatabase.LoadAssetAtPath<Texture2D>(maskPath) : null;
            layer.normalScale = 1f;
            layer.tileSize = new Vector2(tile, tile);
            layer.tileOffset = Vector2.zero;
            layer.metallic = 0f;
            layer.smoothness = 0.1f;
            layer.diffuseRemapMin = Vector4.zero;
            layer.diffuseRemapMax = new Vector4(tint.x, tint.y, tint.z, 1f);
            layer.maskMapRemapMin = Vector4.zero;
            layer.maskMapRemapMax = new Vector4(0f, 1f, 1f, 1f);   // R 금속성 0 고정
            EditorUtility.SetDirty(layer);
            return layer;
        }

        private static void ConfigureGroundTexture(string path, TextureImporterType type, bool srgb)
        {
            if (path == null || !(AssetImporter.GetAtPath(path) is TextureImporter importer))
            {
                return;
            }

            if (importer.textureType == type && importer.sRGBTexture == srgb && importer.maxTextureSize == 2048
                && importer.wrapMode == TextureWrapMode.Repeat && importer.anisoLevel == 8)
            {
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = srgb;
            importer.maxTextureSize = 2048;
            importer.wrapMode = TextureWrapMode.Repeat;
            importer.mipmapEnabled = true;
            importer.anisoLevel = 8;
            importer.SaveAndReimport();
        }

        /// <summary>
        /// Terrain Lit 마스크 맵 (R 금속성 0, G AO, B 높이, A 매끈함 = 1 - 거칠기) 생성.
        /// 원본은 파일에서 직접 읽음 (가져오기 설정 변경 불필요). 높이는 0~1로 늘려 경계 섞기가 잘 보이게.
        /// </summary>
        private static string EnsureGroundMask(string folder, string role)
        {
            string roughPath = FindMap(folder, "rough");
            string dispPath = FindMap(folder, "disp");
            string aoPath = FindMap(folder, "ao");
            string maskPath = $"{folder}/T_{role}_bs_mask.png";

            if (roughPath == null && dispPath == null)
            {
                return null;
            }

            if (File.Exists(maskPath))
            {
                DateTime made = File.GetLastWriteTimeUtc(maskPath);
                bool stale = new[] { roughPath, dispPath, aoPath }.Any(p => p != null && File.GetLastWriteTimeUtc(p) > made);

                if (!stale)
                {
                    ConfigureMaskImporter(maskPath);
                    return maskPath;
                }
            }

            Texture2D rough = LoadRaw(roughPath);
            Texture2D disp = LoadRaw(dispPath);
            Texture2D ao = LoadRaw(aoPath);
            Texture2D reference = rough ?? disp;

            if (reference == null)
            {
                Debug.LogWarning($"LevelTools: {folder} 거칠기·높이 맵을 읽지 못함 → 마스크 없이 사용");
                return null;
            }

            int size = Mathf.Min(2048, reference.width);

            float min = 1f;
            float max = 0f;
            var height = new float[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float h = disp != null ? disp.GetPixelBilinear(u, v).r : 0.5f;
                    height[y * size + x] = h;
                    min = Mathf.Min(min, h);
                    max = Mathf.Max(max, h);
                }
            }

            float range = Mathf.Max(0.0001f, max - min);
            var pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float v = (y + 0.5f) / size;
                    float r = rough != null ? rough.GetPixelBilinear(u, v).r : 0.85f;
                    float o = ao != null ? ao.GetPixelBilinear(u, v).r : 1f;
                    float h = (height[y * size + x] - min) / range;
                    pixels[y * size + x] = new Color(0f, o, h, 1f - r);
                }
            }

            var mask = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
            mask.SetPixels(pixels);
            File.WriteAllBytes(maskPath, mask.EncodeToPNG());

            foreach (Texture2D t in new[] { mask, rough, disp, ao })
            {
                if (t != null)
                {
                    Object.DestroyImmediate(t);
                }
            }

            AssetDatabase.ImportAsset(maskPath);
            ConfigureMaskImporter(maskPath);
            return maskPath;
        }

        private static void ConfigureMaskImporter(string path)
        {
            if (AssetImporter.GetAtPath(path) is TextureImporter importer
                && (importer.sRGBTexture || importer.wrapMode != TextureWrapMode.Repeat || importer.maxTextureSize != 2048))
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = false;
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 2048;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }
        }

        private static Texture2D LoadRaw(string path)
        {
            if (path == null)
            {
                return null;
            }

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            return texture.LoadImage(File.ReadAllBytes(path)) ? texture : null;
        }

        private static Material GroundMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(GROUND_MATERIAL);

            if (material == null)
            {
                material = new Material(Shader.Find(TERRAIN_SHADER));
                AssetDatabase.CreateAsset(material, GROUND_MATERIAL);
            }

            material.shader = Shader.Find(TERRAIN_SHADER);
            material.EnableKeyword("_NORMALMAP");
            material.EnableKeyword("_MASKMAP");
            material.EnableKeyword("_TERRAIN_BLEND_HEIGHT");
            material.DisableKeyword("_TERRAIN_INSTANCED_PERPIXEL_NORMAL");
            material.SetFloat("_EnableHeightBlend", 1f);
            material.SetFloat("_HeightTransition", GROUND_HEIGHT_BLEND);
            material.SetFloat("_EnableInstancedPerPixelNormal", 0f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------
        // 칠하기
        // ------------------------------------------------------------------

        /// <summary>
        /// 길·강 위치 (R 흙길, G 마을 돌바닥, B 강 자갈). 지형이 LevelTools 레이어(7종) 상태면 거기서 읽어 PNG로 저장,
        /// 이미 지면 레이어로 바뀐 상태면 저장해 둔 PNG를 읽음.
        /// </summary>
        private static Color[] EnsureSourceMask(TerrainData data, out int res)
        {
            TerrainLayer[] current = data.terrainLayers;
            bool levelLayout = current.Length > DRY && current[DIRT] != null && current[DIRT].name == "TL_Dirt";

            if (levelLayout)
            {
                res = data.alphamapResolution;
                float[,,] alpha = data.GetAlphamaps(0, 0, res, res);
                var pixels = new Color[res * res];

                for (int z = 0; z < res; z++)
                {
                    for (int x = 0; x < res; x++)
                    {
                        pixels[z * res + x] = new Color(alpha[z, x, DIRT], alpha[z, x, STONE], alpha[z, x, GRAVEL], 1f);
                    }
                }

                var texture = new Texture2D(res, res, TextureFormat.RGB24, false, true);
                texture.SetPixels(pixels);
                File.WriteAllBytes(GROUND_SOURCE_MASK, texture.EncodeToPNG());
                Object.DestroyImmediate(texture);
                AssetDatabase.ImportAsset(GROUND_SOURCE_MASK);
                return pixels;
            }

            Texture2D saved = File.Exists(GROUND_SOURCE_MASK) ? LoadRaw(GROUND_SOURCE_MASK) : null;

            if (saved == null)
            {
                Debug.LogError($"LevelTools: 길·강 위치를 알 수 없음 ({GROUND_SOURCE_MASK} 없음). 오픈 필드를 다시 생성한 뒤 실행하세요.");
                res = 0;
                return null;
            }

            res = saved.width;
            Color[] result = saved.GetPixels();
            Object.DestroyImmediate(saved);
            return result;
        }

        private static Color SampleSource(Color[] source, int sourceRes, float u, float v)
        {
            int x = Mathf.Clamp((int)(u * sourceRes), 0, sourceRes - 1);
            int z = Mathf.Clamp((int)(v * sourceRes), 0, sourceRes - 1);
            return source[z * sourceRes + x];
        }

        private static float[,,] PaintGround(Terrain terrain, Color[] source, int sourceRes)
        {
            TerrainData data = terrain.terrainData;
            int res = data.alphamapResolution;
            var map = new float[res, res, GROUND_SETS.Length];
            var w = new float[GROUND_SETS.Length];
            Vector3 origin = terrain.transform.position;

            for (int z = 0; z < res; z++)
            {
                if (z % 128 == 0)
                {
                    EditorUtility.DisplayProgressBar("지면 실사화", "바닥 칠하기", 0.2f + 0.45f * z / res);
                }

                for (int x = 0; x < res; x++)
                {
                    float u = (x + 0.5f) / res;
                    float v = (z + 0.5f) / res;
                    float wx = origin.x + u * data.size.x;
                    float wz = origin.z + v * data.size.z;
                    Color src = SampleSource(source, sourceRes, u, v);
                    GroundWeights(wx, wz, data.GetSteepness(u, v), src, w);

                    for (int i = 0; i < w.Length; i++)
                    {
                        map[z, x, i] = w[i];
                    }
                }
            }

            return map;
        }

        /// <summary>흙먼지 바탕 위에 갈라진 땅(큰 얼룩·길 어깨·강가) → 자갈 흙길(길·강바닥) → 바위(경사) 순서로 덮음</summary>
        private static void GroundWeights(float wx, float wz, float steep, Color src, float[] w)
        {
            float road = src.r;
            float stone = src.g;
            float river = src.b;
            float patch = Fbm(wx * 0.035f, wz * 0.035f, 3, GROUND_NOISE);         // 약 30m 얼룩
            float breakup = Fbm(wx * 0.15f, wz * 0.15f, 2, GROUND_NOISE + 11f);   // 약 7m 들쭉날쭉

            float trail = SmoothStep(0.25f, 0.6f, road + (breakup - 0.5f) * 0.35f);
            trail = Mathf.Max(trail, stone);
            trail = Mathf.Max(trail, SmoothStep(0.55f, 0.85f, river));

            // 갈라진 땅 얼룩: 면적 약 40~50%, 가장자리는 들쭉날쭉 (높이 기반 섞기로 판 가장자리부터 드러남)
            float crackPatch = SmoothStep(CRACK_PATCH - 0.05f, CRACK_PATCH + 0.07f, patch + (breakup - 0.5f) * 0.35f);
            float shoulder = SmoothStep(0.02f, 0.25f, road) * (1f - trail) * 0.6f;
            float bank = SmoothStep(0.05f, 0.4f, river) * (1f - trail);
            float crack = Mathf.Max(crackPatch, Mathf.Max(shoulder, bank));
            float rock = SmoothStep(32f, 44f, steep) * (1f - stone);

            Array.Clear(w, 0, w.Length);
            w[G_BASE] = 1f;
            Blend(w, G_CRACK, crack);
            Blend(w, G_TRAIL, trail);
            Blend(w, G_ROCK, rock);
            Normalize(w);
        }

        // ------------------------------------------------------------------
        // 풀·잔해 (Terrain 디테일, 인스턴싱 메시)
        // ------------------------------------------------------------------

        private static void PaintClutter(Terrain terrain, float[,,] alpha, Color[] source, int sourceRes)
        {
            TerrainData data = terrain.terrainData;
            GameObject[] prefabs = EnsureClutterPrefabs();

            data.SetDetailScatterMode(DetailScatterMode.InstanceCountMode);
            data.detailPrototypes = new[]
            {
                ClutterPrototype(prefabs[0], 0.4f, 0.8f, 0.25f, 0.55f, 0f),    // 마른 풀 (짚색)
                ClutterPrototype(prefabs[1], 0.35f, 0.7f, 0.2f, 0.45f, 0f),     // 마른 풀 (올리브)
                ClutterPrototype(prefabs[2], 0.08f, 0.3f, 0.06f, 0.22f, 0f),     // 조약돌
                ClutterPrototype(prefabs[3], 0.3f, 0.75f, 0.6f, 1f, 0f),         // 나뭇가지
            };

            const int res = 1024;
            data.SetDetailResolution(res, 32);
            int alphaRes = alpha.GetLength(0);
            Vector3 origin = terrain.transform.position;
            var straw = new int[res, res];
            var olive = new int[res, res];
            var pebble = new int[res, res];
            var twig = new int[res, res];
            var rng = new Random(4242);

            for (int z = 0; z < res; z++)
            {
                for (int x = 0; x < res; x++)
                {
                    int ax = Mathf.Min(alphaRes - 1, x * alphaRes / res);
                    int az = Mathf.Min(alphaRes - 1, z * alphaRes / res);
                    float u = (x + 0.5f) / res;
                    float v = (z + 0.5f) / res;
                    float wx = origin.x + u * data.size.x;
                    float wz = origin.z + v * data.size.z;
                    float baseW = alpha[az, ax, G_BASE];
                    float crack = alpha[az, ax, G_CRACK];
                    float trail = alpha[az, ax, G_TRAIL];
                    Color src = SampleSource(source, sourceRes, u, v);
                    float wet = src.b > 0.05f ? 0f : 1f;   // 강 물가·물속은 풀 없음

                    // 마른 땅에 드문드문 난 풀 (10/22 참고 이미지): 작은 풀 무더기(약 3~6m)만 군데군데, 그 밖은 아주 가끔 한 포기
                    float grass = (baseW + 0.5f * crack) * wet;
                    float patchA = SmoothStep(GRASS_PATCH, GRASS_PATCH + 0.06f, Fbm(wx * 0.11f, wz * 0.11f, 3, GROUND_NOISE + 23f));
                    float patchB = SmoothStep(GRASS_PATCH + 0.02f, GRASS_PATCH + 0.08f, Fbm(wx * 0.09f, wz * 0.09f, 3, GROUND_NOISE + 41f));
                    double roll = rng.NextDouble();

                    straw[z, x] = grass > 0.3f && roll < patchA * 0.7f ? 1 + (roll < patchA * 0.25f ? 1 : 0) : (grass > 0.5f && roll > 1 - GRASS_STRAY ? 1 : 0);
                    olive[z, x] = grass > 0.3f && rng.NextDouble() < patchB * 0.5f ? 1 : 0;

                    double r2 = rng.NextDouble();
                    pebble[z, x] = trail > 0.3f ? (r2 < 0.5 ? 1 + rng.Next(2) : 0) : (r2 < 0.04 ? 1 : 0);
                    twig[z, x] = (baseW + crack) > 0.5f && rng.NextDouble() < 0.05 ? 1 : 0;
                }
            }

            data.SetDetailLayer(0, 0, 0, straw);
            data.SetDetailLayer(0, 0, 1, olive);
            data.SetDetailLayer(0, 0, 2, pebble);
            data.SetDetailLayer(0, 0, 3, twig);
            terrain.detailObjectDistance = DETAIL_DISTANCE;
            terrain.detailObjectDensity = 1f;
            terrain.drawTreesAndFoliage = true;
        }

        private static DetailPrototype ClutterPrototype(GameObject prefab, float minW, float maxW, float minH, float maxH, float alignPercent)
        {
            return new DetailPrototype
            {
                prototype = prefab,
                usePrototypeMesh = true,
                renderMode = DetailRenderMode.VertexLit,
                useInstancing = true,
                minWidth = minW,
                maxWidth = maxW,
                minHeight = minH,
                maxHeight = maxH,
                noiseSpread = 0.4f,
                healthyColor = Color.white,
                dryColor = Color.white,
                alignToGround = alignPercent,   // 0 = 똑바로 (값을 주면 Assertion det 오류가 대량 발생해 끔, 10/22)
            };
        }

        /// <summary>풀 다발 2종·조약돌·나뭇가지 프리팹 (메시·텍스처·머티리얼 모두 코드 생성)</summary>
        private static GameObject[] EnsureClutterPrefabs()
        {
            ModelAssetUtility.EnsureFolder(CLUTTER_MESH_ROOT);
            ModelAssetUtility.EnsureFolder(CLUTTER_PREFAB_ROOT);
            ModelAssetUtility.EnsureFolder(CLUTTER_MATERIAL_ROOT);

            Mesh clump = SaveMesh(GrassClumpMesh(), $"{CLUTTER_MESH_ROOT}/GrassClump.asset");
            Mesh stone = SaveMesh(PebbleMesh(), $"{CLUTTER_MESH_ROOT}/Pebble.asset");
            Mesh stick = SaveMesh(TwigMesh(), $"{CLUTTER_MESH_ROOT}/Twig.asset");

            Texture2D strawTex = GrassBladeTexture("A", new Color(0.16f, 0.13f, 0.07f), new Color(0.66f, 0.57f, 0.34f), 11);
            Texture2D oliveTex = GrassBladeTexture("B", new Color(0.12f, 0.13f, 0.06f), new Color(0.45f, 0.45f, 0.24f), 29);

            return new[]
            {
                SavePrefab("P_GrassStraw", clump, GrassMaterial("M_GrassStraw", strawTex), false),
                SavePrefab("P_GrassOlive", clump, GrassMaterial("M_GrassOlive", oliveTex), false),
                SavePrefab("P_Pebble", stone, SolidMaterial("M_Pebble", new Color(0.34f, 0.32f, 0.29f), 0.15f), true),
                SavePrefab("P_Twig", stick, SolidMaterial("M_Twig", new Color(0.2f, 0.15f, 0.1f), 0.05f), true),
            };
        }

        private static Mesh SaveMesh(Mesh mesh, string path)
        {
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.triangles = mesh.triangles;
            existing.RecalculateBounds();
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        private static GameObject SavePrefab(string name, Mesh mesh, Material material, bool shadows)
        {
            var go = new GameObject(name);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = shadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(go, $"{CLUTTER_PREFAB_ROOT}/{name}.prefab");
            Object.DestroyImmediate(go);
            return prefab;
        }

        private static Material LoadOrCreateLit(string name)
        {
            string path = $"{CLUTTER_MATERIAL_ROOT}/{name}.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(Shader.Find(ModelAssetUtility.URP_LIT));
                AssetDatabase.CreateAsset(material, path);
            }

            material.enableInstancing = true;
            material.SetFloat("_SpecularHighlights", 0f);
            material.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            material.SetFloat("_EnvironmentReflections", 0f);
            material.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            return material;
        }

        private static Material GrassMaterial(string name, Texture2D texture)
        {
            Material material = LoadOrCreateLit(name);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", Color.white);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Cutoff", 0.45f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.SetFloat("_Cull", 0f);   // 양면
            material.SetFloat("_Smoothness", 0.05f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = (int)RenderQueue.AlphaTest;
            material.doubleSidedGI = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material SolidMaterial(string name, Color color, float smoothness)
        {
            Material material = LoadOrCreateLit(name);
            material.SetColor("_BaseColor", color);
            material.SetFloat("_Smoothness", smoothness);
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>풀잎 그림 (투명 배경, 아래 어둡고 위로 밝게, 휘어진 가는 잎 다수)</summary>
        private static Texture2D GrassBladeTexture(string id, Color root, Color tip, int seed)
        {
            string folder = TERRAIN_ROOT + "/Clutter";
            string path = $"{folder}/T_GrassBlades{id}.png";
            ModelAssetUtility.EnsureFolder(folder);

            const int size = 256;
            var pixels = new Color[size * size];
            var rng = new Random(seed);

            for (int blade = 0; blade < 46; blade++)
            {
                float baseX = 0.12f + 0.76f * (float)rng.NextDouble();
                float lean = ((float)rng.NextDouble() - 0.5f) * 0.6f;
                float height = 0.45f + 0.53f * (float)rng.NextDouble();
                float width = 0.008f + 0.014f * (float)rng.NextDouble();
                float shade = 0.7f + 0.45f * (float)rng.NextDouble();
                bool dead = rng.NextDouble() < 0.25;
                int top = Mathf.RoundToInt(height * size);

                for (int py = 0; py < top; py++)
                {
                    float t = (float)py / top;
                    float cx = (baseX + lean * t * t) * size;
                    float hw = (width * Mathf.Pow(1f - t, 0.7f) + 0.0015f) * size;
                    Color c = Color.Lerp(root, tip, Mathf.Pow(t, 0.7f)) * shade;

                    if (dead)
                    {
                        c = Color.Lerp(c, new Color(0.5f, 0.42f, 0.3f), 0.5f);
                    }

                    int x0 = Mathf.Max(0, Mathf.FloorToInt(cx - hw - 1));
                    int x1 = Mathf.Min(size - 1, Mathf.CeilToInt(cx + hw + 1));

                    for (int px = x0; px <= x1; px++)
                    {
                        float coverage = Mathf.Clamp01(hw - Mathf.Abs(px + 0.5f - cx) + 0.5f);

                        if (coverage <= 0f)
                        {
                            continue;
                        }

                        int i = py * size + px;
                        Color under = pixels[i];
                        float a = coverage + under.a * (1f - coverage);
                        Color mixed = (c * coverage + under * under.a * (1f - coverage)) / Mathf.Max(a, 0.0001f);
                        mixed.a = a;
                        pixels[i] = mixed;
                    }
                }
            }

            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            AssetDatabase.ImportAsset(path);

            if (AssetImporter.GetAtPath(path) is TextureImporter importer)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.45f;
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        /// <summary>풀 다발: 폭 1·높이 1 판 3장을 60°씩 교차, 위로 살짝 벌어짐. 법선은 위쪽(위에서 볼 때 고르게 밝게)</summary>
        private static Mesh GrassClumpMesh()
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();

            for (int i = 0; i < 3; i++)
            {
                Quaternion yaw = Quaternion.Euler(0f, i * 60f, 0f);
                Vector3 side = yaw * Vector3.right * 0.5f;
                Vector3 spread = yaw * Vector3.forward * (i % 2 == 0 ? 0.12f : -0.12f);
                int start = vertices.Count;
                vertices.Add(-side);
                vertices.Add(side);
                vertices.Add(-side * 1.15f + Vector3.up + spread);
                vertices.Add(side * 1.15f + Vector3.up + spread);
                uvs.AddRange(new[] { new Vector2(0f, 0f), new Vector2(1f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f) });

                for (int k = 0; k < 4; k++)
                {
                    normals.Add(Vector3.up);
                }

                triangles.AddRange(new[] { start, start + 2, start + 1, start + 1, start + 2, start + 3 });
            }

            var mesh = new Mesh { name = "GrassClump" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>조약돌: 지름 1 울퉁불퉁한 구를 납작하게, 각진 면(평면 법선), 아래 일부는 땅에 묻힘</summary>
        private static Mesh PebbleMesh()
        {
            const int rings = 5;
            const int segments = 8;
            var rng = new Random(91);
            var grid = new Vector3[(rings + 1) * segments];

            for (int r = 0; r <= rings; r++)
            {
                float phi = Mathf.PI * r / rings;

                for (int s = 0; s < segments; s++)
                {
                    float theta = Mathf.PI * 2f * s / segments;
                    float radius = 0.5f * (0.82f + 0.3f * (float)rng.NextDouble());
                    var p = new Vector3(Mathf.Sin(phi) * Mathf.Cos(theta), Mathf.Cos(phi), Mathf.Sin(phi) * Mathf.Sin(theta)) * radius;
                    p.y = p.y * 0.55f + 0.12f;
                    grid[r * segments + s] = p;
                }
            }

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            void Tri(Vector3 a, Vector3 b, Vector3 c)
            {
                triangles.Add(vertices.Count);
                vertices.Add(a);
                triangles.Add(vertices.Count);
                vertices.Add(b);
                triangles.Add(vertices.Count);
                vertices.Add(c);
            }

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int n = (s + 1) % segments;
                    Vector3 a = grid[r * segments + s];
                    Vector3 b = grid[r * segments + n];
                    Vector3 c = grid[(r + 1) * segments + s];
                    Vector3 d = grid[(r + 1) * segments + n];
                    Tri(a, b, c);
                    Tri(b, d, c);
                }
            }

            var mesh = new Mesh { name = "Pebble" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, new Vector2[vertices.Count]);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>나뭇가지: 길이 1(x축) 굽은 가는 원통, 끝으로 갈수록 가늘게, 땅에 누움</summary>
        private static Mesh TwigMesh()
        {
            const int segments = 6;
            const int sides = 4;
            var rng = new Random(57);
            var vertices = new List<Vector3>();
            var triangles = new List<int>();
            var center = new Vector3[segments + 1];

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                center[i] = new Vector3(t - 0.5f, 0.03f + 0.02f * (float)rng.NextDouble(), 0.06f * Mathf.Sin(t * 5f) + 0.02f * ((float)rng.NextDouble() - 0.5f));
            }

            for (int i = 0; i <= segments; i++)
            {
                float radius = Mathf.Lerp(0.03f, 0.012f, (float)i / segments);

                for (int k = 0; k < sides; k++)
                {
                    float a = Mathf.PI * 2f * k / sides;
                    vertices.Add(center[i] + new Vector3(0f, Mathf.Cos(a), Mathf.Sin(a)) * radius);
                }
            }

            for (int i = 0; i < segments; i++)
            {
                for (int k = 0; k < sides; k++)
                {
                    int a = i * sides + k;
                    int b = i * sides + (k + 1) % sides;
                    int c = a + sides;
                    int d = b + sides;
                    triangles.AddRange(new[] { a, c, b, b, c, d });
                }
            }

            var mesh = new Mesh { name = "Twig" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.SetUVs(0, new Vector2[vertices.Count]);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------
        // 분위기·길 덮개
        // ------------------------------------------------------------------

        /// <summary>
        /// 마른 땅 톤 (10/22 참고 이미지로 변경): 밝은 황토색 땅에 따뜻한 강한 햇빛, 옅은 모래빛 안개, 채도는 그대로·대비 약간.
        /// (처음 안: 회청색 안개·어두운 주변광·채도 -25 → 참고 이미지와 달라 폐기)
        /// </summary>
        private static void ApplyGroundAtmosphere()
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.8f, 0.72f, 0.6f);
            RenderSettings.fogStartDistance = 60f;
            RenderSettings.fogEndDistance = 450f;
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.62f, 0.6f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.45f, 0.38f);
            RenderSettings.ambientGroundColor = new Color(0.3f, 0.26f, 0.2f);

            Light sun = RenderSettings.sun;

            if (sun == null)
            {
                sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            }

            if (sun != null)
            {
                sun.color = new Color(1f, 0.93f, 0.8f);
                sun.intensity = 1.8f;
                sun.shadows = LightShadows.Soft;
                EditorUtility.SetDirty(sun);
            }

            VolumeProfile profile = WorldVolumeProfile();

            foreach (Volume volume in Object.FindObjectsByType<Volume>(FindObjectsSortMode.None))
            {
                if (volume.isGlobal)
                {
                    volume.sharedProfile = profile;
                    EditorUtility.SetDirty(volume);
                }
            }

            DynamicGI.UpdateEnvironment();
        }

        /// <summary>오픈 필드 전용 후처리 (공용 FieldVolumeProfile은 보스 방·테스트 씬도 쓰므로 복사본)</summary>
        private static VolumeProfile WorldVolumeProfile()
        {
            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(WORLD_VOLUME);

            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, WORLD_VOLUME);
                VolumeProfile field = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FIELD_VOLUME);

                if (field != null)
                {
                    foreach (VolumeComponent component in field.components)
                    {
                        VolumeComponent copy = Object.Instantiate(component);
                        copy.name = component.name;
                        profile.components.Add(copy);
                        AssetDatabase.AddObjectToAsset(copy, profile);
                    }
                }
            }

            ColorAdjustments color = GetOrAddVolume<ColorAdjustments>(profile);
            color.postExposure.Override(0f);
            color.contrast.Override(8f);
            color.saturation.Override(0f);
            color.colorFilter.Override(new Color(1f, 0.97f, 0.92f));

            Vignette vignette = GetOrAddVolume<Vignette>(profile);
            vignette.intensity.Override(0.25f);
            vignette.smoothness.Override(0.45f);

            EditorUtility.SetDirty(profile);
            return profile;
        }

        private static T GetOrAddVolume<T>(VolumeProfile profile) where T : VolumeComponent
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

        /// <summary>길 덮개의 흙길은 끔 (지형의 자갈 흙길 레이어가 대신), 마을 돌바닥만 남김</summary>
        private static void DisableRoadDirt()
        {
            Material road = AssetDatabase.LoadAssetAtPath<Material>(ROAD_MATERIAL);

            if (road != null && road.HasProperty("_DirtAmount"))
            {
                road.SetFloat("_DirtAmount", 0f);
                EditorUtility.SetDirty(road);
            }
        }
    }
}
