using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 능력치 변환식과 상한 (순수 함수, EditMode 테스트 대상).
    /// 스탯이 수천 단위까지 오르는 것을 전제로, 비율 효과는 수렴식으로 100%를 넘지 않게 합니다.
    /// 상한 규칙은 디아블로 4 방식: 물리 피해 감소 85%, 원소 저항 70%(최대 85%), 치명타 확률 100%,
    /// 피해 감소·재사용 대기 감소는 상한 없이 곱셈 쌓기.
    /// 수치는 밸런스 에셋 도입 시 이전합니다.
    /// </summary>
    public static class StatFormula
    {
        public const float MAIN_STAT_DAMAGE_PER_POINT = 0.001f;

        public const float ARMOR_PER_STRENGTH = 1f;
        public const float ARMOR_K = 1000f;
        public const float ARMOR_REDUCTION_CAP = 0.85f;

        public const float AGILITY_ATTACK_SPEED_MAX = 0.5f;
        public const float AGILITY_K = 1000f;

        public const float INTELLIGENCE_RESISTANCE_MAX = 0.5f;
        public const float INTELLIGENCE_K = 1000f;

        public const float RESISTANCE_CAP = 0.7f;
        public const float RESISTANCE_HARD_CAP = 0.85f;

        /// <summary>
        /// 수렴식: max × value ÷ (value + k). value = k일 때 max의 절반. 아무리 커도 max 미만.
        /// </summary>
        public static float Converge(float value, float max, float k)
        {
            if (value <= 0f || k <= 0f)
            {
                return 0f;
            }

            return max * value / (value + k);
        }

        /// <summary>방어력 → 물리 피해 감소율 (상한 85%)</summary>
        public static float ArmorReduction(float armor)
        {
            if (armor <= 0f)
            {
                return 0f;
            }

            return Mathf.Min(ARMOR_REDUCTION_CAP, armor / (armor + ARMOR_K));
        }

        /// <summary>원소 저항 상한: 기본 70% + 최대 원소 저항 증가, 최대 85%</summary>
        public static float ResistanceCap(float maxResistanceBonus)
        {
            return Mathf.Clamp(RESISTANCE_CAP + maxResistanceBonus, 0f, RESISTANCE_HARD_CAP);
        }

        /// <summary>곱셈 쌓기: 1 − (1 − a)(1 − b)</summary>
        public static float StackMultiplicative(float a, float b)
        {
            return 1f - (1f - Mathf.Clamp01(a)) * (1f - Mathf.Clamp01(b));
        }

        /// <summary>치명타 판정. roll은 0~1 무작위 값 (테스트에서 고정값 주입)</summary>
        public static bool RollCrit(float critChance, float roll)
        {
            return roll < Mathf.Clamp01(critChance);
        }
    }
}
