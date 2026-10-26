using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 환경 소품 에셋 생성 도구 (에디터 전용).
    /// 메뉴: BrokenSovereign > Props > 소품 에셋 생성 (전체)
    ///
    /// Assets/Models/Prop{이름}/·Town{이름}/ 폴더마다 (Town = 마을 건물, 10/22)
    /// 1. FBX 임포트 설정 (리그 없음, 머티리얼 가져오지 않음)
    /// 2. 텍스처 설정, URP Lit 머티리얼 Materials/Props/M_Prop{이름}
    /// 3. 프리팹 Prefabs/Props/Prop{이름} (MeshCollider, Static)
    /// 씬 배치는 프리팹을 끌어다 놓아 직접 하고, 배치 후 NavMeshSurface를 다시 굽습니다.
    /// </summary>
    public static class PropSetupTools
    {
        private const string MATERIAL_ROOT = "Assets/Materials/Props";
        private const string PREFAB_ROOT = "Assets/Prefabs/Props";

        [MenuItem("BrokenSovereign/Props/소품 에셋 생성 (전체)")]
        private static void BuildAll()
        {
            string[] folders = AssetDatabase.GetSubFolders(CharacterModelImporter.MODELS_ROOT.TrimEnd('/'))
                .Where(f => System.IO.Path.GetFileName(f).StartsWith(CharacterModelImporter.PROP_PREFIX)
                    || System.IO.Path.GetFileName(f).StartsWith(CharacterModelImporter.TOWN_PREFIX))
                .ToArray();

            int built = 0;

            foreach (string folder in folders)
            {
                if (Build(System.IO.Path.GetFileName(folder), folder))
                {
                    built++;
                }
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"PropSetupTools: 소품 {built}/{folders.Length}개 생성 ({PREFAB_ROOT})");
        }

        /// <summary>소품 하나만 생성 (10/26 보스 계단 등). folder = Assets/Models/{name}</summary>
        public static bool BuildOne(string name)
        {
            bool ok = Build(name, $"{CharacterModelImporter.MODELS_ROOT.TrimEnd('/')}/{name}");
            AssetDatabase.SaveAssets();
            return ok;
        }

        private static bool Build(string name, string folder)
        {
            string modelPath = $"{folder}/{name}.fbx";

            if (AssetImporter.GetAtPath(modelPath) is not ModelImporter importer)
            {
                Debug.LogWarning($"PropSetupTools: 모델 없음 ({modelPath})");
                return false;
            }

            CharacterModelImporter.ApplyPropSettings(importer);
            importer.SaveAndReimport();
            ModelAssetUtility.ConfigureTextures(folder, name);

            Material material = ModelAssetUtility.BuildLitMaterial($"{MATERIAL_ROOT}/M_{name}.mat", folder, name);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = name;

            foreach (MeshFilter filter in instance.GetComponentsInChildren<MeshFilter>())
            {
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();

                if (renderer != null)
                {
                    renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
                }

                // 클릭 이동 레이캐스트·피격 판정이 메시 모양을 따르도록 MeshCollider 사용
                MeshCollider collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }

            // 길찾기는 NavMeshSurface 굽기에 맡김 (상자 장애물은 패루 통로까지 막으므로 사용하지 않음)
            GameObjectUtility.SetStaticEditorFlags(instance, StaticEditorFlags.BatchingStatic);

            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);
            PrefabUtility.SaveAsPrefabAsset(instance, $"{PREFAB_ROOT}/{name}.prefab");
            Object.DestroyImmediate(instance);
            return true;
        }
    }
}
