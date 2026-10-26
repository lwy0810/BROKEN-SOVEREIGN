using System.Collections.Generic;
using BrokenSovereign.Core;

namespace BrokenSovereign.Achievement
{
    /// <summary>업적 분류 (업적 창 왼쪽 목록 순서)</summary>
    public enum AchievementCategory
    {
        Trial,
        Combat,
        Explore,
        Growth,
        Special
    }

    /// <summary>업적 진행을 세는 기준</summary>
    public enum AchievementCounter
    {
        Kills,
        EliteKills,
        BossKills,
        /// <summary>도달한 보스 페이즈 번호 (1부터, 최댓값)</summary>
        BossPhase,
        /// <summary>플레이 중 새로 발견한 웨이포인트 수 (처음부터 열린 곳 제외)</summary>
        WaypointsDiscovered,
        /// <summary>웨이포인트 전부 발견 (목표 = 처음부터 열린 곳을 뺀 씬의 웨이포인트 수)</summary>
        AllWaypoints,
        Level,
        /// <summary>격 단계 (초인 1, 반신 2)</summary>
        Tier,
        HeroPath,
        Dodges,
        Potions,
        Deaths,
        /// <summary>기술 사용 횟수 (10/26)</summary>
        SkillCasts,
        /// <summary>플레이어 치명타 횟수</summary>
        Crits,
        /// <summary>한 번에 준 가장 큰 피해 (최댓값)</summary>
        BiggestHit,
        /// <summary>얻은 금화 합계</summary>
        GoldEarned,
        /// <summary>영웅 등급 이상 아이템 획득 수</summary>
        EpicItems,
        LegendaryItems,
        /// <summary>신격 노드 활성 수 (시작 노드 제외)</summary>
        GyeokNodes,
        /// <summary>체력 절반 이상으로 보스 처치</summary>
        BossKillsHealthy
    }

    /// <summary>업적 하나 (코드 카탈로그)</summary>
    public sealed class AchievementDefinition
    {
        public readonly string Id;
        public readonly AchievementCategory Category;
        public readonly string Name;
        public readonly string Description;
        public readonly AchievementCounter Counter;
        public readonly int Goal;
        public readonly int Points;
        public readonly StatModifier[] Rewards;
        public readonly string RewardText;
        public readonly string Title;

        public AchievementDefinition(string id, AchievementCategory category, string name, string description,
            AchievementCounter counter, int goal, int points, string rewardText = null, StatModifier[] rewards = null,
            string title = null)
        {
            Id = id;
            Category = category;
            Name = name;
            Description = description;
            Counter = counter;
            Goal = goal;
            Points = points;
            RewardText = rewardText;
            Rewards = rewards ?? new StatModifier[0];
            Title = title;
        }

        /// <summary>보상 안내 (업적 창 오른쪽): +포인트 · 보상</summary>
        public string RewardLabel
        {
            get
            {
                if (!string.IsNullOrEmpty(Title))
                {
                    return Points > 0 ? $"+{Points} · 칭호" : "칭호";
                }

                return string.IsNullOrEmpty(RewardText) ? $"+{Points}" : $"+{Points} · {RewardText}";
            }
        }
    }

    /// <summary>
    /// 업적 목록 (10/26 프로토타입). 일반 업적 +1 · 스탯 보너스, 시련 +3, 특수 업적은 칭호.
    /// 업적 포인트는 신격 트리(GyeokTree) 노드 활성화에 사용합니다.
    /// </summary>
    public static class AchievementCatalog
    {
        public const int TRIAL_POINTS = 3;
        public const int NORMAL_POINTS = 1;

        public static string CategoryName(AchievementCategory category)
        {
            switch (category)
            {
                case AchievementCategory.Trial: return "시련";
                case AchievementCategory.Combat: return "전투";
                case AchievementCategory.Explore: return "탐험";
                case AchievementCategory.Growth: return "성장";
                default: return "특수";
            }
        }

