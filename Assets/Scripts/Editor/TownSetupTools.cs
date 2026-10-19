using System.Linq;
using BrokenSovereign.Map;
using BrokenSovereign.Player;
using BrokenSovereign.Town;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 마을 상점·NPC 도구 (에디터 전용). 메뉴: BrokenSovereign > Town
    ///
    /// 상점 3종 (잡화상·대장간·치유소) = 건물 + NPC 한 쌍.
    /// - 건물: Prefabs/Props/PropShop{이름}.prefab (Props 메뉴로 VARCO 모델에서 생성). 없으면 돌·기와 임시 건물
    /// - NPC: Prefabs/Town/Npc{이름}.prefab. Models/Npc{이름}/Npc{이름}.fbx(Humanoid)가 있으면 모델 + 대기 애니메이션, 없으면 캡슐 임시 인형
    ///   대기 애니메이션: Animations/Npc{이름}/Npc{이름}Idle.fbx, 없으면 성기사 대기 동작을 Humanoid로 공유
    /// - 명판: 건물 앞면 위쪽 가운데에 상점 종류 아이콘 판 (Sprites/Town/T_ShopSign{종류}.png)
    /// 건물 앞면 = +Z. LevelTools가 마을 생성 때 광장을 바라보도록 배치합니다.
    /// </summary>
    public static class TownSetupTools
    {
        public const string PREFAB_ROOT = "Assets/Prefabs/Town";
        private const string PROP_PREFABS = "Assets/Prefabs/Props";
        private const string MATERIAL_ROOT = "Assets/Materials/Characters";
        private const string CONTROLLER_ROOT = "Assets/Animations/Npc";
        private const string SHARED_IDLE = "Assets/Animations/Paladin/PaladinIdle.fbx";
        private const string STONE_MATERIAL = "Assets/Materials/Field/M_FieldStone.mat";
        private const string ROOF_MATERIAL = "Assets/Materials/Field/M_TownRoof.mat";
        private const string INPUT_ACTIONS = "Assets/Settings/InputSystem_Actions.inputactions";
        private const string SIGN_TEXTURE_ROOT = "Assets/Sprites/Town";
        private const string SIGN_MATERIAL_ROOT = "Assets/Materials/Town";

        // 명판: 가로 1.8m, 세로는 이미지 비율(없으면 4:3). 앞면이 쿼터뷰 카메라 쪽을 보도록 위로 젖힘
        private const float SIGN_WIDTH = 1.8f;
        private const float SIGN_THICKNESS = 0.08f;
        private const float SIGN_TILT = 15f;
        private const float SIGN_GAP = 0.06f;

        private const float NPC_HEIGHT = 1.8f;
        private const float NPC_RADIUS = 0.35f;

        public enum ShopKind
        {
            Goods,  // 잡화상
            Forge,  // 대장간
            Clinic  // 치유소
        }

        /// <summary>상점 1종의 이름·대사·임시 건물 크기. 새 상점은 이 값만 추가합니다.</summary>
        public sealed class ShopSpec
        {
            public ShopKind Kind;
            public string NpcName;
            public string ShopName;
            public string DisplayName;
            public string Title;
            public string[] Lines;
            public Vector3 Size;        // 임시 건물 가로·높이·깊이 (m)
            public float SignHeight = 0.4f; // 명판 아래 끝 높이 = 건물 높이 × 비율 (모델에 있는 빈 현판 위치에 맞춤)
            public bool IsHealer;

            public string NpcPrefabPath => $"{PREFAB_ROOT}/{NpcName}.prefab";
            public string ShopPrefabPath => $"{PROP_PREFABS}/{ShopName}.prefab";
            public string PlaceholderPath => $"{PREFAB_ROOT}/{ShopName}Placeholder.prefab";
            public string ModelPath => $"Assets/Models/{NpcName}/{NpcName}.fbx";
            public string IdlePath => $"Assets/Animations/{NpcName}/{NpcName}Idle.fbx";
            public string SignTexturePath => $"{SIGN_TEXTURE_ROOT}/T_ShopSign{Kind}.png";
            public string SignMaterialPath => $"{SIGN_MATERIAL_ROOT}/M_ShopSign{Kind}.mat";
            public string SignPrefabPath => $"{PREFAB_ROOT}/ShopSign{Kind}.prefab";
        }

        public static readonly ShopSpec[] SHOPS =
        {
            new ShopSpec
            {
                Kind = ShopKind.Goods, NpcName = "NpcMerchant", ShopName = "PropShopGoods",
                DisplayName = "곽상", Title = "잡화상",
                Lines = new[]
                {
                    "구름 너머에서 들여온 물건들일세. 아직 짐을 푸는 중이라 거래는 조금만 기다려 주게.",
                    "격을 되찾는 길이라… 살아 돌아오면 좋은 물건을 보여 주지.",
                },
                Size = new Vector3(5f, 3.6f, 4f), SignHeight = 0.42f,
            },
            new ShopSpec
            {
                Kind = ShopKind.Forge, NpcName = "NpcBlacksmith", ShopName = "PropShopForge",
                DisplayName = "철무", Title = "대장장이",
                Lines = new[]
                {
                    "천병들의 창끝도 내 망치 앞에선 무뎌지지. 화로가 달궈지면 그때 다시 오게.",
                    "무기 손질은 준비 중이네. 대신 하나 일러 두지 — 금빛 기운을 두른 놈은 피하는 게 상책이야.",
                },
                Size = new Vector3(6f, 3.8f, 4f), SignHeight = 0.4f,
            },
            new ShopSpec
            {
                Kind = ShopKind.Clinic, NpcName = "NpcHealer", ShopName = "PropShopClinic",
                DisplayName = "백련", Title = "치유사",
                Lines = new[]
                {
                    "다친 데는 없는가? 언제든 들르게.",
                    "물약은 아끼지 말게. 여기 오면 다시 채워 주겠네.",
                },
                Size = new Vector3(5f, 3.6f, 4f), SignHeight = 0.36f, IsHealer = true,
            },
        };

        // ------------------------------------------------------------------
        // 메뉴
        // ------------------------------------------------------------------

        [MenuItem("BrokenSovereign/Town/상점·NPC 프리팹 다시 만들기")]
        private static void RebuildAll()
        {
            foreach (ShopSpec spec in SHOPS)
            {
                BuildNpcPrefab(spec);
                ShopPrefab(spec);
                BuildSignPrefab(spec);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"TownSetupTools: NPC·상점 프리팹 갱신 ({PREFAB_ROOT}). 마을에 반영하려면 Levels > 오픈 필드 생성을 다시 실행하세요.");
        }

        [MenuItem("BrokenSovereign/Town/NPC 상호작용 연결 (선택한 플레이어)")]
        private static void AttachToSelectedPlayer()
        {
            PlayerController player = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PlayerController>()
                : null;

            if (player == null)
            {
                EditorUtility.DisplayDialog("NPC 상호작용 연결", "Hierarchy에서 플레이어(PlayerController가 있는 오브젝트)를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            GameObject root = player.gameObject;
            NpcInteractor interactor = root.GetComponent<NpcInteractor>();

            if (interactor == null)
            {
                interactor = Undo.AddComponent<NpcInteractor>(root);
            }

            var so = new SerializedObject(interactor);
            // 입력 에셋 (Editor asmdef는 Input System을 참조하지 않으므로 Object로 불러옴)
            so.FindProperty("_inputActions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<Object>(INPUT_ACTIONS);
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"TownSetupTools: {root.name}에 NpcInteractor 연결 (F 키). 씬을 저장하세요.");
        }

        [MenuItem("BrokenSovereign/Town/시험용 상점 3종 배치 (현재 씬, 선택 위치)")]
        private static void PlaceTestShops()
        {
            Vector3 origin = Selection.activeTransform != null ? Selection.activeTransform.position : Vector3.zero;
            var root = new GameObject("TestShops");
            Undo.RegisterCreatedObjectUndo(root, "시험용 상점 배치");
            root.transform.position = origin;

            for (int i = 0; i < SHOPS.Length; i++)
            {
                // 선택 위치 뒤쪽(+Z)에 가로로 나란히, 앞면은 -Z(선택 위치)를 향함
                float x = (i - 1) * 9f;
                Vector3 shopPos = origin + new Vector3(x, 0f, 8f);
                PlaceShop(SHOPS[i], root.transform, shopPos, Quaternion.Euler(0f, 180f, 0f));
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log("TownSetupTools: 시험용 상점 3종 배치. 길찾기에 반영하려면 NavMeshSurface를 다시 구우세요.");
        }

        // ------------------------------------------------------------------
        // 배치 (LevelTools 사용)
        // ------------------------------------------------------------------

        /// <summary>
        /// 건물과 NPC를 함께 배치합니다. NPC는 건물 앞면 바로 앞에 서서 건물과 같은 방향(앞)을 바라봅니다.
        /// snap: 위치를 지면 높이에 맞추는 함수 (없으면 그대로)
        /// </summary>
        public static void PlaceShop(ShopSpec spec, Transform parent, Vector3 shopPosition, Quaternion facing,
            System.Func<Vector3, Vector3> snap = null)
        {
            var group = new GameObject($"Shop_{spec.Kind}");
            group.transform.SetParent(parent, false);
            group.transform.position = shopPosition;

            // 회전 전 앞면(+Z) 거리를 재고 회전 → NPC는 앞면에서 1.2m 앞
            GameObject shop = Instantiate(ShopPrefab(spec), group.transform, shopPosition, Quaternion.identity, spec.ShopName);
            float front = spec.Size.z * 0.5f;
            float height = spec.Size.y;
            float centerX = 0f;

            if (shop != null)
            {
                Renderer[] renderers = shop.GetComponentsInChildren<Renderer>();

                if (renderers.Length > 0)
                {
                    Bounds bounds = renderers[0].bounds;

                    foreach (Renderer renderer in renderers)
                    {
                        bounds.Encapsulate(renderer.bounds);
                    }

                    front = bounds.max.z - shopPosition.z;
                    height = bounds.max.y - shopPosition.y;
                    centerX = bounds.center.x - shopPosition.x;
                }

                shop.transform.rotation = facing;
            }

            // 명판: 앞면(처마 끝 포함) 바로 앞, 가운데 위쪽
            var signLocal = new Vector3(centerX, height * spec.SignHeight, front + SIGN_GAP);
            Instantiate(BuildSignPrefab(spec), group.transform, shopPosition + facing * signLocal,
                facing * Quaternion.Euler(-SIGN_TILT, 0f, 0f), $"Sign_{spec.Kind}");

            GameObject npcPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.NpcPrefabPath);

            if (npcPrefab == null)
            {
                npcPrefab = BuildNpcPrefab(spec);
            }

            Vector3 npcPosition = shopPosition + facing * Vector3.forward * (front + 1.2f);
            Instantiate(npcPrefab, group.transform, snap != null ? snap(npcPosition) : npcPosition, facing, spec.NpcName);
        }

        private static GameObject Instantiate(GameObject prefab, Transform parent, Vector3 position, Quaternion rotation, string name)
        {
            if (prefab == null)
            {
                return null;
            }

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = name;
            instance.transform.SetParent(parent, true);
            instance.transform.SetPositionAndRotation(position, rotation);
            return instance;
        }

        // ------------------------------------------------------------------
        // 건물
        // ------------------------------------------------------------------

        /// <summary>VARCO 건물 프리팹, 없으면 임시 건물</summary>
        public static GameObject ShopPrefab(ShopSpec spec)
        {
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ShopPrefabPath);
            return prefab != null ? prefab : BuildPlaceholderShop(spec);
        }

        // 임시 건물: 돌 기단 + 뒷벽 + 계산대 + 기둥 4 + 기와 지붕 (앞면 +Z)
        private static GameObject BuildPlaceholderShop(ShopSpec spec)
        {
            Material stone = AssetDatabase.LoadAssetAtPath<Material>(STONE_MATERIAL);
            Material roof = AssetDatabase.LoadAssetAtPath<Material>(ROOF_MATERIAL);
            Vector3 s = spec.Size;
            var root = new GameObject(spec.ShopName);

            Box(root, "Base", new Vector3(0f, 0.2f, 0f), new Vector3(s.x, 0.4f, s.z), stone);
            Box(root, "BackWall", new Vector3(0f, s.y * 0.5f, -s.z * 0.5f + 0.2f), new Vector3(s.x, s.y, 0.4f), stone);
            Box(root, "Counter", new Vector3(0f, 0.9f, s.z * 0.5f - 0.6f), new Vector3(s.x * 0.8f, 1f, 0.6f), stone);

            for (int i = 0; i < 4; i++)
            {
                float x = (i % 2 == 0 ? -1f : 1f) * (s.x * 0.5f - 0.25f);
                float z = (i < 2 ? -1f : 1f) * (s.z * 0.5f - 0.25f);
                Box(root, $"Post_{i}", new Vector3(x, s.y * 0.5f, z), new Vector3(0.3f, s.y, 0.3f), stone);
            }

            Box(root, "Roof", new Vector3(0f, s.y + 0.25f, 0f), new Vector3(s.x + 1.2f, 0.5f, s.z + 1.2f), roof);

            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.PlaceholderPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static void Box(GameObject root, string name, Vector3 center, Vector3 size, Material material)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = name;
            box.transform.SetParent(root.transform, false);
            box.transform.localPosition = center;
            box.transform.localScale = size;
            box.GetComponent<MeshRenderer>().sharedMaterial = material;
            GameObjectUtility.SetStaticEditorFlags(box, StaticEditorFlags.BatchingStatic);
        }

        // ------------------------------------------------------------------
        // 명판 (상점 종류 아이콘). 텍스처: Sprites/Town/T_ShopSign{종류}.png (VARCO 이미지). 없으면 단색
        // ------------------------------------------------------------------

        public static GameObject BuildSignPrefab(ShopSpec spec)
        {
            Material material = BuildSignMaterial(spec);
            Texture texture = material.GetTexture("_BaseMap");
            float aspect = texture != null ? (float)texture.width / texture.height : 4f / 3f;
            var size = new Vector2(SIGN_WIDTH, SIGN_WIDTH / Mathf.Clamp(aspect, 1f, 2.5f));
            var root = new GameObject($"ShopSign{spec.Kind}");

            // 판 두께: 돌 머티리얼 상자. 원점 = 판 아래 가운데 뒷면 (건물 앞면에 붙임)
            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "Board";
            Object.DestroyImmediate(board.GetComponent<Collider>());
            board.transform.SetParent(root.transform, false);
            board.transform.localPosition = new Vector3(0f, size.y * 0.5f, SIGN_THICKNESS * 0.5f);
            board.transform.localScale = new Vector3(size.x + 0.06f, size.y + 0.06f, SIGN_THICKNESS);
            board.GetComponent<MeshRenderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(STONE_MATERIAL);

            // 그림 면: Quad는 -Z 쪽에서 보이므로 180° 돌려 +Z(바깥)를 향하게
            GameObject face = GameObject.CreatePrimitive(PrimitiveType.Quad);
            face.name = "Face";
            Object.DestroyImmediate(face.GetComponent<Collider>());
            face.transform.SetParent(root.transform, false);
            face.transform.localPosition = new Vector3(0f, size.y * 0.5f, SIGN_THICKNESS + 0.003f);
            face.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            face.transform.localScale = new Vector3(size.x, size.y, 1f);
            MeshRenderer faceRenderer = face.GetComponent<MeshRenderer>();
            faceRenderer.sharedMaterial = material;
            faceRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.SignPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static Material BuildSignMaterial(ShopSpec spec)
        {
            ModelAssetUtility.EnsureFolder(SIGN_MATERIAL_ROOT);
            Material material = AssetDatabase.LoadAssetAtPath<Material>(spec.SignMaterialPath);

            if (material == null)
            {
                material = new Material(Shader.Find(ModelAssetUtility.URP_LIT));
                AssetDatabase.CreateAsset(material, spec.SignMaterialPath);
            }

            if (AssetImporter.GetAtPath(spec.SignTexturePath) is TextureImporter importer)
            {
                importer.textureType = TextureImporterType.Default;
                importer.sRGBTexture = true;
                importer.maxTextureSize = 1024;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.npotScale = TextureImporterNPOTScale.None; // 비율 유지 (명판 세로 길이 계산)
                importer.alphaIsTransparency = true;
                importer.SaveAndReimport();
            }

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(spec.SignTexturePath);
            material.SetTexture("_BaseMap", texture);
            material.SetColor("_BaseColor", texture != null ? Color.white : new Color(0.45f, 0.12f, 0.1f));
            material.SetFloat("_Smoothness", 0.35f);

            // 그늘진 처마 밑에서도 읽히도록 약하게 발광
            material.SetTexture("_EmissionMap", texture);
            material.SetColor("_EmissionColor", texture != null ? new Color(0.35f, 0.35f, 0.35f) : Color.black);
            ModelAssetUtility.SetKeyword(material, "_EMISSION", texture != null);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;

            EditorUtility.SetDirty(material);
            return material;
        }

        // ------------------------------------------------------------------
        // NPC
        // ------------------------------------------------------------------

        public static GameObject BuildNpcPrefab(ShopSpec spec)
        {
            var root = new GameObject(spec.NpcName);

            TownNpc npc = root.AddComponent<TownNpc>();
            var so = new SerializedObject(npc);
            so.FindProperty("_displayName").stringValue = spec.DisplayName;
            so.FindProperty("_title").stringValue = spec.Title;
            SerializedProperty lines = so.FindProperty("_lines");
            lines.arraySize = spec.Lines.Length;

            for (int i = 0; i < spec.Lines.Length; i++)
            {
                lines.GetArrayElementAtIndex(i).stringValue = spec.Lines[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();

            if (spec.IsHealer)
            {
                root.AddComponent<HealerService>();
            }

            // 미니맵·전체 지도 상점 아이콘
            root.AddComponent<MapMarker>().SetKind(spec.Kind == ShopKind.Goods ? MapMarkerKind.Merchant
                : spec.Kind == ShopKind.Forge ? MapMarkerKind.Smith : MapMarkerKind.Healer);

            // 플레이어가 NPC를 통과하지 않도록 (길찾기는 장애물로 피해 감)
            CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, NPC_HEIGHT * 0.5f, 0f);
            collider.radius = NPC_RADIUS;
            collider.height = NPC_HEIGHT;

            NavMeshObstacle obstacle = root.AddComponent<NavMeshObstacle>();
            obstacle.shape = NavMeshObstacleShape.Capsule;
            obstacle.center = collider.center;
            obstacle.radius = NPC_RADIUS + 0.1f;
            obstacle.height = NPC_HEIGHT;
            obstacle.carving = true;

            if (!AttachModel(spec, root))
            {
                AttachPlaceholder(root);
            }

            // 커서 규칙(NPC → 대화)용 레이어 (HUD 아트 연결 메뉴가 만든 NPC 레이어)
            int npcLayer = LayerMask.NameToLayer(UiArtTools.NPC_LAYER);

            if (npcLayer >= 0)
            {
                foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
                {
                    child.gameObject.layer = npcLayer;
                }
            }

            ModelAssetUtility.EnsureFolder(PREFAB_ROOT);
            GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, spec.NpcPrefabPath);
            Object.DestroyImmediate(root);
            return prefab;
        }

        private static bool AttachModel(ShopSpec spec, GameObject root)
        {
            if (AssetImporter.GetAtPath(spec.ModelPath) is not ModelImporter importer)
            {
                return false;
            }

            CharacterModelImporter.ApplyModelSettings(importer);
            importer.SaveAndReimport();

            string textureRoot = System.IO.Path.GetDirectoryName(spec.ModelPath)?.Replace('\\', '/');
            ModelAssetUtility.ConfigureTextures(textureRoot, spec.NpcName);
            Material material = ModelAssetUtility.BuildLitMaterial($"{MATERIAL_ROOT}/M_{spec.NpcName}.mat", textureRoot, spec.NpcName);

            GameObject modelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(spec.ModelPath);
            Avatar avatar = AssetDatabase.LoadAllAssetsAtPath(spec.ModelPath).OfType<Avatar>().FirstOrDefault();

            if (avatar == null || !avatar.isHuman)
            {
                Debug.LogWarning($"TownSetupTools: {spec.NpcName} Humanoid Avatar 없음. 대기 애니메이션 없이 배치합니다.");
            }

            var model = (GameObject)PrefabUtility.InstantiatePrefab(modelAsset);
            model.name = "Model";
            model.transform.SetParent(root.transform, false);

            Animator animator = model.GetComponent<Animator>();

            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.avatar = avatar;
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = BuildIdleController(spec);
            animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            }

            return true;
        }

        private static void AttachPlaceholder(GameObject root)
        {
            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Placeholder";
            Object.DestroyImmediate(body.GetComponent<Collider>());
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, NPC_HEIGHT * 0.5f, 0f);
            body.transform.localScale = new Vector3(0.6f, NPC_HEIGHT * 0.5f, 0.6f);

            // 앞 방향 표시 (코)
            GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
            nose.name = "Front";
            Object.DestroyImmediate(nose.GetComponent<Collider>());
            nose.transform.SetParent(root.transform, false);
            nose.transform.localPosition = new Vector3(0f, 1.5f, 0.3f);
            nose.transform.localScale = new Vector3(0.15f, 0.15f, 0.2f);
        }

        // 대기 상태 하나만 있는 Animator (NPC별 대기 동작이 없으면 성기사 대기 동작 공유)
        private static RuntimeAnimatorController BuildIdleController(ShopSpec spec)
        {
            string clipPath = AssetImporter.GetAtPath(spec.IdlePath) != null ? spec.IdlePath : SHARED_IDLE;
            AnimationClip idle = AssetDatabase.LoadAllAssetsAtPath(clipPath).OfType<AnimationClip>()
                .FirstOrDefault(c => !c.name.StartsWith("__preview__"));

            if (idle == null)
            {
                Debug.LogWarning($"TownSetupTools: 대기 동작 없음 ({clipPath})");
                return null;
            }

            ModelAssetUtility.EnsureFolder(CONTROLLER_ROOT);
            string path = $"{CONTROLLER_ROOT}/{spec.NpcName}Animator.controller";
            AssetDatabase.DeleteAsset(path);
            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            AnimatorState state = controller.layers[0].stateMachine.AddState("Idle");
            state.motion = idle;
            // NPC끼리 동작이 겹쳐 보이지 않도록 속도만 약간 다르게
            state.speed = 0.9f + 0.1f * (int)spec.Kind;
            controller.layers[0].stateMachine.defaultState = state;
            EditorUtility.SetDirty(controller);
            return controller;
        }
    }
}
