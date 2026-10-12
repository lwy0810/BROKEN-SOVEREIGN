namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 피해 계산 입력값. 스탯·장비·신물 보너스는 이 값들을 채우는 쪽에서 합산합니다.
    /// </summary>
    public readonly struct DamageRequest
    {
        public readonly float AttackPower;
        public readonly float Coefficient;

        /// <summary>피해 증가율 (0.2 = +20%)</summary>
        public readonly float DamageBonus;

        /// <summary>대상 피해 감소율 (0.3 = 30% 감소, 0~1)</summary>
        public readonly float TargetReduction;

        public DamageRequest(float attackPower, float coefficient, float damageBonus = 0f, float targetReduction = 0f)
        {
            AttackPower = attackPower;
            Coefficient = coefficient;
            DamageBonus = damageBonus;
            TargetReduction = targetReduction;
        }
    }
}