        private static StatModifier[] Flat(StatType stat, float value)
        {
            return new[] { new StatModifier(stat, ModifierType.Flat, value) };
        }

        // 10/26 재구성: 시작 상태(마을 웨이포인트·시작 레벨 등)로는 달성되지 않고, 플레이 중 행동으로만 진행
        private static readonly List<AchievementDefinition> _all = new List<AchievementDefinition>
        {
            // 시련 (+3)
            new AchievementDefinition("trial_superhuman", AchievementCategory.Trial, "초인 시련", "범인의 한계를 넘어 초인으로 돌파",
                AchievementCounter.Tier, 1, TRIAL_POINTS, "격 돌파"),
            new AchievementDefinition("trial_demigod", AchievementCategory.Trial, "반신 시련", "초인을 넘어 반신으로 돌파",
                AchievementCounter.Tier, 2, TRIAL_POINTS, "격 돌파"),
            new AchievementDefinition("trial_erlang", AchievementCategory.Trial, "이랑진군 토벌", "천계의 수문장 이랑진군 처치",
                AchievementCounter.BossKills, 1, TRIAL_POINTS, "보스 처치"),

            // 전투
            new AchievementDefinition("kill_30", AchievementCategory.Combat, "첫 사냥", "적 30마리 처치",
                AchievementCounter.Kills, 30, NORMAL_POINTS, "힘 +2", Flat(StatType.Strength, 2f)),
            new AchievementDefinition("kill_100", AchievementCategory.Combat, "사냥꾼", "적 100마리 처치",
                AchievementCounter.Kills, 100, NORMAL_POINTS, "최대 체력 +20", Flat(StatType.MaxHealth, 20f)),
            new AchievementDefinition("kill_300", AchievementCategory.Combat, "학살자", "적 300마리 처치",
                AchievementCounter.Kills, 300, NORMAL_POINTS, "공격력 +3", Flat(StatType.AttackPower, 3f)),
            new AchievementDefinition("elite_5", AchievementCategory.Combat, "정예 사냥꾼 I", "정예 몬스터 5마리 처치",
                AchievementCounter.EliteKills, 5, NORMAL_POINTS, "힘 +2", Flat(StatType.Strength, 2f)),
            new AchievementDefinition("elite_20", AchievementCategory.Combat, "정예 사냥꾼 II", "정예 몬스터 20마리 처치",
                AchievementCounter.EliteKills, 20, NORMAL_POINTS, "치명타 확률 +2%", Flat(StatType.CritChance, 0.02f)),
            new AchievementDefinition("crit_50", AchievementCategory.Combat, "급소를 꿰뚫다", "치명타 50회",
                AchievementCounter.Crits, 50, NORMAL_POINTS, "치명타 피해 +10%", Flat(StatType.CritDamage, 0.1f)),
            new AchievementDefinition("big_hit", AchievementCategory.Combat, "일격", "한 번에 피해 300 이상",
                AchievementCounter.BiggestHit, 300, NORMAL_POINTS, "공격력 +2", Flat(StatType.AttackPower, 2f)),
            new AchievementDefinition("skill_100", AchievementCategory.Combat, "기술 숙련", "기술 100회 사용",
                AchievementCounter.SkillCasts, 100, NORMAL_POINTS, "재사용 대기 -3%", Flat(StatType.CooldownReduction, 0.03f)),
            new AchievementDefinition("dodge_50", AchievementCategory.Combat, "흐르는 발걸음", "회피 50회",
                AchievementCounter.Dodges, 50, NORMAL_POINTS, "민첩 +2", Flat(StatType.Agility, 2f)),
            new AchievementDefinition("boss_phase", AchievementCategory.Combat, "무신의 그림자", "이랑진군을 3페이즈까지 몰아넣기",
                AchievementCounter.BossPhase, 3, NORMAL_POINTS, "기술 피해 +2%", Flat(StatType.DamageBonus, 0.02f)),

            // 탐험 (마을처럼 처음부터 열린 웨이포인트는 세지 않음)
            new AchievementDefinition("wp_1", AchievementCategory.Explore, "길을 여는 자", "새 웨이포인트 1곳 발견",
                AchievementCounter.WaypointsDiscovered, 1, NORMAL_POINTS, "최대 체력 +10", Flat(StatType.MaxHealth, 10f)),
            new AchievementDefinition("wp_all", AchievementCategory.Explore, "천계 순례", "필드의 웨이포인트 모두 발견",
                AchievementCounter.AllWaypoints, 0, NORMAL_POINTS, "이동 속도 +3%", Flat(StatType.MoveSpeed, 0.03f)),
            new AchievementDefinition("gold_1000", AchievementCategory.Explore, "부의 축적", "금화 1,000 획득",
                AchievementCounter.GoldEarned, 1000, NORMAL_POINTS, "지능 +2", Flat(StatType.Intelligence, 2f)),
            new AchievementDefinition("epic_3", AchievementCategory.Explore, "보물 사냥꾼", "영웅 등급 이상 아이템 3개 획득",
                AchievementCounter.EpicItems, 3, NORMAL_POINTS, "방어력 +5", Flat(StatType.Armor, 5f)),
            new AchievementDefinition("legend_1", AchievementCategory.Explore, "전설과의 조우", "전설 등급 아이템 획득",
                AchievementCounter.LegendaryItems, 1, NORMAL_POINTS, "모든 피해 +2%", Flat(StatType.DamageBonus, 0.02f)),

            // 성장
            new AchievementDefinition("lv_5", AchievementCategory.Growth, "견습 영웅", "레벨 5 달성",
                AchievementCounter.Level, 5, NORMAL_POINTS, "지능 +2", Flat(StatType.Intelligence, 2f)),
            new AchievementDefinition("lv_15", AchievementCategory.Growth, "격을 넘보는 자", "레벨 15 달성",
                AchievementCounter.Level, 15, NORMAL_POINTS, "최대 체력 +20", Flat(StatType.MaxHealth, 20f)),
            new AchievementDefinition("hero_path", AchievementCategory.Growth, "첫 번째 성물", "영웅의 길 선택 · 성물 1단계 각성",
                AchievementCounter.HeroPath, 1, NORMAL_POINTS, "지능 +2", Flat(StatType.Intelligence, 2f)),
            new AchievementDefinition("gyeok_5", AchievementCategory.Growth, "신격의 싹", "신격 노드 5개 활성화",
                AchievementCounter.GyeokNodes, 5, NORMAL_POINTS, "최대 체력 +15", Flat(StatType.MaxHealth, 15f)),
            new AchievementDefinition("potion_20", AchievementCategory.Growth, "약초꾼", "물약 20회 사용",
                AchievementCounter.Potions, 20, NORMAL_POINTS, "생명력 재생 +1", Flat(StatType.LifeRegen, 1f)),

            // 특수 (칭호)
            new AchievementDefinition("title_trial", AchievementCategory.Special, "시련을 넘어선 자", "범인 시련 달성 (초인 돌파)",
                AchievementCounter.Tier, 1, NORMAL_POINTS, title: "시련을 넘어선 자"),
            new AchievementDefinition("title_rise", AchievementCategory.Special, "다시 일어선 자", "쓰러진 뒤 부활",
                AchievementCounter.Deaths, 1, NORMAL_POINTS, title: "다시 일어선 자"),
            new AchievementDefinition("title_slayer", AchievementCategory.Special, "신을 벤 자", "이랑진군 처치",
                AchievementCounter.BossKills, 1, NORMAL_POINTS, title: "신을 벤 자"),
            new AchievementDefinition("title_unshaken", AchievementCategory.Special, "흔들림 없는 자", "체력 절반 이상 남기고 이랑진군 처치",
                AchievementCounter.BossKillsHealthy, 1, NORMAL_POINTS, title: "흔들림 없는 자"),
        };

        public static IReadOnlyList<AchievementDefinition> All => _all;
    }
}
