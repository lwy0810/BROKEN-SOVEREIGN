using System;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 경험치·레벨 (순수 클래스, EditMode 테스트 대상).
    /// 레벨 n → n+1 필요 경험치 = 기본값 × 증가율^(n−1). 넘친 경험치는 다음 레벨로 이월되고, 한 번에 여러 레벨이 오를 수 있습니다.
    /// </summary>
    public class ProgressionModel
    {
        private readonly int _baseExperience;
        private readonly float _growth;

        public ProgressionModel(int baseExperience, float growth, int maxLevel)
        {
            _baseExperience = Math.Max(1, baseExperience);
            _growth = Math.Max(1f, growth);
            MaxLevel = Math.Max(1, maxLevel);
        }

        public int Level { get; private set; } = 1;
        public int MaxLevel { get; }

        /// <summary>현재 레벨에서 쌓은 경험치</summary>
        public int Experience { get; private set; }

        public bool IsMaxLevel => Level >= MaxLevel;

        /// <summary>다음 레벨까지 필요한 경험치 (최대 레벨이면 0)</summary>
        public int ExperienceToNext => IsMaxLevel ? 0 : RequiredFor(Level);

        /// <summary>현재 레벨 진행률 0~1 (최대 레벨이면 1)</summary>
        public float Progress => IsMaxLevel ? 1f : (float)Experience / ExperienceToNext;

        /// <summary>레벨 level → level+1 필요 경험치</summary>
        public int RequiredFor(int level)
        {
            return Math.Max(1, (int)Math.Round(_baseExperience * Math.Pow(_growth, Math.Max(0, level - 1))));
        }

        /// <summary>경험치 추가. 오른 레벨 수를 돌려줍니다</summary>
        public int Add(int amount)
        {
            if (amount <= 0 || IsMaxLevel)
            {
                return 0;
            }

            Experience += amount;
            int gained = 0;

            while (!IsMaxLevel && Experience >= ExperienceToNext)
            {
                Experience -= ExperienceToNext;
                Level++;
                gained++;
            }

            if (IsMaxLevel)
            {
                Experience = 0;
            }

            return gained;
        }
    }
}
