using System.Linq;
using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 적 3종 생성 도구 (에디터 전용). 메뉴: BrokenSovereign > Enemies > 적 3종 생성 (선택한 적 기준)
    ///
    /// 1. 천병·역사 모델 에셋 (임포트·머티리얼·Animator·모델 프리팹, CharacterSetupTools 공용 처리)
    ///    정예용 금빛 오라 머티리얼 M_EliteAura (본 머티리얼 뒤 두 번째 머티리얼로 추가)
    /// 2. 적 데이터: 천병 = 기존 BasicEnemy, 정예·역사는 없을 때만 BasicEnemy를 복사해 수치 지정 (이후 인스펙터 값 유지)
    /// 3. 선택한 씬 적(EnemyController)을 틀로 프리팹 Prefabs/Enemies/Enemy{이름} 생성
    ///    도형 부품은 끄고 Visual 아래에 모델 배치, EnemyAnimator 연결. 정예는 크기 1.25배 + 금빛 오라 (GDD: 크기·외형 변형)
    /// </summary>
    public static class EnemySetupTools
    {
        private const string DATA_ROOT = "Assets/Data/Enemies";
        private const string PREFAB_ROOT = "Assets/Prefabs/Enemies";
        private const string BASE_DEFINITION = DATA_ROOT + "/BasicEnemy.asset";
        private const string BASE_STATS = DATA_ROOT + "/BasicEnemyStats.asset";
        private const string AURA_MATERIAL = "Assets/Materials/Characters/M_EliteAura.mat";
        private const string AURA_SHADER = "BrokenSovereign/Character/EliteAura";

        private const string HEAVEN_SOLDIER = "HeavenSoldier";
        private const string TEMPLE_GUARDIAN = "TempleGuardian";

        private readonly struct EnemySpec
        {
            public readonly string PrefabName;
            public readonly string ModelName;
            public readonly string DefinitionPath;
            public readonly string StatsPath;
            public readonly float Scale;
            public readonly bool IsElite;

            public EnemySpec(string prefabName, string modelName, string definitionPath, string statsPath, float scale, bool isElite)
            {
                PrefabName = prefabName;
                ModelName = modelName;
                DefinitionPath = definitionPath;
                StatsPath = statsPath;
                Scale = scale;
                IsElite = isElite;
            }
        }

        private static readonly EnemySpec[] ENEMIES =
        {
            new EnemySpec("EnemyHeavenSoldier", HEAVEN_SOLDIER, BASE_DEFINITION, BASE_STATS, 1f, false),
            new EnemySpec("EnemyHeavenSoldierElite", HEAVEN_SOLDIER, DATA_ROOT + "/HeavenSoldierElite.asset",
                DATA_ROOT + "/HeavenSoldierEliteStats.asset", 1.25f, true),
            new EnemySpec("EnemyTempleGuardian", TEMPLE_GUARDIAN, DATA_ROOT + "/TempleGuardian.asset",
                DATA_ROOT + "/TempleGuardianStats.asset", 1f, false),
        };

        [MenuItem("BrokenSovereign/Enemies/적 3종 생성 (선택한 적 기준)")]
        private static void BuildAll()
        {
            EnemyController template = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<EnemyController>()
                : null;

            if (template == null)
            {
                EditorUtility.DisplayDialog("적 3종 생성", "Hierarchy에서 틀로 쓸 적(EnemyController가 있는 오브젝트, 예: DummyEnemy)을 선택한 뒤 실행하세요.", "확인");
                return;
            }

            GameObject soldier = CharacterSetupTools.BuildModelAssets(HEAVEN_SOLDIER, out float soldierAttack);
            GameObject guardian = CharacterSetupTools.BuildModelAssets(TEMPLE_GUARDIAN, out float guardianAttack);

            if (soldier == null || guardian == null)
            {
                return;
            }

            Material aura = BuildAuraMaterial();
            EnsureData();
            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);

            foreach (EnemySpec spec in ENEMIES)
            {
                bool isSoldier = spec.ModelName == HEAVEN_SOLDIER;
                BuildPrefab(spec, template.gameObject, isSoldier ? soldier : guardian,
                    isSoldier ? soldierAttack : guardianAttack, spec.IsElite ? aura : null);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"EnemySetupTools: 적 프리팹 {ENEMIES.Length}종 생성 ({PREFAB_ROOT}). 씬에 끌어다 놓아 배치하세요.");
        }

        // ------------------------------------------------------------------
        // 데이터
        // ------------------------------------------------------------------

        private static void EnsureData()
        {
            // 천병 정예: 체력 ×3, 공격력 ×1.5, 경험치 60
            if (CopyIfMissing(BASE_STATS, ENEMIES[1].StatsPath, out SerializedObject eliteStats))
            {
                Scale(eliteStats, "_maxHealth", 3f);
                Scale(eliteStats, "_attackPower", 1.5f);
                eliteStats.ApplyModifiedPropertiesWithoutUndo();
            }

            if (CopyIfMissing(BASE_DEFINITION, ENEMIES[1].DefinitionPath, out SerializedObject elite))
            {
                elite.FindProperty("_experienceReward").intValue = 60;
                elite.ApplyModifiedPropertiesWithoutUndo();
            }

            // 역사: 느리고 단단한 광역형 (체력 600, 이동 2.5, 사거리 2.4m·140°, 예비 동작 0.9초, 계수 1.6, 경험치 35)
            if (CopyIfMissing(BASE_STATS, ENEMIES[2].StatsPath, out SerializedObject guardianStats))
            {
                guardianStats.FindProperty("_maxHealth").floatValue = 600f;
                guardianStats.FindProperty("_moveSpeed").floatValue = 2.5f;
                guardianStats.ApplyModifiedPropertiesWithoutUndo();
            }

            if (CopyIfMissing(BASE_DEFINITION, ENEMIES[2].DefinitionPath, out SerializedObject guardian))
            {
                guardian.FindProperty("_attackRange").floatValue = 2.4f;
                guardian.FindProperty("_attackAngle").floatValue = 140f;
                guardian.FindProperty("_attackWindup").floatValue = 0.9f;
                guardian.FindProperty("_attackInterval").floatValue = 2.6f;
                guardian.FindProperty("_attackCoefficient").floatValue = 1.6f;
                guardian.FindProperty("_experienceReward").intValue = 35;
                guardian.ApplyModifiedPropertiesWithoutUndo();
            }

            AssetDatabase.SaveAssets();
        }

        private static bool CopyIfMissing(string source, string destination, out SerializedObject copy)
        {
            copy = null;

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(destination) != null)
            {
                return false;
            }

            if (!AssetDatabase.CopyAsset(source, destination))
            {
                Debug.LogError($"EnemySetupTools: 복사 실패 ({source} → {destination})");
                return false;
            }

            copy = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(destination));
            return true;
        }

        private static void Scale(SerializedObject target, string property, float factor)
        {
            SerializedProperty value = target.FindProperty(property);
            value.floatValue *= factor;
        }

        // 색·두께·맥동은 머티리얼 인스펙터에서 조정 (이미 있으면 값 유지)
        private static Material BuildAuraMaterial()
        {
            Material aura = AssetDatabase.LoadAssetAtPath<Material>(AURA_MATERIAL);

            if (aura != null)
            {
                return aura;
            }

            Shader shader = Shader.Find(AURA_SHADER);

            if (shader == null)
            {
                Debug.LogError($"EnemySetupTools: 셰이더 없음 ({AURA_SHADER})");
                return null;
            }

            aura = new Material(shader);
            AssetDatabase.CreateAsset(aura, AURA_MATERIAL);
            return aura;
        }

        // ------------------------------------------------------------------
        // 프리팹
        // ------------------------------------------------------------------

        private static void BuildPrefab(EnemySpec spec, GameObject template, GameObject modelPrefab, float attackClipLength,
            Material aura)
        {
            GameObject instance = Object.Instantiate(template);
            instance.name = spec.PrefabName;
            instance.transform.position = Vector3.zero;
            instance.transform.rotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one * spec.Scale;

            // 도형 모형(Visual 아래 부품) 비활성화. 렌더러만 끄면 TestDummyRespawner가 부활 때 다시 켜므로 오브젝트째 끔
            Transform visual = instance.transform.Find("Visual");
            Transform parent = visual != null ? visual : instance.transform;

            if (visual != null)
            {
                foreach (Transform part in visual)
                {
                    part.gameObject.SetActive(false);
                }
            }
            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Model";
            model.transform.SetParent(parent, false);

            // NavMeshAgent는 발밑 + baseOffset 높이에 루트를 두므로 모델을 그만큼 내림
            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            float feet = agent != null ? -agent.baseOffset : 0f;
            model.transform.position = instance.transform.position + Vector3.up * feet * spec.Scale;
            model.transform.localRotation = Quaternion.identity;

            // 오라: 본 머티리얼은 그대로 두고 마지막에 한 겹 더 그림
            if (aura != null)
            {
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    renderer.sharedMaterials = renderer.sharedMaterials.Append(aura).ToArray();
                }
            }

            SetReference(instance.GetComponent<EnemyController>(), "_definition",
                AssetDatabase.LoadAssetAtPath<EnemyDefinition>(spec.DefinitionPath));
            SetReference(instance.GetComponent<CharacterStats>(), "_data",
                AssetDatabase.LoadAssetAtPath<CharacterStatsData>(spec.StatsPath));

            EnemyAnimator animator = instance.GetComponent<EnemyAnimator>();

            if (animator == null)
            {
                animator = instance.AddComponent<EnemyAnimator>();
            }

            animator.EditorSetup(model.GetComponent<Animator>(), attackClipLength);

            PrefabUtility.SaveAsPrefabAsset(instance, $"{PREFAB_ROOT}/{spec.PrefabName}.prefab");
            Object.DestroyImmediate(instance);
        }

        private static void SetReference(Object target, string property, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty field = serialized.FindProperty(property);

            if (field == null)
            {
                Debug.LogWarning($"EnemySetupTools: {target.GetType().Name}.{property} 필드 없음");
                return;
            }

            field.objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
