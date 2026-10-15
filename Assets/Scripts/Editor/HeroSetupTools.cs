using BrokenSovereign.Ascension;
using BrokenSovereign.Hero;
using BrokenSovereign.Player;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 영웅의 길 설정 도구 (에디터 전용).
    /// 메뉴: BrokenSovereign > Heroes
    ///
    /// 영웅의 길 에셋 생성: Data/Heroes/Hero{이름} 생성·갱신, 성투사 직업의 영웅 후보로 연결
    /// 영웅의 길 연결 (선택한 플레이어): 플레이어에 HeroPathController 추가
    /// 영웅 기술·고유 규칙은 구현 후 에셋 인스펙터에서 연결합니다.
    /// </summary>
    public static class HeroSetupTools
    {
        private const string DATA_ROOT = "Assets/Data/Heroes";
        private const string HOLY_WARRIOR_PATH = "Assets/Data/Ascension/JobHolyWarrior.asset";

        private readonly struct HeroSpec
        {
            public readonly string AssetName;
            public readonly string Id;
            public readonly string DisplayName;
            public readonly string Title;
            public readonly string Mechanic;
            public readonly string Description;

            public HeroSpec(string assetName, string id, string displayName, string title, string mechanic, string description)
            {
                AssetName = assetName;
                Id = id;
                DisplayName = displayName;
                Title = title;
                Mechanic = mechanic;
                Description = description;
            }

            public string Path => $"{DATA_ROOT}/{AssetName}.asset";
        }

        // 순서 = 선택 화면 카드 순서 (GDD 5장 두 영웅 비교 기준)
        private static readonly HeroSpec[] HEROES =
        {
            new HeroSpec("HeroYaromir", "yaromir", "야로미르", "철퇴의 순례자", "분쇄",
                "모든 피해가 적의 버팀 게이지를 깎는다.\n게이지가 무너진 적은 무력화되고,\n분쇄 일격으로 처형한다.\n\n한 대상을 부숴 무너뜨리는 제어형"),
            new HeroSpec("HeroSeokcheon", "seokcheon", "석천", "투전성자", "사거리 전환",
                "적과의 거리에 따라 신봉 길이가 바뀐다.\n가까우면 빠른 연타, 멀면 넓은 휘두르기.\n난천무로 회전하며 적을 쓸어 담는다.\n\n휘저으며 쓸어 담는 위치 운영형"),
        };

        [MenuItem("BrokenSovereign/Heroes/영웅의 길 에셋 생성")]
        private static void BuildAll()
        {
            ModelAssetUtility.EnsureFolder(DATA_ROOT);
            var paths = new HeroPathDefinition[HEROES.Length];

            for (int i = 0; i < HEROES.Length; i++)
            {
                HeroSpec spec = HEROES[i];
                HeroPathDefinition path = AssetDatabase.LoadAssetAtPath<HeroPathDefinition>(spec.Path);

                if (path == null)
                {
                    path = ScriptableObject.CreateInstance<HeroPathDefinition>();
                    AssetDatabase.CreateAsset(path, spec.Path);
                }

                path.EditorSetup(spec.Id, spec.DisplayName, spec.Title, spec.Mechanic, spec.Description);
                EditorUtility.SetDirty(path);
                paths[i] = path;
            }

            JobDefinition holyWarrior = AssetDatabase.LoadAssetAtPath<JobDefinition>(HOLY_WARRIOR_PATH);

            if (holyWarrior != null)
            {
                holyWarrior.EditorSetHeroPaths(paths);
                EditorUtility.SetDirty(holyWarrior);
            }
            else
            {
                Debug.LogWarning($"HeroSetupTools: 성투사 직업 없음 ({HOLY_WARRIOR_PATH})");
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"HeroSetupTools: 영웅의 길 {paths.Length}개 생성, 성투사 후보로 연결 ({DATA_ROOT})");
        }

        [MenuItem("BrokenSovereign/Heroes/영웅의 길 연결 (선택한 플레이어)")]
        private static void AttachToSelectedPlayer()
        {
            PlayerController player = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<PlayerController>()
                : null;

            if (player == null)
            {
                EditorUtility.DisplayDialog("영웅의 길 연결", "Hierarchy에서 플레이어(PlayerController가 있는 오브젝트)를 선택한 뒤 실행하세요.", "확인");
                return;
            }

            GameObject root = player.gameObject;

            if (root.GetComponent<HeroPathController>() == null)
            {
                Undo.AddComponent<HeroPathController>(root);
            }

            EditorSceneManager.MarkSceneDirty(root.scene);
            Debug.Log($"HeroSetupTools: {root.name}에 HeroPathController 연결. 씬을 저장하세요.");
        }
    }
}
