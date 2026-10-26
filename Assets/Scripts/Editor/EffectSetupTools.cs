using System.IO;
using BrokenSovereign.Effects;
using BrokenSovereign.Skill;
using UnityEditor;
using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 이펙트 연결 (10/26): Cartoon FX Remaster Free 프리팹을 기술 9종(SkillDefinition)과 공용 이펙트(EffectLibrary)에 연결.
    /// 프리팹은 이름으로 찾음 (Assets/JMO Assets/Cartoon FX Remaster). 없으면 그 칸은 비워 두고 콘솔에 알림.
    /// 다시 실행하면 같은 조합으로 덮어씀 (인스펙터에서 바꾼 값을 지키려면 다시 실행하지 말 것).
    /// </summary>
    public static class EffectSetupTools
    {
        private const string FX_ROOT = "Assets/JMO Assets/Cartoon FX Remaster";
        private const string SKILL_ROOT = "Assets/Data/Skills";
        private const string LIBRARY_PATH = "Assets/Resources/" + EffectLibrary.RESOURCE_PATH + ".asset";

        private const string SWORD_HIT = "CFXR4 Sword Hit PLAIN (Cross)";
        private const string HIT_YELLOW = "CFXR Hit D 3D (Yellow)";
        private const string HIT_RED = "CFXR Hit A (Red)";
        private const string HIT_LIGHT = "CFXR3 Hit Light B (Air)";
        private const string GROUND_HIT = "CFXR2 Ground Hit";
        private const string SMOKE = "CFXR Explosion Smoke 2 Solo (HDR)";
        private const string AURA = "CFXR3 Magic Aura A (Runic)";
        private const string WIND = "CFXR4 Wind Trails";
        private const string GLOW = "CFXR3 LightGlow A (Loop)";
        private const string SPIRAL = "CFXR4 Sword Trail PLAIN (360 Spiral)";
        private const string POOF = "CFXR Magic Poof";
        private const string FLASH = "CFXR Flash";
        private const string SOULS = "CFXR2 Souls Escape";

        private static int _missing;

        [MenuItem("BrokenSovereign/Effects/이펙트 연결 (Cartoon FX)")]
        private static void Setup()
        {
            if (!AssetDatabase.IsValidFolder(FX_ROOT))
            {
                EditorUtility.DisplayDialog("이펙트 연결", "Cartoon FX Remaster Free가 없습니다.\nAsset Store에서 Import한 뒤 다시 실행하세요.", "확인");
                return;
            }

            _missing = 0;
            SetupSkills();
            SetupLibrary();
            AssetDatabase.SaveAssets();
            Debug.Log($"EffectSetupTools: 이펙트 연결 완료 (기술 9종 + 공용 {LIBRARY_PATH}){(_missing > 0 ? $", 찾지 못한 프리팹 {_missing}개" : string.Empty)}");
        }

        private static void SetupSkills()
        {
            //                 기술 ID              시전           보조          위치                       크기   유지                              타격         보조 타격    크기
            SetSkill("strike",            null,      null,  SkillEffectAnchor.Player, 1f,   SkillEffectLifetime.Natural,       SWORD_HIT,  null,       1.2f);
            SetSkill("radiant_smite",     FLASH,     null,  SkillEffectAnchor.Player, 1f,   SkillEffectLifetime.Natural,       HIT_LIGHT,  null,       1.4f);
            SetSkill("crushing_blow",     null,      null,  SkillEffectAnchor.Player, 1f,   SkillEffectLifetime.Natural,       HIT_YELLOW, GROUND_HIT, 1.3f);
            SetSkill("sovereign_quake",   GROUND_HIT, SMOKE, SkillEffectAnchor.Player, 2.2f, SkillEffectLifetime.Natural,      HIT_YELLOW, null,       1f);
            SetSkill("battle_cry",        AURA,      null,  SkillEffectAnchor.Player, 1.6f, SkillEffectLifetime.Natural,       null,       null,       1f);
            SetSkill("advancing_oath",    WIND,      null,  SkillEffectAnchor.Follow, 1f,   SkillEffectLifetime.Natural,       SWORD_HIT,  null,       1.2f);
            SetSkill("unyielding_aegis",  GLOW,      null,  SkillEffectAnchor.Follow, 1.4f, SkillEffectLifetime.SkillDuration, null,       null,       1f);
            SetSkill("heavenly_whirl",    SPIRAL,    null,  SkillEffectAnchor.Follow, 1.5f, SkillEffectLifetime.Channel,       HIT_YELLOW, null,       0.9f);
            SetSkill("reach_shift",       POOF,      null,  SkillEffectAnchor.Player, 1f,   SkillEffectLifetime.Natural,       null,       null,       1f);
        }

        private static void SetSkill(string id, string cast, string castExtra, SkillEffectAnchor anchor, float castScale,
            SkillEffectLifetime lifetime, string hit, string hitExtra, float hitScale)
        {
            SkillDefinition skill = FindSkill(id);

            if (skill == null)
            {
                Debug.LogWarning($"EffectSetupTools: 기술 '{id}' 데이터를 찾지 못했습니다 ({SKILL_ROOT}).");
                return;
            }

            Undo.RecordObject(skill, "이펙트 연결");
            skill.EditorSetEffects(Prefab(cast), Prefab(castExtra), anchor, castScale, lifetime, Prefab(hit), Prefab(hitExtra), hitScale);
            EditorUtility.SetDirty(skill);
        }

        private static void SetupLibrary()
        {
            EffectLibrary library = AssetDatabase.LoadAssetAtPath<EffectLibrary>(LIBRARY_PATH);

            if (library == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LIBRARY_PATH));
                AssetDatabase.Refresh();
                library = ScriptableObject.CreateInstance<EffectLibrary>();
                AssetDatabase.CreateAsset(library, LIBRARY_PATH);
            }

            Undo.RecordObject(library, "이펙트 연결");
            library.BasicHit = Prefab(HIT_YELLOW);
            library.CritHit = Prefab(HIT_RED);
            library.EnemyDeath = Prefab(SOULS);
            library.Potion = null;           // 10/26: 물약 빛 이펙트 제거
            library.Dodge = Prefab(WIND);     // 10/26: 회피 바람 이펙트
            library.BossSlash = Prefab(SWORD_HIT);
            library.BossBeam = Prefab(HIT_LIGHT);
            library.BossLeap = Prefab(GROUND_HIT);
            library.BossHound = Prefab(POOF);
            library.BossPhase = Prefab(AURA);
            EditorUtility.SetDirty(library);
        }

        private static SkillDefinition FindSkill(string id)
        {
            foreach (string guid in AssetDatabase.FindAssets("t:SkillDefinition", new[] { SKILL_ROOT }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillDefinition>(AssetDatabase.GUIDToAssetPath(guid));

                if (skill != null && skill.SkillId == id)
                {
                    return skill;
                }
            }

            return null;
        }

        // 파일 이름이 정확히 같은 프리팹
        private static GameObject Prefab(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }

            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { FX_ROOT }))
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);

                if (Path.GetFileNameWithoutExtension(path) == name)
                {
                    return AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }

            _missing++;
            Debug.LogWarning($"EffectSetupTools: 프리팹 '{name}'을(를) 찾지 못했습니다 ({FX_ROOT}).");
            return null;
        }
    }
}
