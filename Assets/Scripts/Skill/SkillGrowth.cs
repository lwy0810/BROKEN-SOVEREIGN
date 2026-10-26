using System.Collections.Generic;
using BrokenSovereign.Core;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 성장 규칙 (10/21 기술 창 개편, GDD 5.4 축소판). 순수 계산.
    /// - 슬롯 레벨 1~15. 레벨업당 기술 포인트 1, 포인트 1 = 슬롯 레벨 +1 (기술을 바꿔도 슬롯 레벨 유지)
    /// - 효과: 슬롯 레벨 1당 피해 +5% (피해 계수 키). 피해 계수가 없는 기술은 효과량(피해 감소·약화율) +5%
    /// - 강화 분기: 우클릭 슬롯만, 슬롯 레벨 3부터 A 확산(주변 적에게 피해 분산) / B 응징(피해 +20%) 택1. 기술별로 저장
    ///   (10/26: 강타를 단일 대상으로 정리하면서 A 광휘(범위 +25%) → 확산으로 변경)
    /// - 기본 공격(좌클릭)과 효과 수치가 없는 기술(모드변환)은 성장 없음
    /// </summary>
    public static class SkillGrowth
    {
        public const int MAX_LEVEL = 15;
        public const float PER_LEVEL = 0.05f;
        public const SkillSlot BRANCH_SLOT = SkillSlot.RightClick;
        public const int BRANCH_LEVEL = 3;
        public const float BRANCH_DAMAGE = 0.2f;

        /// <summary>확산: 대상 주변 이 반경(m) 안의 적을 최대 SPREAD_COUNT명 더 맞히고, 총 피해(SPREAD_TOTAL배)를 맞은 수로 나눔</summary>
        public const int SPREAD_COUNT = 3;
        public const float SPREAD_RADIUS = 2.5f;
        public const float SPREAD_TOTAL = 1.6f;

        public static readonly string[] BRANCH_NAMES = { "확산", "응징" };
        public static readonly string[] BRANCH_DESCRIPTIONS = { "주변 적 3명까지 피해 분산", "피해 +20%" };

        /// <summary>분기 카드 둘째 줄 괄호 안 보충 설명 (없으면 빈 문자열)</summary>
        public static readonly string[] BRANCH_NOTES = { "총 최대 160%", string.Empty };

        private static readonly string[] DAMAGE_KEYS =
        {
            SkillParameterKeys.COEFFICIENT, SkillParameterKeys.EXECUTE_COEFFICIENT,
            SkillParameterKeys.SHOCKWAVE_COEFFICIENT, SkillParameterKeys.FINISH_COEFFICIENT
        };

        private static readonly string[] EFFECT_KEYS =
        {
            SkillParameterKeys.DAMAGE_REDUCTION, SkillParameterKeys.ATTACK_POWER_REDUCTION, SkillParameterKeys.ARMOR_REDUCTION
        };

        /// <summary>이 슬롯·기술이 레벨로 성장하는지</summary>
        public static bool CanGrow(SkillSlot slot, SkillDefinition skill)
        {
            if (skill == null || slot == SkillSlot.LeftClick)
            {
                return false;
            }

            SkillParameterSet set = skill.CreateParameters();
            return HasAny(set, DAMAGE_KEYS) || HasAny(set, EFFECT_KEYS);
        }

        public static bool HasBranch(SkillSlot slot)
        {
            return slot == BRANCH_SLOT;
        }

        /// <summary>시전 수치에 슬롯 레벨·분기 효과 적용</summary>
        public static void Apply(SkillParameterSet set, SkillSlot slot, int level, int branch)
        {
            if (slot == SkillSlot.LeftClick)
            {
                return;
            }

            float bonus = PER_LEVEL * (level - 1);
            string[] keys = HasAny(set, DAMAGE_KEYS) ? DAMAGE_KEYS : EFFECT_KEYS;

            if (bonus > 0f)
            {
                foreach (string key in keys)
                {
                    set.Apply(new SkillParameterModifier(key, ModifierType.Percent, bonus));
                }
            }

            if (!HasBranch(slot) || level < BRANCH_LEVEL)
            {
                return;
            }

            if (branch == 0)
            {
                set.Apply(new SkillParameterModifier(SkillParameterKeys.SPREAD_COUNT, ModifierType.Flat, SPREAD_COUNT));
                set.Apply(new SkillParameterModifier(SkillParameterKeys.SPREAD_RADIUS, ModifierType.Flat, SPREAD_RADIUS));
                set.Apply(new SkillParameterModifier(SkillParameterKeys.SPREAD_TOTAL, ModifierType.Flat, SPREAD_TOTAL));
            }
            else if (branch == 1)
            {
                foreach (string key in DAMAGE_KEYS)
                {
                    set.Apply(new SkillParameterModifier(key, ModifierType.Percent, BRANCH_DAMAGE));
                }
            }
        }

        /// <summary>표시용 수치 (레벨·분기 적용)</summary>
        public static SkillParameterSet Preview(SkillDefinition skill, SkillSlot slot, int level, int branch)
        {
            SkillParameterSet set = skill.CreateParameters();
            Apply(set, slot, level, branch);
            return set;
        }

        /// <summary>창에 보여 줄 수치 줄: 이름, 키, 비율 표시 여부</summary>
        public static IEnumerable<(string Label, string Key, bool Percent)> DisplayKeys()
        {
            yield return ("피해 계수", SkillParameterKeys.COEFFICIENT, true);
            yield return ("처형 계수", SkillParameterKeys.EXECUTE_COEFFICIENT, true);
            yield return ("충격파 계수", SkillParameterKeys.SHOCKWAVE_COEFFICIENT, true);
            yield return ("마무리 계수", SkillParameterKeys.FINISH_COEFFICIENT, true);
            yield return ("받는 피해 감소", SkillParameterKeys.DAMAGE_REDUCTION, true);
            yield return ("적 공격력 감소", SkillParameterKeys.ATTACK_POWER_REDUCTION, true);
            yield return ("적 방어력 감소", SkillParameterKeys.ARMOR_REDUCTION, true);
            yield return ("범위", SkillParameterKeys.RADIUS, false);
            yield return ("지속", SkillParameterKeys.DURATION, false);
        }

        private static bool HasAny(SkillParameterSet set, string[] keys)
        {
            foreach (string key in keys)
            {
                if (set.Has(key) && set.Get(key) > 0f)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
