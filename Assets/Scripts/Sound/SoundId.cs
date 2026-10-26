namespace BrokenSovereign.Sound
{
    /// <summary>
    /// 소리 이름 = Resources/Audio 안의 파일 이름 (확장자 제외). 변형은 이름_2, 이름_3 … (무작위 재생).
    /// VARCO Sound 저장 폴더를 Assets/Resources/Audio로 두고, 생성한 파일을 아래 이름으로 바꾸면 바로 재생됩니다.
    /// 파일이 없으면 조용히 넘어갑니다 (메뉴 BrokenSovereign/Audio/사운드 목록 확인).
    /// </summary>
    public static class SoundId
    {
        // 음악 (반복)
        public const string BGM_TITLE = "bgm_title";
        public const string BGM_FIELD = "bgm_field";
        public const string BGM_BOSS = "bgm_boss";

        // UI·타이틀
        public const string UI_HOVER = "ui_hover";
        public const string UI_CLICK = "ui_click";
        public const string UI_START = "ui_start";
        public const string STORY_LINE = "story_line";

        // 플레이어
        public const string ATTACK_SWING = "atk_swing";
        public const string HIT_ENEMY = "hit_enemy";
        public const string HIT_CRIT = "hit_crit";
        public const string PLAYER_HURT = "player_hurt";
        public const string PLAYER_DEATH = "player_death";
        public const string DODGE = "dodge";
        public const string POTION = "potion";
        public const string SKILL_CAST = "skill_cast";   // 기술별 파일(skill_기술ID)이 없을 때

        // 적·보스
        public const string ENEMY_DEATH = "enemy_death";
        public const string BOSS_SLASH = "boss_slash";
        public const string BOSS_BEAM = "boss_beam";
        public const string BOSS_LEAP = "boss_leap";
        public const string BOSS_HOUND = "boss_hound";
        public const string BOSS_ROAR = "boss_roar";
        public const string THUNDER = "thunder";
        public const string VICTORY = "victory";

        // 월드
        public const string WAYPOINT = "waypoint";
        public const string PORTAL = "portal";

        /// <summary>기술별 소리 이름 (예: skill_radiant_smite)</summary>
        public static string Skill(string skillId) => "skill_" + skillId;

        public static readonly string[] ALL =
        {
            BGM_TITLE, BGM_FIELD, BGM_BOSS,
            UI_HOVER, UI_CLICK, UI_START, STORY_LINE,
            ATTACK_SWING, HIT_ENEMY, HIT_CRIT, PLAYER_HURT, PLAYER_DEATH, DODGE, POTION, SKILL_CAST,
            ENEMY_DEATH, BOSS_SLASH, BOSS_BEAM, BOSS_LEAP, BOSS_HOUND, BOSS_ROAR, THUNDER, VICTORY,
            WAYPOINT, PORTAL,
        };
    }
}
