using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 피해량 계산 (순수 클래스, EditMode 테스트 대상).
    /// 피해량 = 공격력 × 계수 × (1 + 피해 증가) × (1 − 피해 감소)
    /// </summary>
    public static class DamageCalculator
    {
        public static float Calculate(DamageRequest request)
        {
            float bonus = Mathf.Max(0f, 1f + request.DamageBonus);
            float reduction = 1f - Mathf.Clamp01(request.TargetReduction);
            return Mathf.Max(0f, request.AttackPower * request.Coefficient * bonus * reduction);
        }
    }
}
