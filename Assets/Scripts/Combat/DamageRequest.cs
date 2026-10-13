namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 피해 계산 입력값. 공격자 능력치(StatSheet)에서 채웁니다.
    /// </summary>
    public readonly struct DamageRequest
    {
        public readonly float AttackPower;
        public readonly float Coefficient;

        /// <summary>피해 증가 합 (전체 + 속성별, 0.2 = +20%)</summary>
        public readonly float DamageBonus;

        /// <summary>대상 감소율 (0~1). 방어 측 감소는 보통 Health에서 따로 적용하므로 0</summary>
        public readonly float TargetReduction;

        /// <summary>주 스탯 피해 배율 (1 = 보너스 없음)</summary>
        public readonly float MainStatMultiplier;

        /// <summary>치명타 배율 (1 = 치명타 아님, 1.5 = 치명타 피해 +50%)</summary>
        public readonly float CritMultiplier;

        public DamageRequest(float attackPower, float coefficient, float damageBonus = 0f, float targetReduction = 0f,
            float mainStatMultiplier = 1f, float critMultiplier = 1f)
        {
            AttackPower = attackPower;
            Coefficient = coefficient;
            DamageBonus = damageBonus;
            TargetReduction = targetReduction;
            MainStatMultiplier = mainStatMultiplier;
            CritMultiplier = critMultiplier;
        }
    }
}
