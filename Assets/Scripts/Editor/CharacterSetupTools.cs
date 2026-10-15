using System.IO;
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
        private const string URP_LIT = "Universal Render Pipeline/Lit";

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
            public string Texture(string role) => $"{TextureRoot}/T_{Name}_{role}.png";
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

            ConfigureTexture(spec.Texture("BaseColor"), TextureImporterType.Default, true);
            ConfigureTexture(spec.Texture("Normal"), TextureImporterType.NormalMap, false);
            ConfigureTexture(spec.Texture("MetallicSmoothness"), TextureImporterType.Default, false);
            return true;
        }

        private static void ConfigureTexture(string path, TextureImporterType type, bool sRgb)
        {
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
            {
                Debug.LogWarning($"CharacterSetupTools: 텍스처 없음 ({path})");
                return;
            }

            importer.textureType = type;
            importer.sRGBTexture = sRgb;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static Material BuildMaterial(CharacterSpec spec)
        {
            EnsureFolder(MATERIAL_ROOT);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.MaterialPath);

            if (material == null)
            {
                material = new Material(Shader.Find(URP_LIT));
                AssetDatabase.CreateAsset(material, spec.MaterialPath);
            }

            material.shader = Shader.Find(URP_LIT);
            material.SetColor("_BaseColor", Color.white);
            material.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(spec.Texture("BaseColor")));

            Texture2D normal = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.Texture("Normal"));
            material.SetTexture("_BumpMap", normal);
            SetKeyword(material, "_NORMALMAP", normal != null);

            // URP Lit: 금속성 텍스처 R = 금속성, A = 매끄러움 (_Smoothness는 배율)
            Texture2D metallic = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.Texture("MetallicSmoothness"));
            material.SetTexture("_MetallicGlossMap", metallic);
            material.SetFloat("_SmoothnessTextureChannel", 0f);
            material.SetFloat("_Smoothness", 1f);
            material.SetFloat("_Metallic", 0f);
            SetKeyword(material, "_METALLICSPECGLOSSMAP", metallic != null);

            EditorUtility.SetDirty(material);
            return material;
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

        private static void SetKeyword(Material material, string keyword, bool enabled)
        {
            if (enabled)
            {
                material.EnableKeyword(keyword);
            }
            else
            {
                material.DisableKeyword(keyword);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = Path.GetDirectoryName(path)?.Replace('\\', '/');
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
        }
    }
}
