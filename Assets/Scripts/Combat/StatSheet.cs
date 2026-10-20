using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 능력치 합산 (순수 클래스, EditMode 테스트 대상).
    /// 최종값 = (기본값 + Flat 합) × (1 + Percent 합). 피해 감소·재사용 대기 감소는 곱셈 쌓기.
    /// 1차 스탯 변환: 힘 → 방어력, 민첩 → 공격 속도, 지능 → 원소 저항, 주 스탯 → 피해 배율.
    /// 비율 값은 소수(0.05 = 5%)입니다.
    /// </summary>
    public class StatSheet
    {
        private readonly Dictionary<StatType, float> _base = new Dictionary<StatType, float>();
        private readonly List<IStatModifierSource> _sources = new List<IStatModifierSource>();
        private StatType? _mainStat;

        /// <summary>기본값·출처·주 스탯이 바뀌었을 때</summary>
        public event Action Changed;

        /// <summary>등록된 수정치 출처 (디버그 출력용)</summary>
        public IReadOnlyList<IStatModifierSource> Sources => _sources;

        /// <summary>주 스탯 (직업별). null이면 무직: 주 스탯 피해 보너스 없음</summary>
        public StatType? MainStat => _mainStat;

        public void SetBase(StatType stat, float value)
        {
            _base[stat] = value;
            Changed?.Invoke();
        }

        public float GetBase(StatType stat)
        {
            return _base.TryGetValue(stat, out float value) ? value : 0f;
        }

        public void SetMainStat(StatType? stat)
        {
            _mainStat = stat;
            Changed?.Invoke();
        }

        public void AddSource(IStatModifierSource source)
        {
            if (source == null || _sources.Contains(source))
            {
                return;
            }

            _sources.Add(source);
            Changed?.Invoke();
        }

        public void RemoveSource(IStatModifierSource source)
        {
            if (_sources.Remove(source))
            {
                Changed?.Invoke();
            }
        }

        /// <summary>출처의 수정치 내용이 바뀌었을 때 호출 (버프 수치 변화 등)</summary>
        public void NotifySourcesChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>1차 스탯 변환·상한을 적용한 최종값</summary>
        public float Get(StatType stat)
        {
            switch (stat)
            {
                case StatType.Armor:
                    return GetRaw(StatType.Armor) + GetRaw(StatType.Strength) * StatFormula.ARMOR_PER_STRENGTH;

                case StatType.AttackSpeed:
                    return GetRaw(StatType.AttackSpeed) + StatFormula.Converge(
                        GetRaw(StatType.Agility), StatFormula.AGILITY_ATTACK_SPEED_MAX, StatFormula.AGILITY_K);

                case StatType.ElementalResistance:
                    float cap = StatFormula.ResistanceCap(GetRaw(StatType.MaxElementalResistance));
                    float resistance = GetRaw(StatType.ElementalResistance) + StatFormula.Converge(
                        GetRaw(StatType.Intelligence), StatFormula.INTELLIGENCE_RESISTANCE_MAX, StatFormula.INTELLIGENCE_K);
                    return Mathf.Clamp(resistance, 0f, cap);

                case StatType.CritChance:
                    return Mathf.Clamp01(GetRaw(StatType.CritChance));

                default:
                    return GetRaw(stat);
            }
        }

        /// <summary>주 스탯 피해 배율: 1 + 주 스탯 × 0.1%. 무직이면 1</summary>
        public float MainStatDamageMultiplier =>
            _mainStat.HasValue ? 1f + Mathf.Max(0f, GetRaw(_mainStat.Value)) * StatFormula.MAIN_STAT_DAMAGE_PER_POINT : 1f;

        /// <summary>공격 측 피해 증가 합: 전체 + 속성별 (같은 묶음은 더함)</summary>
        public float GetDamageBonus(DamageType type)
        {
            StatType typeBonus = type == DamageType.Physical ? StatType.PhysicalDamageBonus : StatType.ElementalDamageBonus;
            return Get(StatType.DamageBonus) + Get(typeBonus);
        }

        /// <summary>방어 측 최종 감소율: 속성 감소(방어력 또는 원소 저항)와 전체 피해 감소를 곱셈 쌓기</summary>
        /// <param name="armorPenetration">방어 관통 (0.3 = 방어력 30% 무시). 물리 피해에만 적용</param>
        public float GetMitigation(DamageType type, float armorPenetration = 0f)
        {
            float typeReduction = type == DamageType.Physical
                ? StatFormula.ArmorReduction(Get(StatType.Armor) * (1f - Mathf.Clamp01(armorPenetration)))
                : Get(StatType.ElementalResistance);
            return StatFormula.StackMultiplicative(typeReduction, Get(StatType.DamageReduction));
        }

        /// <summary>1차 스탯 변환 없이 기본값과 수정치만 합산한 값</summary>
        public float GetRaw(StatType stat)
        {
            float flat = 0f;
            float percent = 0f;
            float remaining = 1f;

            foreach (IStatModifierSource source in _sources)
            {
                foreach (StatModifier modifier in source.GetModifiers())
                {
                    if (modifier.Stat != stat)
                    {
                        continue;
                    }

                    switch (modifier.Type)
                    {
                        case ModifierType.Flat:
                            flat += modifier.Value;
                            break;
                        case ModifierType.Percent:
                            percent += modifier.Value;
                            break;
                        case ModifierType.Multiplicative:
                            remaining *= 1f - Mathf.Clamp01(modifier.Value);
                            break;
                    }
                }
            }

            float additive = (GetBase(stat) + flat) * (1f + percent);

            if (IsMultiplicativeStat(stat))
            {
                // 기본값·Flat 합도 하나의 출처로 보고 곱셈 쌓기
                return 1f - remaining * (1f - Mathf.Clamp01(additive));
            }

            return additive;
        }

        private static bool IsMultiplicativeStat(StatType stat)
        {
            return stat == StatType.DamageReduction || stat == StatType.CooldownReduction;
        }
    }
}
