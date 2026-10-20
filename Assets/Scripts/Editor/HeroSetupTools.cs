using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using BrokenSovereign.Hero;
using BrokenSovereign.Player;
using BrokenSovereign.Skill;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 영웅의 길 설정 도구 (에디터 전용).
    /// 메뉴: BrokenSovereign > Heroes
    ///
    /// 영웅의 길 에셋 생성: Data/Heroes/Hero{이름} 생성·갱신, 성투사 직업의 영웅 후보로 연결,
    ///   영웅 기술(Data/Skills)·기술 동작(Data/Skills/Behaviours)·고유 규칙(Data/Heroes/Rule*) 생성·연결 (10/20),
    ///   정예 천병 등급을 정예로 설정 (버팀 게이지 100)
    /// 영웅의 길 연결 (선택한 플레이어): 플레이어에 HeroPathController 추가
    /// 수치는 다시 실행하면 아래 기본값으로 덮어씁니다 (조정은 이 파일 또는 실행 후 인스펙터).
    /// </summary>
    public static class HeroSetupTools
    {
        private const string DATA_ROOT = "Assets/Data/Heroes";
        private const string HOLY_WARRIOR_PATH = "Assets/Data/Ascension/JobHolyWarrior.asset";
        private const string SKILL_ROOT = "Assets/Data/Skills";
        private const string BEHAVIOUR_ROOT = "Assets/Data/Skills/Behaviours";
        private const string ELITE_DEFINITION_PATH = "Assets/Data/Enemies/HeavenSoldierElite.asset";

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
                "모드변환으로 신봉 길이를 바꾼다.\n장봉은 멀리 일직선으로, 단봉은 강하게 꿰뚫는다.\n난천무로 회전하며 적을 쓸어 담는다.\n\n휘저으며 쓸어 담는 위치 운영형"),
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

            LinkHeroSkills(paths);

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

        // 영웅 기술·규칙 생성 후 각 영웅의 길에 연결 (GDD 5장 수치 초안)
        private static void LinkHeroSkills(HeroPathDefinition[] paths)
        {
            ModelAssetUtility.EnsureFolder(SKILL_ROOT);
            ModelAssetUtility.EnsureFolder(BEHAVIOUR_ROOT);

            var spin = LoadOrCreate<SpinningStaffBehaviour>($"{BEHAVIOUR_ROOT}/SpinningStaff.asset");
            var crush = LoadOrCreate<CrushStrikeBehaviour>($"{BEHAVIOUR_ROOT}/CrushStrike.asset");
            var toggle = LoadOrCreate<ModeToggleBehaviour>($"{BEHAVIOUR_ROOT}/ModeToggle.asset");

            // 석천 4번 난천무: 키 유지 회전 이동, 0.5초마다 반경 2.5m 60%. 시작 10 + 초당 15. 충만: 4초 무소모 + 종료 내려찍기
            var whirl = LoadOrCreate<SkillDefinition>($"{SKILL_ROOT}/SkillHeavenlyWhirl.asset");
            whirl.EditorSetup("heavenly_whirl", "난천무", 0f, 10f, SkillCastMovement.MoveWhileCasting, 0f, true,
                new[]
                {
                    P(SkillParameterKeys.COEFFICIENT, 0.6f),
                    P(SkillParameterKeys.RADIUS, 2.5f),
                    P(SkillParameterKeys.TICK_INTERVAL, 0.5f),
                    P(SkillParameterKeys.MOVE_SPEED_MULTIPLIER, 0.8f),
                    P(SkillParameterKeys.COST_PER_SECOND, 15f),
                    P(SkillParameterKeys.FREE_DURATION, 0f),
                    P(SkillParameterKeys.FINISH_COEFFICIENT, 0f),
                    P(SkillParameterKeys.FINISH_RADIUS, 3f)
                },
                new[]
                {
                    M(SkillParameterKeys.FREE_DURATION, ModifierType.Flat, 4f),
                    M(SkillParameterKeys.FINISH_COEFFICIENT, ModifierType.Flat, 1.5f)
                },
                spin);

            // 석천 5번 모드변환: 장봉 ↔ 단봉, 재사용 대기 1초, 비용 없음
            var shift = LoadOrCreate<SkillDefinition>($"{SKILL_ROOT}/SkillReachShift.asset");
            shift.EditorSetup("reach_shift", "모드변환", 1f, 0f, SkillCastMovement.InPlace, 0f, false,
                new SkillDefinition.SkillParameter[0], new SkillDefinition.SkillModifierEntry[0], toggle);

            // 야로미르 4번 분쇄 일격: 전방 단일 300%, 무력화 대상 처형 600% + 2.5m 충격파 150%. 충만: 버팀 50% 감소
            var crushSkill = LoadOrCreate<SkillDefinition>($"{SKILL_ROOT}/SkillCrushingBlow.asset");
            crushSkill.EditorSetup("crushing_blow", "분쇄 일격", 10f, 30f, SkillCastMovement.StopInRange, 0.5f, false,
                new[]
                {
                    P(SkillParameterKeys.COEFFICIENT, 3f),
                    P(SkillParameterKeys.RADIUS, 2.5f),
                    P(SkillParameterKeys.ANGLE, 120f),
                    P(SkillParameterKeys.CAST_RANGE, 2.2f),
                    P(SkillParameterKeys.EXECUTE_COEFFICIENT, 6f),
                    P(SkillParameterKeys.SHOCKWAVE_RADIUS, 2.5f),
                    P(SkillParameterKeys.SHOCKWAVE_COEFFICIENT, 1.5f),
                    P(SkillParameterKeys.POISE_BREAK_RATIO, 0f)
                },
                new[] { M(SkillParameterKeys.POISE_BREAK_RATIO, ModifierType.Flat, 0.5f) },
                crush);

            var reachRule = LoadOrCreate<ReachShiftRuleDefinition>($"{DATA_ROOT}/RuleReachShift.asset");
            var crushRule = LoadOrCreate<CrushRuleDefinition>($"{DATA_ROOT}/RuleCrush.asset");

            foreach (HeroPathDefinition path in paths)
            {
                if (path.HeroId == "seokcheon")
                {
                    path.EditorSetSkills(whirl, SkillSlot.Key4,
                        new[] { new HeroPathDefinition.ExtraSkill(SkillSlot.Key5, shift) }, reachRule);
                }
                else if (path.HeroId == "yaromir")
                {
                    path.EditorSetSkills(crushSkill, SkillSlot.Key4, new HeroPathDefinition.ExtraSkill[0], crushRule);
                }

                EditorUtility.SetDirty(path);
            }

            foreach (Object asset in new Object[] { spin, crush, toggle, whirl, shift, crushSkill, reachRule, crushRule })
            {
                EditorUtility.SetDirty(asset);
            }

            // 정예 천병: 버팀 게이지 정예 등급
            EnemyDefinition elite = AssetDatabase.LoadAssetAtPath<EnemyDefinition>(ELITE_DEFINITION_PATH);

            if (elite != null)
            {
                elite.EditorSetRank(EnemyRank.Elite);
                EditorUtility.SetDirty(elite);
            }
        }

        private static T LoadOrCreate<T>(string path) where T : ScriptableObject
        {
            T asset = AssetDatabase.LoadAssetAtPath<T>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<T>();
                AssetDatabase.CreateAsset(asset, path);
            }

            return asset;
        }

        private static SkillDefinition.SkillParameter P(string key, float value)
        {
            return new SkillDefinition.SkillParameter(key, value);
        }

        private static SkillDefinition.SkillModifierEntry M(string key, ModifierType type, float value)
        {
            return new SkillDefinition.SkillModifierEntry { Key = key, Type = type, Value = value };
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
