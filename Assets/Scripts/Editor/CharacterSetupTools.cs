using System.Linq;
using BrokenSovereign.Player;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 캐릭터 모델 적용 도구 (에디터 전용).
    /// 메뉴: BrokenSovereign > Characters > 성기사 모델 적용
    ///
    /// 처리 순서
    /// 1. FBX 임포트 설정 (Humanoid, 클립 반복·루트 굽기) 재적용
    /// 2. 텍스처 설정 (노멀 맵, 선형 색공간)
    /// 3. URP Lit 머티리얼 M_{이름} 생성·갱신
    /// 4. Animator Controller 생성 (Locomotion 블렌드 트리 + Attack)
    /// 5. 모델 프리팹 생성 (Animator·머티리얼 연결)
    /// 6. 선택한 플레이어 아래에 모델 배치, 기존 도형 렌더러 숨김, PlayerAnimator 연결
    /// </summary>
    public static class CharacterSetupTools
    {
        private const string MODEL_CHILD_NAME = "Model";
        private const string PREFAB_ROOT = "Assets/Prefabs/Characters";
        private const string MATERIAL_ROOT = "Assets/Materials/Characters";

        /// <summary>캐릭터 1종의 에셋 경로 묶음. 새 캐릭터는 이 값만 추가합니다.</summary>
        private readonly struct CharacterSpec
        {
            public readonly string Name;

            public CharacterSpec(string name)
            {
                Name = name;
            }

            public string ModelPath => $"Assets/Models/{Name}/{Name}.fbx";
            public string TextureRoot => $"Assets/Models/{Name}";
            public string AnimationRoot => $"Assets/Animations/{Name}";
            public string IdlePath => $"{AnimationRoot}/{Name}Idle.fbx";
            public string RunPath => $"{AnimationRoot}/{Name}Run.fbx";
            public string AttackPath => $"{AnimationRoot}/{Name}Attack.fbx";
            public string ControllerPath => $"{AnimationRoot}/{Name}Animator.controller";
            public string MaterialPath => $"{MATERIAL_ROOT}/M_{Name}.mat";
            public string PrefabPath => $"{PREFAB_ROOT}/{Name}Model.prefab";
        }

        private static readonly CharacterSpec PALADIN = new CharacterSpec("Paladin");

        [MenuItem("BrokenSovereign/Characters/성기사 모델 적용 (선택한 플레이어)")]
        private static void ApplyPaladin()
        {
            PlayerController player = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PlayerController>()
                : null;

            if (player == null)
            {
                EditorUtility.DisplayDialog("성기사 모델 적용", "Hierarchy에서 플레이어(PlayerController가 있는 오브젝트)를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            GameObject prefab = BuildAssets(PALADIN, out float attackClipLength);

            if (prefab == null)
            {
                return;
            }

            AttachToPlayer(player, prefab, attackClipLength);
        }

        [MenuItem("BrokenSovereign/Characters/성기사 에셋만 갱신 (머티리얼·Animator·프리팹)")]
        private static void RebuildPaladinAssets()
        {
            BuildAssets(PALADIN, out _);
        }

        /// <summary>
        /// 캐릭터 1종의 임포트 설정·머티리얼·Animator·모델 프리팹을 만들고 모델 프리팹을 돌려줍니다 (적 모델 도구에서 사용).
        /// 규칙: Models/{이름}/{이름}.fbx, Animations/{이름}/{이름}Idle·Run·Attack.fbx
        /// </summary>
        public static GameObject BuildModelAssets(string name, out float attackClipLength)
        {
            return BuildAssets(new CharacterSpec(name), out attackClipLength);
        }

        // ------------------------------------------------------------------
        // 에셋 생성
        // ------------------------------------------------------------------

        private static GameObject BuildAssets(CharacterSpec spec, out float attackClipLength)
        {
            attackClipLength = 1f;

            if (!ConfigureImporters(spec))
            {
                return null;
            }

            Material material = BuildMaterial(spec);
            AnimationClip idle = LoadClip(spec.IdlePath);
            AnimationClip run = LoadClip(spec.RunPath);
            AnimationClip attack = LoadClip(spec.AttackPath);

            if (idle == null || run == null || attack == null)
            {
                Debug.LogError($"CharacterSetupTools: {spec.Name} 애니메이션 클립을 찾지 못했습니다 ({spec.AnimationRoot}).");
                return null;
            }

            attackClipLength = attack.length;
            AnimatorController controller = BuildController(spec, idle, run, attack);
            GameObject prefab = BuildPrefab(spec, material, controller);

            AssetDatabase.SaveAssets();
            Debug.Log($"CharacterSetupTools: {spec.Name} 에셋 갱신 완료 (공격 클립 {attackClipLength:0.00}s)");
            return prefab;
        }

        private static bool ConfigureImporters(CharacterSpec spec)
        {
            if (AssetImporter.GetAtPath(spec.ModelPath) is not ModelImporter model)
            {
                Debug.LogError($"CharacterSetupTools: 모델이 없습니다 ({spec.ModelPath}).");
                return false;
            }

            CharacterModelImporter.ApplyModelSettings(model);
            model.SaveAndReimport();

            foreach (string path in new[] { spec.IdlePath, spec.RunPath, spec.AttackPath })
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter anim)
                {
                    Debug.LogError($"CharacterSetupTools: 애니메이션이 없습니다 ({path}).");
                    return false;
                }

                CharacterModelImporter.ApplyAnimationSettings(anim);
                anim.SaveAndReimport();
                CharacterModelImporter.ApplyClipSettings(anim, path);
                anim.SaveAndReimport();
            }

            ModelAssetUtility.ConfigureTextures(spec.TextureRoot, spec.Name);
            return true;
        }

        private static Material BuildMaterial(CharacterSpec spec)
        {
            return ModelAssetUtility.BuildLitMaterial(spec.MaterialPath, spec.TextureRoot, spec.Name);
        }

        private static AnimatorController BuildController(CharacterSpec spec, AnimationClip idle, AnimationClip run, AnimationClip attack)
        {
            // 매번 새로 만들어 상태 구성이 코드와 항상 같게 유지
            if (AssetDatabase.LoadAssetAtPath<AnimatorController>(spec.ControllerPath) != null)
            {
                AssetDatabase.DeleteAsset(spec.ControllerPath);
            }

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(spec.ControllerPath);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Moving", AnimatorControllerParameterType.Bool);
            controller.AddParameter("Attack", AnimatorControllerParameterType.Trigger);
            controller.AddParameter(new AnimatorControllerParameter
            {
                name = "AttackSpeed",
                type = AnimatorControllerParameterType.Float,
                defaultFloat = 1f,
            });

            AnimatorStateMachine machine = controller.layers[0].stateMachine;

            AnimatorState locomotion = controller.CreateBlendTreeInController("Locomotion", out BlendTree tree, 0);
            tree.blendType = BlendTreeType.Simple1D;
            tree.blendParameter = "Speed";
            tree.useAutomaticThresholds = false;
            tree.AddChild(idle, 0f);
            tree.AddChild(run, 1f);
            machine.defaultState = locomotion;

            AnimatorState attackState = machine.AddState("Attack");
            attackState.motion = attack;
            attackState.speedParameter = "AttackSpeed";
            attackState.speedParameterActive = true;

            // 어느 상태에서든 Attack 트리거 → 공격 (연속 공격은 처음부터 다시 재생)
            AnimatorStateTransition toAttack = machine.AddAnyStateTransition(attackState);
            toAttack.AddCondition(AnimatorConditionMode.If, 0f, "Attack");
            toAttack.hasExitTime = false;
            toAttack.duration = 0.05f;
            toAttack.canTransitionToSelf = true;

            // 모션 끝 → 이동·대기
            AnimatorStateTransition finished = attackState.AddTransition(locomotion);
            finished.hasExitTime = true;
            finished.exitTime = 0.9f;
            finished.duration = 0.1f;

            // 공격 중 이동 입력 → 즉시 이동 (공격 후 바로 이동 규칙)
            AnimatorStateTransition cancel = attackState.AddTransition(locomotion);
            cancel.AddCondition(AnimatorConditionMode.If, 0f, "Moving");
            cancel.hasExitTime = false;
            cancel.duration = 0.1f;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        private static GameObject BuildPrefab(CharacterSpec spec, Material material, AnimatorController controller)
        {
            EnsureFolder(PREFAB_ROOT);
            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(spec.ModelPath).OfType<Avatar>().FirstOrDefault();

            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogError($"CharacterSetupTools: Humanoid Avatar가 없습니다 ({spec.ModelPath}). Rig 탭을 확인하세요.");
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            instance.name = $"{spec.Name}Model";

            Animator animator = instance.GetComponent<Animator>();

            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            animator.runtimeAnimatorController = controller;
            animator.avatar = avatar;
            animator.applyRootMotion = false;

            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(instance, spec.PrefabPath);
            Object.DestroyImmediate(instance);
            return prefab;
        }

        // ------------------------------------------------------------------
        // 씬 배치
        // ------------------------------------------------------------------

        private static void AttachToPlayer(PlayerController player, GameObject prefab, float attackClipLength)
        {
            GameObject root = player.gameObject;
            Transform existing = root.transform.Find(MODEL_CHILD_NAME);

            if (existing != null)
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject model = (GameObject)PrefabUtility.InstantiatePrefab(prefab, root.scene);
            Undo.RegisterCreatedObjectUndo(model, "Attach Character Model");
            model.name = MODEL_CHILD_NAME;
            model.transform.SetParent(root.transform, false);

            // NavMeshAgent는 발밑 + baseOffset 높이에 루트를 두므로 모델을 그만큼 내림
            NavMeshAgent agent = root.GetComponent<NavMeshAgent>();
            float feet = agent != null ? -agent.baseOffset : 0f;
            model.transform.localPosition = new Vector3(0f, feet, 0f);
            model.transform.localRotation = Quaternion.identity;

            // 기존 도형 렌더러 숨김 (디버그 표시 컴포넌트는 그대로 동작)
            foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.transform.IsChildOf(model.transform))
                {
                    continue;
                }

                Undo.RecordObject(renderer, "Hide Placeholder Renderer");
                renderer.enabled = false;
            }

            PlayerAnimator playerAnimator = root.GetComponent<PlayerAnimator>();

            if (playerAnimator == null)
            {
                playerAnimator = Undo.AddComponent<PlayerAnimator>(root);
            }

            Undo.RecordObject(playerAnimator, "Setup PlayerAnimator");
            playerAnimator.EditorSetup(model.GetComponent<Animator>(), attackClipLength);
            EditorUtility.SetDirty(playerAnimator);

            Selection.activeGameObject = model;
            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"CharacterSetupTools: {root.name}에 {prefab.name} 배치 (발 높이 {feet:0.00}). 씬을 저장하세요.");
        }

        // ------------------------------------------------------------------
        // 유틸리티
        // ------------------------------------------------------------------

        private static AnimationClip LoadClip(string path)
        {
            return AssetDatabase.LoadAllAssetsAtPath(path)
                .OfType<AnimationClip>()
                .FirstOrDefault(clip => !clip.name.StartsWith("__preview__"));
        }

        private static void EnsureFolder(string path)
        {
            ModelAssetUtility.EnsureFolder(path);
        }
    }
}
