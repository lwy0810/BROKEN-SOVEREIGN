using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 대상에게 전달되는 최종 피해 정보.
    /// </summary>
    public readonly struct DamageInfo
    {
        public readonly GameObject Attacker;
        public readonly float Amount;

        /// <summary>기본 공격 여부 (신앙 충전 판정용, GDD 6.3)</summary>
        public readonly bool IsBasicAttack;

        public DamageInfo(GameObject attacker, float amount, bool isBasicAttack)
        {
            Attacker = attacker;
            Amount = amount;
            IsBasicAttack = isBasicAttack;
        }
    }
}
