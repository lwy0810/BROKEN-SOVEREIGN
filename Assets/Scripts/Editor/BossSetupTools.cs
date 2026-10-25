using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 이랑진군 보스 생성·배치 (2026-10-25). 메뉴: BrokenSovereign > Enemies > 이랑진군 보스 생성·배치 (현재 씬)
    /// 1. 모델 에셋: Models/ErlangShen (Mixamo 65본, 2.4m) + Animations/ErlangShen (대기·공격 Mixamo, 달리기는 공용 Humanoid)
    /// 2. 데이터: Data/Enemies/ErlangShen(보스 등급)·ErlangShenStats (없을 때만 만들고, 있으면 값 유지 → 밸런스는 에셋에서 조정)
    /// 3. 프리팹: 역사 프리팹을 틀로 모델 교체 + BossStatus + ErlangBoss, 부활 없음 → Prefabs/Enemies/BossErlangShen
    /// 4. 배치: 오픈 필드 보스 제단(BossGate) 패루 안쪽 13m (씬 최상위 "Bosses"). 보스 방 씬 전환 대신 오픈 필드에서 싸움 (시연 일정)
    ///    처음엔 숨겨 두고 보스방에 들어오면 번개와 함께 등장 (BossAppearTrigger, 10/25)
    /// </summary>
    public static class BossSetupTools
    {
        private const string MODEL = "ErlangShen";
        private const string DATA_ROOT = "Assets/Data/Enemies";
        private const string DEFINITION = DATA_ROOT + "/ErlangShen.asset";
        private const string STATS = DATA_ROOT + "/ErlangShenStats.asset";
        private const string PHASES = DATA_ROOT + "/ErlangShenPhases.asset";
        private const string TEMPLATE = "Assets/Prefabs/Enemies/EnemyTempleGuardian.prefab";
        private const string PREFAB = "Assets/Prefabs/Enemies/BossErlangShen.prefab";
        private const string ROOT = "Bosses";

        // 10/25: 보스는 오픈 필드 정사각형 보스방 중앙 (LevelTools.BOSS_ROOM_CENTER, 제단에서 경사길·계단 약 200m)

        [MenuItem("BrokenSovereign/Enemies/이랑진군 보스 생성·배치 (현재 씬)")]
        private static void Build()
        {
            GameObject model = CharacterSetupTools.BuildModelAssets(MODEL, out float attackLength);

            if (model == null)
            {
                return;
            }

            EnsureData();
            GameObject prefab = BuildPrefab(model, attackLength);

            if (prefab == null)
            {
                return;
            }

            Place(prefab);
            AssetDatabase.SaveAssets();
        }

        // 역사 데이터를 복사해 보스 값으로 (이미 있으면 그대로)
        private static void EnsureData()
        {
            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(STATS) == null
                && AssetDatabase.CopyAsset(DATA_ROOT + "/TempleGuardianStats.asset", STATS))
            {
                var stats = new SerializedObject(AssetDatabase.LoadAssetAtPath<ScriptableObject>(STATS));
                stats.FindProperty("_maxHealth").floatValue = 6000f;
                stats.FindProperty("_attackPower").floatValue = 9f;
                stats.FindProperty("_moveSpeed").floatValue = 3.6f;
                stats.ApplyModifiedPropertiesWithoutUndo();
            }

            if (AssetDatabase.LoadAssetAtPath<ScriptableObject>(DEFINITION) == null
                && AssetDatabase.CopyAsset(DATA_ROOT + "/TempleGuardian.asset", DEFINITION))
            {
                EnemyDefinition definition = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DEFINITION);
                definition.EditorSetRank(EnemyRank.Boss);
                var data = new SerializedObject(definition);
                data.FindProperty("_detectRange").floatValue = 14f;
                data.FindProperty("_leashDistance").floatValue = 30f;
                data.FindProperty("_attackRange").floatValue = 3.2f;
                data.FindProperty("_attackAngle").floatValue = 140f;
                data.FindProperty("_attackWindup").floatValue = 0.8f;
                data.FindProperty("_attackRecovery").floatValue = 0.5f;
                data.FindProperty("_attackInterval").floatValue = 2.4f;
                data.FindProperty("_attackCoefficient").floatValue = 1.5f;
                data.FindProperty("_experienceReward").intValue = 600;
                data.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(definition);
            }

            AssetDatabase.SaveAssets();
        }

        private static GameObject BuildPrefab(GameObject modelPrefab, float attackLength)
        {
            GameObject template = AssetDatabase.LoadAssetAtPath<GameObject>(TEMPLATE);

            if (template == null)
            {
                Debug.LogError($"BossSetupTools: 틀 프리팹 없음 ({TEMPLATE}). 적 3종을 먼저 생성하세요.");
                return null;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(template);
            PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            instance.name = "BossErlangShen";
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            // 역사 모델 제거 → 이랑진군 모델
            Transform visual = instance.transform.Find("Visual");
            Transform parent = visual != null ? visual : instance.transform;
            Transform oldModel = parent.Find("Model");

            if (oldModel != null)
            {
                Object.DestroyImmediate(oldModel.gameObject);
            }

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(modelPrefab);
            model.name = "Model";
            model.transform.SetParent(parent, false);

            NavMeshAgent agent = instance.GetComponent<NavMeshAgent>();
            float feet = agent != null ? -agent.baseOffset : 0f;
            model.transform.position = instance.transform.position + Vector3.up * feet;
            model.transform.localRotation = Quaternion.identity;

            if (agent != null)
            {
                agent.radius = 0.9f;
                agent.height = 2.4f;
            }

            // 보스는 부활·머리 위 체력 막대 없음 (상단 보스 HUD가 대신)
            foreach (TestDummyRespawner respawner in instance.GetComponents<TestDummyRespawner>())
            {
                Object.DestroyImmediate(respawner);
            }

            foreach (HealthDebugView bar in instance.GetComponentsInChildren<HealthDebugView>(true))
            {
                Object.DestroyImmediate(bar);
            }

            Set(instance.GetComponent<EnemyController>(), "_definition", AssetDatabase.LoadAssetAtPath<EnemyDefinition>(DEFINITION));
            Set(instance.GetComponent<CharacterStats>(), "_data", AssetDatabase.LoadAssetAtPath<CharacterStatsData>(STATS));

            if (!instance.TryGetComponent(out EnemyAnimator animator))
            {
                animator = instance.AddComponent<EnemyAnimator>();
            }

            animator.EditorSetup(model.GetComponent<Animator>(), attackLength);

            BossStatus boss = instance.GetComponent<BossStatus>();

            if (boss == null)
            {
                boss = instance.AddComponent<BossStatus>();
            }

            boss.Configure("이랑진군", "천계의 무신");

            ErlangBoss erlang = instance.GetComponent<ErlangBoss>();

            if (erlang == null)
            {
                erlang = instance.AddComponent<ErlangBoss>();
            }

            Set(erlang, "_profile", EnsurePhases());

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, PREFAB);
            Object.DestroyImmediate(instance);
            Debug.Log($"BossSetupTools: 보스 프리팹 생성 ({PREFAB}, 공격 클립 {attackLength:0.00}s)");
            return prefab;
        }

        // 오픈 필드 보스 제단 안쪽에 배치 (현재 씬에 Level/Terrain이 있을 때)
        private static void Place(GameObject prefab)
        {
            GameObject level = GameObject.Find("Level");
            Terrain terrain = level != null ? level.GetComponentInChildren<Terrain>() : null;

            if (terrain == null)
            {
                Debug.Log("BossSetupTools: 현재 씬에 오픈 필드 지형이 없어 배치는 건너뜀 (프리팹만 생성)");
                return;
            }

            GameObject old = GameObject.Find(ROOT);

            if (old != null)
            {
                Object.DestroyImmediate(old);
            }

            Vector2 p = LevelTools.BOSS_ROOM_CENTER;
            Vector2 entry = LevelTools.BOSS_ROOM_ENTRY;   // 방 중심 → 입구 (보스가 입구를 바라봄)
            var world = new Vector3(p.x, 0f, p.y);
            world.y = terrain.SampleHeight(world) + terrain.transform.position.y;

            if (NavMesh.SamplePosition(world, out NavMeshHit hit, 4f, NavMesh.AllAreas))
            {
                world = hit.position;
            }

            var root = new GameObject(ROOT).transform;
            var boss = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root);
            boss.name = "Boss_ErlangShen";
            boss.transform.SetPositionAndRotation(world, Quaternion.LookRotation(new Vector3(entry.x, 0f, entry.y)));

            // 등장 연출: 처음엔 꺼 두고 보스방에 들어오면 번개와 함께 나타남 (BossAppearTrigger, 방 중심에 배치)
            var trigger = new GameObject("BossAppearTrigger").AddComponent<BossAppearTrigger>();
            trigger.transform.SetParent(root, false);
            trigger.transform.position = world;
            trigger.Configure(boss, LevelTools.BOSS_ROOM_HALF);

            EditorSceneManager.MarkSceneDirty(boss.scene);
            EditorSceneManager.SaveScene(boss.scene);
            Debug.Log($"BossSetupTools: 이랑진군 배치 ({world}), 보스 제단 앞 웨이포인트에서 시작해 확인");
        }

        // 10/26: 3페이즈 프로필 (없으면 기본값으로 생성, 있으면 인스펙터 수정값 유지)
        private static BossPhaseProfile EnsurePhases()
        {
            BossPhaseProfile profile = AssetDatabase.LoadAssetAtPath<BossPhaseProfile>(PHASES);

            if (profile == null)
            {
                profile = BossPhaseProfile.CreateDefault();
                profile.name = "ErlangShenPhases";
                AssetDatabase.CreateAsset(profile, PHASES);
                AssetDatabase.SaveAssets();
                Debug.Log($"BossSetupTools: 페이즈 프로필 생성 ({PHASES})");
            }

            return profile;
        }

        private static void Set(Object target, string property, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            SerializedProperty field = serialized.FindProperty(property);

            if (field != null)
            {
                field.objectReferenceValue = value;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
    }
}
