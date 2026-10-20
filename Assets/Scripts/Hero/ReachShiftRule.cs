using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>석천 사거리 전환 규칙: 플레이어에 신봉 모드(StaffReachMode)를 붙이고 단봉으로 시작</summary>
    public class ReachShiftRule : HeroRule
    {
        private readonly ReachShiftRuleDefinition _definition;
        private StaffReachMode _mode;

        public ReachShiftRule(HeroRuleContext context, ReachShiftRuleDefinition definition) : base(context)
        {
            _definition = definition;
        }

        public override void Activate()
        {
            if (Context.Player == null)
            {
                return;
            }

            _mode = Context.Player.GetComponent<StaffReachMode>();

            if (_mode == null)
            {
                _mode = Context.Player.AddComponent<StaffReachMode>();
            }

            _mode.Configure(_definition.Settings, _definition.ToggleSkillId, _definition.ToggleSkillName,
                StaffMode.Short);
        }

        public override void Deactivate()
        {
            if (_mode != null)
            {
                Object.Destroy(_mode);
            }

            _mode = null;
        }
    }
}
