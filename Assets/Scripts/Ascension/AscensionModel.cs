namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 격 돌파 조건 판정 (순수 클래스, EditMode 테스트 대상). 프로토타입은 레벨·직업 위주로 단순화 (GDD 11장).
    /// 범인 → 초인: 직업 선택 + 초인 레벨 / 초인 → 반신: 반신 레벨 (영웅의 길 선택은 반신 돌파 후 진행)
    /// </summary>
    public static class AscensionModel
    {
        /// <summary>돌파 가능한 다음 단계. 조건 미달·최고 단계면 null</summary>
        public static AscensionTier? GetNext(AscensionTier current, int level, bool hasJob, int superhumanLevel,
            int demigodLevel)
        {
            switch (current)
            {
                case AscensionTier.Mortal:
                    return hasJob && level >= superhumanLevel ? AscensionTier.Superhuman : (AscensionTier?)null;
                case AscensionTier.Superhuman:
                    return level >= demigodLevel ? AscensionTier.Demigod : (AscensionTier?)null;
                default:
                    return null;
            }
        }

        public static string DisplayName(AscensionTier tier)
        {
            switch (tier)
            {
                case AscensionTier.Superhuman:
                    return "초인";
                case AscensionTier.Demigod:
                    return "반신";
                default:
                    return "범인";
            }
        }
    }
}
