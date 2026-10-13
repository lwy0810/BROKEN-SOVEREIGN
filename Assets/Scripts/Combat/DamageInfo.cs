using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 대상에게 전달되는 피해 정보. Amount는 방어 측 감소 전 값이며,
    /// Health가 회피·감소를 적용한 뒤 최종 값으로 Damaged 이벤트를 발행합니다.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly GameObject Attacker;
        public readonly float Amount;

        /// <summary>기본 공격 여부 (신앙 충전 판정용, GDD 6.3)</summary>
        public readonly bool IsBasicAttack;

        public readonly DamageType Type;
        public readonly bool IsCritical;

        public DamageInfo(GameObject attacker, float amount, bool isBasicAttack,
            DamageType type = DamageType.Physical, bool isCritical = false)
        {
            Attacker = attacker;
            Amount = amount;
            IsBasicAttack = isBasicAttack;
            Type = type;
            IsCritical = isCritical;
        }

        public DamageInfo WithAmount(float amount)
        {
            return new DamageInfo(Attacker, amount, IsBasicAttack, Type, IsCritical);
        }
    }
}
