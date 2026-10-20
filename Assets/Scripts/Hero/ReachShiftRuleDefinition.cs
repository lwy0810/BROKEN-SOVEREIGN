using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>석천 사거리 전환 설정 (모드별 기본 공격 수치, 모드변환 기술 ID)</summary>
    [CreateAssetMenu(fileName = "ReachShiftRule", menuName = "BrokenSovereign/Hero Rule/Reach Shift (석천)")]
    public class ReachShiftRuleDefinition : HeroRuleDefinition
    {
        [Header("장봉 (원거리)")]
        [SerializeField] private float _longRangeBonus = 1.5f;
        [SerializeField] private float _longLineWidth = 0.8f;
        [SerializeField, Min(1)] private int _longMaxTargets = 3;
        [SerializeField] private float _longCoefficient = 0.75f;

        [Header("단봉 (근거리)")]
        [SerializeField] private float _shortCoefficient = 1.3f;
        [SerializeField, Range(0f, 1f)] private float _shortArmorPenetration = 0.3f;

        [Header("HUD")]
        [Tooltip("모드변환 기술 ID (슬롯 이름에 현재 모드 표시)")]
        [SerializeField] private string _toggleSkillId = "reach_shift";
        [SerializeField] private string _toggleSkillName = "모드변환";

        public string ToggleSkillId => _toggleSkillId;
        public string ToggleSkillName => _toggleSkillName;

        public ReachShiftSettings Settings => new ReachShiftSettings
        {
            LongRangeBonus = _longRangeBonus,
            LongLineWidth = _longLineWidth,
            LongMaxTargets = _longMaxTargets,
            LongCoefficient = _longCoefficient,
            ShortCoefficient = _shortCoefficient,
            ShortArmorPenetration = _shortArmorPenetration
        };

        public override HeroRule CreateRule(HeroRuleContext context)
        {
            return new ReachShiftRule(context, this);
        }
    }
}
