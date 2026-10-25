namespace BrokenSovereign.Hero
{
    /// <summary>성물 메커니즘 흐름 한 칸 (캐릭터 창 [성물] 탭)</summary>
    public readonly struct RelicFlowStep
    {
        public readonly string Title;
        public readonly string Description;

        public RelicFlowStep(string title, string description)
        {
            Title = title;
            Description = description;
        }
    }

    /// <summary>영웅별 성물 정보 (이름·일화·1단계 효과·고유 메커니즘 흐름)</summary>
    public sealed class RelicInfo
    {
        public string HeroId;
        public string Name;
        public string Lore;
        public string Tagline;
        public string Shape;
        public string MechanicSummary;
        public string MechanicDescription;
        public string[] Stage1Effects;
        public RelicFlowStep[] Flow;
    }

    /// <summary>
    /// 프로토타입 성물 목록 (10/21 캐릭터 창 개편, GDD 5.1). 성물은 반신(영웅의 길 선택)에 1단계 각성.
    /// 2~5단계 조건은 미정이라 정의하지 않습니다. 석천 성물 이름·일화·효과는 초안.
    /// </summary>
    public static class RelicCatalog
    {
        public const int MAX_STAGE = 5;

        // 야로미르: 버팀 피해 +15%, 무력화 시간 +0.5초
        public const float YAROMIR_POISE_BONUS = 0.15f;
        public const float YAROMIR_STAGGER_BONUS = 0.5f;

        // 석천: 모드변환 후 3초간 피해 +15%
        public const float SEOKCHEON_DAMAGE_BONUS = 0.15f;
        public const float SEOKCHEON_DURATION = 3f;

        private static readonly RelicInfo[] RELICS =
        {
            new RelicInfo
            {
                HeroId = "yaromir",
                Name = "무쇠 철퇴",
                Shape = "mace",
                Tagline = "순례자에게 받은 힘으로 처음 들어 올린 철퇴",
                Lore = "\"오래 일어서지 못하던 자가 순례자의 물로 일어나 처음 들어 올린 철퇴. 국경을 지킨 세월만큼 무거워졌다.\"",
                MechanicSummary = "한 놈을 부숴서 무너뜨리는 제어형. 정예·보스 상대에 강합니다.",
                MechanicDescription = "버팀 게이지를 깎아 대상을 무력화하고, 무력화된 대상에게 처형 일격을 가합니다.",
                Stage1Effects = new[] { "버팀 피해  +15%", "무력화 시간  +0.5초" },
                Flow = new[]
                {
                    new RelicFlowStep("① 버팀 게이지 파괴", "공격으로 대상의 버팀 게이지를 깎습니다."),
                    new RelicFlowStep("② 무력화", "게이지가 0이 되면 대상이 잠시 무너집니다."),
                    new RelicFlowStep("③ 처형 일격", "무력화된 대상에게 큰 피해를 줍니다.")
                }
            },
            new RelicInfo
            {
                HeroId = "seokcheon",
                Name = "천근신봉",
                Shape = "staff",
                Tagline = "하늘 기둥을 깎아 만든, 길이가 바뀌는 신봉",
                Lore = "\"하늘 기둥을 받치던 쇠를 깎아 만든 신봉. 늘였다 줄였다 하는 길이만큼 싸움의 거리가 바뀐다.\"",
                MechanicSummary = "거리를 바꿔 가며 싸우는 전환형. 무리와 단일 대상을 모두 상대합니다.",
                MechanicDescription = "모드변환(5)으로 신봉 길이를 바꿔 단봉(근거리 피해·관통)과 장봉(일직선 여러 명)을 오갑니다.",
                Stage1Effects = new[] { "모드변환 후 3초간 피해  +15%" },
                Flow = new[]
                {
                    new RelicFlowStep("① 단봉", "가까운 대상에게 피해 증가와 방어 관통."),
                    new RelicFlowStep("② 모드변환", "5키로 신봉 길이를 바꿉니다."),
                    new RelicFlowStep("③ 장봉", "일직선의 여러 적을 한 번에 칩니다.")
                }
            }
        };

        public static RelicInfo Get(string heroId)
        {
            foreach (RelicInfo relic in RELICS)
            {
                if (relic.HeroId == heroId)
                {
                    return relic;
                }
            }

            return null;
        }

        /// <summary>후보 영웅 이름 나열용 전체 목록</summary>
        public static RelicInfo[] All => RELICS;
    }
}
