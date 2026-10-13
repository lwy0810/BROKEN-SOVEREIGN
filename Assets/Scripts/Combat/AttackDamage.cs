using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 공격자 능력치로 DamageInfo를 만드는 공용 함수 (플레이어 기본 공격·적 공격 공용).
    /// 공격력·피해 증가·주 스탯·치명타를 반영하고, 방어 측 감소는 대상 Health가 적용합니다.
    /// </summary>
    public static class AttackDamage
    {
        /// <param name="stats">공격자 능력치. null이면 fallbackAttackPower로 계산 (치명타 없음)</param>
        public static DamageInfo Build(GameObject attacker, StatSheet stats, float coefficient,
            float fallbackAttackPower, bool isBasicAttack, DamageType type = DamageType.Physical)
        {
            if (stats == null)
            {
                float plain = DamageCalculator.Calculate(new DamageRequest(fallbackAttackPower, coefficient));
                return new DamageInfo(attacker, plain, isBasicAttack, type);
            }

            bool isCritical = StatFormula.RollCrit(stats.Get(StatType.CritChance), Random.value);
            float critMultiplier = isCritical ? 1f + Mathf.Max(0f, stats.Get(StatType.CritDamage)) : 1f;

            float amount = DamageCalculator.Calculate(new DamageRequest(
                stats.Get(StatType.AttackPower),
                coefficient,
                stats.GetDamageBonus(type),
                0f,
                stats.MainStatDamageMultiplier,
                critMultiplier));

            return new DamageInfo(attacker, amount, isBasicAttack, type, isCritical);
        }
    }
}
