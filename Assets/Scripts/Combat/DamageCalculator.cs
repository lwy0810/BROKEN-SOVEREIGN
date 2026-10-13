using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 피해량 계산 (순수 클래스, EditMode 테스트 대상).
    /// 피해량 = 공격력 × 계수 × (1 + 피해 증가) × 주 스탯 배율 × 치명타 배율 × (1 − 대상 감소)
    /// 같은 묶음(피해 증가)은 더하고, 묶음끼리는 곱합니다.
    /// </summary>
    public static class DamageCalculator
    {
        public static float Calculate(DamageRequest request)
        {
            float bonus = Mathf.Max(0f, 1f + request.DamageBonus);
            float reduction = 1f - Mathf.Clamp01(request.TargetReduction);
            float mainStat = Mathf.Max(0f, request.MainStatMultiplier);
            float crit = Mathf.Max(0f, request.CritMultiplier);
            return Mathf.Max(0f, request.AttackPower * request.Coefficient * bonus * mainStat * crit * reduction);
        }

        /// <summary>방어 측 감소 적용 (감소율 0~1)</summary>
        public static float ApplyMitigation(float amount, float mitigation)
        {
            return Mathf.Max(0f, amount * (1f - Mathf.Clamp01(mitigation)));
        }
    }
}
