namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 시전 사거리 계산 (순수 클래스, EditMode 테스트 대상).
    /// 효과 범위(radius·distance 등)와 시전 사거리를 분리합니다.
    /// 1) castRange가 있으면 그 값 2) 없으면 radius × 0.8 (대상 중심이 범위 안쪽에 확실히 들어오도록) 3) 둘 다 없으면 0
    /// 0이면 호출하는 쪽(PlayerController)이 기본 공격 사거리를 사용합니다.
    /// </summary>
    public static class SkillRange
    {
        public const float RADIUS_REACH_RATIO = 0.8f;

        public static float Resolve(SkillParameterSet parameters)
        {
            if (parameters == null)
            {
                return 0f;
            }

            if (parameters.Has(SkillParameterKeys.CAST_RANGE))
            {
                return System.Math.Max(0f, parameters.Get(SkillParameterKeys.CAST_RANGE));
            }

            float radius = parameters.Get(SkillParameterKeys.RADIUS);
            return radius > 0f ? radius * RADIUS_REACH_RATIO : 0f;
        }
    }
}
