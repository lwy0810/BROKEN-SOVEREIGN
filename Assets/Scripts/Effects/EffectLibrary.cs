using UnityEngine;

namespace BrokenSovereign.Effects
{
    /// <summary>
    /// 공용 이펙트 목록 (10/26): 기본 타격·치명타·적 사망·물약·보스 패턴. Resources/Effects/EffectLibrary.asset에서 읽습니다.
    /// 메뉴 BrokenSovereign/Effects/이펙트 연결 (Cartoon FX)이 Cartoon FX Remaster Free 프리팹으로 채웁니다. 비운 칸은 재생 안 함.
    /// 기술별 시전·타격 이펙트는 SkillDefinition에 있습니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EffectLibrary", menuName = "BrokenSovereign/Effect Library")]
    public class EffectLibrary : ScriptableObject
    {
        public const string RESOURCE_PATH = "Effects/EffectLibrary";

        [Header("전투")]
        public GameObject BasicHit;
        public GameObject CritHit;
        [Min(0.05f)] public float HitScale = 1f;
        public GameObject EnemyDeath;
        public GameObject Potion;
        [Tooltip("회피(대시) 때 플레이어 뒤로 나오는 바람 가르는 이펙트 (10/26)")]
        public GameObject Dodge;
        [Min(0.05f)] public float DodgeScale = 1.1f;

        [Header("보스 (이랑진군)")]
        public GameObject BossSlash;
        public GameObject BossBeam;
        public GameObject BossLeap;
        [Min(0.05f)] public float BossLeapScale = 2.2f;
        public GameObject BossHound;
        public GameObject BossPhase;
        [Min(0.05f)] public float BossPhaseScale = 2.5f;

        private static EffectLibrary _instance;
        private static bool _loaded;

        /// <summary>Resources에서 한 번 읽음 (없으면 null → 공용 이펙트 없음)</summary>
        public static EffectLibrary Instance
        {
            get
            {
                if (!_loaded)
                {
                    _loaded = true;
                    _instance = Resources.Load<EffectLibrary>(RESOURCE_PATH);
                }

                return _instance;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _loaded = false;
        }
    }
}
