using System;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 캐릭터 기본 능력치 세트. 모든 StatType의 기본값을 에셋 하나에 담습니다.
    /// 비율 능력치는 소수로 입력합니다 (0.05 = 5%).
    /// StatType을 추가하면 필드와 GetBaseValue의 분기도 함께 추가해야 합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "CharacterStatsData", menuName = "BrokenSovereign/Character Stats Data")]
    public class CharacterStatsData : ScriptableObject
    {
        [Header("주 스탯")]
        [Tooltip("주 스탯 사용 여부. 끄면 무직 (주 스탯 피해 보너스 없음)")]
        [SerializeField] private bool _hasMainStat;
        [SerializeField] private StatType _mainStat = StatType.Strength;

        [Header("1차 스탯")]
        [Tooltip("힘. 1당 방어력 +1")]
        [SerializeField, Min(0f)] private float _strength = 10f;
        [Tooltip("민첩. 공격 속도로 수렴 변환 (최대 +50%)")]
        [SerializeField, Min(0f)] private float _agility = 10f;
        [Tooltip("지능. 원소 저항으로 수렴 변환 (최대 +50%)")]
        [SerializeField, Min(0f)] private float _intelligence = 10f;

        [Header("공격")]
        [SerializeField, Min(0f)] private float _attackPower = 100f;
        [Tooltip("모든 피해 증가 (0.1 = 10%)")]
        [SerializeField] private float _damageBonus;
        [Tooltip("물리 피해 증가")]
        [SerializeField] private float _physicalDamageBonus;
        [Tooltip("원소 피해 증가")]
        [SerializeField] private float _elementalDamageBonus;
        [Tooltip("추가 공격 속도 (0.2 = 20%)")]
        [SerializeField] private float _attackSpeed;
        [Tooltip("치명타 확률 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _critChance = 0.05f;
        [Tooltip("치명타 추가 피해 (0.5 = +50%)")]
        [SerializeField, Min(0f)] private float _critDamage = 0.5f;

        [Header("방어")]
        [SerializeField, Min(1f)] private float _maxHealth = 500f;
        [Tooltip("초당 생명력 회복")]
        [SerializeField, Min(0f)] private float _lifeRegen;
        [Tooltip("추가 방어력 (힘 환산분 제외)")]
        [SerializeField, Min(0f)] private float _armor;
        [Tooltip("추가 원소 저항 (지능 환산분 제외)")]
        [SerializeField] private float _elementalResistance;
        [Tooltip("원소 저항 상한 증가 (기본 70%, 최대 85%)")]
        [SerializeField, Min(0f)] private float _maxElementalResistance;
        [Tooltip("회피 확률 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _dodgeChance;
        [Tooltip("받는 피해 감소 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _damageReduction;

        [Header("유틸")]
        [SerializeField, Min(0f)] private float _moveSpeed = 6f;
        [Tooltip("재사용 대기시간 감소 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _cooldownReduction;
        [Tooltip("자원 소모 감소 (0~1)")]
        [SerializeField, Range(0f, 1f)] private float _resourceCostReduction;
        [Tooltip("물약 회복량 증가")]
        [SerializeField] private float _potionHealing;

        /// <summary>주 스탯. 무직이면 null</summary>
        public StatType? MainStat => _hasMainStat ? _mainStat : (StatType?)null;

        public float GetBaseValue(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return _strength;
                case StatType.Agility: return _agility;
                case StatType.Intelligence: return _intelligence;
                case StatType.AttackPower: return _attackPower;
                case StatType.DamageBonus: return _damageBonus;
                case StatType.PhysicalDamageBonus: return _physicalDamageBonus;
                case StatType.ElementalDamageBonus: return _elementalDamageBonus;
                case StatType.AttackSpeed: return _attackSpeed;
                case StatType.CritChance: return _critChance;
                case StatType.CritDamage: return _critDamage;
                case StatType.MaxHealth: return _maxHealth;
                case StatType.LifeRegen: return _lifeRegen;
                case StatType.Armor: return _armor;
                case StatType.ElementalResistance: return _elementalResistance;
                case StatType.MaxElementalResistance: return _maxElementalResistance;
                case StatType.DodgeChance: return _dodgeChance;
                case StatType.DamageReduction: return _damageReduction;
                case StatType.MoveSpeed: return _moveSpeed;
                case StatType.CooldownReduction: return _cooldownReduction;
                case StatType.ResourceCostReduction: return _resourceCostReduction;
                case StatType.PotionHealing: return _potionHealing;
                default: throw new ArgumentOutOfRangeException(nameof(stat), stat, "CharacterStatsData에 필드가 없는 능력치입니다.");
            }
        }

        /// <summary>모든 기본값과 주 스탯을 StatSheet에 씁니다.</summary>
        public void ApplyTo(StatSheet sheet)
        {
            foreach (StatType stat in (StatType[])Enum.GetValues(typeof(StatType)))
            {
                sheet.SetBase(stat, GetBaseValue(stat));
            }

            sheet.SetMainStat(MainStat);
        }

        /// <summary>에셋이 바뀌면 참조 중인 CharacterStats에 알립니다 (Play 중 반영용).</summary>
        public event Action Changed;

        private void OnValidate()
        {
            Changed?.Invoke();
        }
    }
}
