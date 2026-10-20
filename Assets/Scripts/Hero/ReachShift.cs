using BrokenSovereign.Combat;

namespace BrokenSovereign.Hero
{
    /// <summary>석천 신봉 모드</summary>
    public enum StaffMode
    {
        /// <summary>단봉 (근거리): 피해 증가 + 방어 관통</summary>
        Short,

        /// <summary>장봉 (원거리): 사거리 증가 + 일직선 다중 타격</summary>
        Long
    }

    /// <summary>석천 사거리 전환 수치 (GDD 5장 초안)</summary>
    [System.Serializable]
    public struct ReachShiftSettings
    {
        public float LongRangeBonus;
        public float LongLineWidth;
        public int LongMaxTargets;
        public float LongCoefficient;
        public float ShortCoefficient;
        public float ShortArmorPenetration;

        public static ReachShiftSettings Default => new ReachShiftSettings
        {
            LongRangeBonus = 1.5f,
            LongLineWidth = 0.8f,
            LongMaxTargets = 3,
            LongCoefficient = 0.75f,
            ShortCoefficient = 1.3f,
            ShortArmorPenetration = 0.3f
        };
    }

    /// <summary>모드별 기본 공격 수정 (순수 함수, EditMode 테스트 대상)</summary>
    public static class ReachShift
    {
        public static void Apply(StaffMode mode, ReachShiftSettings settings, ref BasicAttackProfile profile)
        {
            if (mode == StaffMode.Long)
            {
                profile.RangeBonus += settings.LongRangeBonus;
                profile.CoefficientMultiplier *= settings.LongCoefficient;
                profile.LineWidth = settings.LongLineWidth;
                profile.MaxTargets = settings.LongMaxTargets;
                return;
            }

            profile.CoefficientMultiplier *= settings.ShortCoefficient;
            profile.ArmorPenetration = 1f - (1f - profile.ArmorPenetration) * (1f - settings.ShortArmorPenetration);
        }

        public static StaffMode Toggle(StaffMode mode)
        {
            return mode == StaffMode.Long ? StaffMode.Short : StaffMode.Long;
        }

        public static string DisplayName(StaffMode mode)
        {
            return mode == StaffMode.Long ? "장봉" : "단봉";
        }
    }
}
