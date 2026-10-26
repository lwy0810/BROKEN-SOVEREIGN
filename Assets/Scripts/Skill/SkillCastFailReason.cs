namespace BrokenSovereign.Skill
{
    /// <summary>기술 시전 실패 사유 (HUD 경고 메시지 등)</summary>
    public enum SkillCastFailReason
    {
        /// <summary>마나 등 기술 비용 자원 부족</summary>
        NotEnoughResource,

        /// <summary>포인트를 넣어야 쓸 수 있는 기술을 아직 배우지 않음 (10/26 모드변환)</summary>
        NotLearned
    }
}
