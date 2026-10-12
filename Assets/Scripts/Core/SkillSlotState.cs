namespace BrokenSovereign.Core
{
    /// <summary>
    /// HUD 기술 슬롯 표시용 상태 (개방 여부, 재사용 대기).
    /// </summary>
    public readonly struct SkillSlotState
    {
        public readonly bool IsUnlocked;
        public readonly float CooldownRemaining;
        public readonly float CooldownDuration;

        public SkillSlotState(bool isUnlocked, float cooldownRemaining = 0f, float cooldownDuration = 0f)
        {
            IsUnlocked = isUnlocked;
            CooldownRemaining = cooldownRemaining;
            CooldownDuration = cooldownDuration;
        }

        public static SkillSlotState Locked => new SkillSlotState(false);

        /// <summary>남은 재사용 대기 비율 (1 = 방금 사용, 0 = 사용 가능)</summary>
        public float CooldownRatio => CooldownDuration > 0f ? CooldownRemaining / CooldownDuration : 0f;
    }
}
