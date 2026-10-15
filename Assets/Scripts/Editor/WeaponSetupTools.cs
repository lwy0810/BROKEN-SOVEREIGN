using System.Linq;
using BrokenSovereign.Ascension;
using BrokenSovereign.Equipment;
using BrokenSovereign.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 무기 에셋 생성 도구 (에디터 전용).
    /// 메뉴: BrokenSovereign > Weapons
    ///
    /// 무기 에셋 생성 (전체)
    /// 1. Assets/Models/Weapon{이름}/ FBX 임포트 설정 (리그 없음), 텍스처·머티리얼 Materials/Weapons/M_Weapon{이름}
    /// 2. 프리팹 Prefabs/Weapons/Weapon{이름} (콜라이더 없음, 정적 아님)
    /// 3. 무기 정의 Data/Equipment/Weapon{이름} (공격 간격, 손잡이 위치·회전)
    /// 4. 직업에 기본 무기 연결 (성기사 = 메이스, 성투사 = 장봉)
    ///
    /// 손잡이 위치·회전은 성기사 모델의 T포즈 오른손에서 계산합니다.
    /// 무기 모델 규칙: 원점 = 손잡이, +Y = 무기 머리 방향 (Blender 프리셋 pivot_height가 맞춤)
    /// T포즈에서 무기 머리가 캐릭터 정면(+Z)을 향하도록 쥐게 합니다.
    /// </summary>
    public static class WeaponSetupTools
    {
        private const string MATERIAL_ROOT = "Assets/Materials/Weapons";
        private const string PREFAB_ROOT = "Assets/Prefabs/Weapons";
        private const string DATA_ROOT = "Assets/Data/Equipment";
        private const string GRIP_MODEL_PATH = "Assets/Prefabs/Characters/PaladinModel.prefab";

        // 손바닥 중심 = 손목 → 가운데 손가락 시작점 사이 비율, 손바닥 쪽(T포즈 아래)으로 내림
        private const float PALM_RATIO = 0.6f;
        private const float PALM_DROP = 0.025f;

        /// <summary>무기 1종의 설정. 새 무기는 이 목록에 추가합니다.</summary>
        private readonly struct WeaponSpec
        {
            public readonly string Name;
            public readonly string DisplayName;
            public readonly WeaponType Type;
            public readonly float Interval;
            public readonly string JobPath;

            public WeaponSpec(string name, string displayName, WeaponType type, float interval, string jobPath)
            {
                Name = name;
                DisplayName = displayName;
                Type = type;
                Interval = interval;
                JobPath = jobPath;
            }

            public string Folder => $"{CharacterModelImporter.MODELS_ROOT}{Name}";
            public string ModelPath => $"{Folder}/{Name}.fbx";
            public string MaterialPath => $"{MATERIAL_ROOT}/M_{Name}.mat";
            public string PrefabPath => $"{PREFAB_ROOT}/{Name}.prefab";
            public string DataPath => $"{DATA_ROOT}/{Name}.asset";
        }

        private static readonly WeaponSpec[] WEAPONS =
        {
            new WeaponSpec("WeaponHolyMace", "성광 철퇴", WeaponType.OneHandBlunt, 1.0f, "Assets/Data/Ascension/JobPaladin.asset"),
            new WeaponSpec("WeaponCelestialStaff", "천계 장봉", WeaponType.Staff, 1.3f, "Assets/Data/Ascension/JobHolyWarrior.asset"),
        };

        [MenuItem("BrokenSovereign/Weapons/무기 에셋 생성 (전체)")]
        private static void BuildAll()
        {
            if (!TryComputeGrip(out Vector3 gripPosition, out Vector3 gripEuler))
            {
                return;
            }

            int built = 0;

            foreach (WeaponSpec spec in WEAPONS)
            {
                GameObject prefab = BuildPrefab(spec);
                WeaponDefinition definition = BuildDefinition(spec, prefab, gripPosition, gripEuler);
                LinkJob(spec, definition);
                built += prefab != null ? 1 : 0;
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"WeaponSetupTools: 무기 정의 {WEAPONS.Length}개, 프리팹 {built}개 생성 (손잡이 {gripPosition}, {gripEuler})");
        }

        [MenuItem("BrokenSovereign/Weapons/무기 장착 (선택한 플레이어)")]
        private static void AttachToSelectedPlayer()
        {
            PlayerController player = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PlayerController>()
                : null;

            if (player == null)
            {
                EditorUtility.DisplayDialog("무기 장착", "Hierarchy에서 플레이어(PlayerController가 있는 오브젝트)를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            WeaponDefinition fallback = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(WEAPONS[0].DataPath);

            if (fallback == null)
            {
                EditorUtility.DisplayDialog("무기 장착", "먼저 '무기 에셋 생성 (전체)'를 실행하세요.", "확인");
                return;
            }

            GameObject root = player.gameObject;
            PlayerWeapon weapon = root.GetComponent<PlayerWeapon>();

            if (weapon == null)
            {
                weapon = Undo.AddComponent<PlayerWeapon>(root);
            }

            Undo.RecordObject(weapon, "Setup PlayerWeapon");
            weapon.EditorSetDefaultWeapon(fallback);
            EditorUtility.SetDirty(weapon);
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"WeaponSetupTools: {root.name}에 PlayerWeapon 연결 (무직 기본 무기 {fallback.DisplayName}). 씬을 저장하세요.");
        }

        // ------------------------------------------------------------------
        // 에셋 생성
        // ------------------------------------------------------------------

        private static GameObject BuildPrefab(WeaponSpec spec)
        {
            if (AssetImporter.GetAtPath(spec.ModelPath) is not ModelImporter importer)
            {
                Debug.LogWarning($"WeaponSetupTools: 모델 없음 ({spec.ModelPath}). 외형 없이 무기 정의만 만듭니다.");
                return null;
            }

            CharacterModelImporter.ApplyPropSettings(importer);
            importer.SaveAndReimport();
            ModelAssetUtility.ConfigureTextures(spec.Folder, spec.Name);

            Material material = ModelAssetUtility.BuildLitMaterial(spec.MaterialPath, spec.Folder, spec.Name);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = spec.Name;

            foreach (MeshRenderer renderer in instance.GetComponentsInChildren<MeshRenderer>())
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            // 손에 붙어 움직이므로 콜라이더·Static 없음 (피격 판정은 BasicAttack이 담당)
            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, spec.PrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        private static WeaponDefinition BuildDefinition(WeaponSpec spec, GameObject prefab, Vector3 gripPosition, Vector3 gripEuler)
        {
            ModelAssetUtility.EnsureFolder(DATA_ROOT);
            WeaponDefinition definition = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(spec.DataPath);

            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<WeaponDefinition>();
                AssetDatabase.CreateAsset(definition, spec.DataPath);
            }

            definition.EditorSetup(ToId(spec.Name), spec.DisplayName, spec.Type, spec.Interval);
            definition.EditorSetVisual(prefab, gripPosition, gripEuler);
            EditorUtility.SetDirty(definition);
            return definition;
        }

        private static void LinkJob(WeaponSpec spec, WeaponDefinition definition)
        {
            JobDefinition job = AssetDatabase.LoadAssetAtPath<JobDefinition>(spec.JobPath);

            if (job == null)
            {
                Debug.LogWarning($"WeaponSetupTools: 직업 없음 ({spec.JobPath})");
                return;
            }

            job.EditorSetDefaultWeapon(definition);
            EditorUtility.SetDirty(job);
        }

        // ------------------------------------------------------------------
        // 손잡이 계산
        // ------------------------------------------------------------------

        /// <summary>T포즈 성기사 모델의 오른손 본 기준 손잡이 위치·회전을 구합니다.</summary>
        private static bool TryComputeGrip(out Vector3 localPosition, out Vector3 localEuler)
        {
            localPosition = Vector3.zero;
            localEuler = Vector3.zero;

            GameObject modelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(GRIP_MODEL_PATH);

            if (modelPrefab == null)
            {
                EditorUtility.DisplayDialog("무기 에셋 생성", $"손잡이 기준 모델이 없습니다.\n{GRIP_MODEL_PATH}", "확인");
                return false;
            }

            GameObject instance = Object.Instantiate(modelPrefab, Vector3.zero, Quaternion.identity);

            try
            {
                Animator animator = instance.GetComponentInChildren<Animator>();
                Transform hand = animator != null && animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.RightHand) : null;

                if (hand == null)
                {
                    EditorUtility.DisplayDialog("무기 에셋 생성", "기준 모델에서 Humanoid 오른손 본을 찾지 못했습니다.", "확인");
                    return false;
                }

                Transform middle = animator.GetBoneTransform(HumanBodyBones.RightMiddleProximal);
                Transform lowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                Vector3 palm = middle != null
                    ? Vector3.Lerp(hand.position, middle.position, PALM_RATIO)
                    : hand.position + (hand.position - lowerArm.position).normalized * 0.06f;
                palm += Vector3.down * PALM_DROP;

                // 무기 +Y(머리) = 캐릭터 정면, 무기 +Z = 캐릭터 오른쪽 바깥
                Quaternion world = Quaternion.LookRotation(Vector3.right, Vector3.forward);

                localPosition = hand.InverseTransformPoint(palm);
                localEuler = (Quaternion.Inverse(hand.rotation) * world).eulerAngles;
                return true;
            }
            finally
            {
                Object.DestroyImmediate(instance);
            }
        }

        private static string ToId(string name)
        {
            // WeaponHolyMace → weapon_holy_mace
            return string.Concat(name.Select((c, i) => i > 0 && char.IsUpper(c) ? "_" + char.ToLower(c) : char.ToLower(c).ToString()));
        }
    }
}
