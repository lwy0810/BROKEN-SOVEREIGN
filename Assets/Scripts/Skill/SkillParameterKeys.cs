namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 수치 키 이름. 에셋·코드·분기 수정치가 같은 이름을 쓰도록 여기서만 정의합니다.
    /// </summary>
    public static class SkillParameterKeys
    {
        /// <summary>피해 계수 (1.5 = 공격력의 150%)</summary>
        public const string COEFFICIENT = "coefficient";

        /// <summary>범위 반경 (m)</summary>
        public const string RADIUS = "radius";

        /// <summary>부채꼴 각도 (도)</summary>
        public const string ANGLE = "angle";
    }
}
