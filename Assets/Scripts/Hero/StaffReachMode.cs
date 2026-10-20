using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>
    /// 석천 신봉 모드 상태 (사거리 전환 규칙이 플레이어에 붙임).
    /// 기본 공격 수정치(IBasicAttackModifier), 모드변환 기술 대상(IModeToggle), HUD 슬롯 이름(ISkillLabelSource)을 제공합니다.
    /// </summary>
    [DisallowMultipleComponent]
    public class StaffReachMode : MonoBehaviour, IBasicAttackModifier, IModeToggle, ISkillLabelSource
    {
        private ReachShiftSettings _settings = ReachShiftSettings.Default;
        private string _toggleSkillId;
        private string _toggleSkillName;

        public StaffMode Mode { get; private set; } = StaffMode.Short;

        /// <summary>모드가 바뀌었을 때</summary>
        public event Action<StaffMode> ModeChanged;

        public void Configure(ReachShiftSettings settings, string toggleSkillId, string toggleSkillName,
            StaffMode startMode)
        {
            _settings = settings;
            _toggleSkillId = toggleSkillId;
            _toggleSkillName = toggleSkillName;
            Mode = startMode;
        }

        public void ToggleMode()
        {
            Mode = ReachShift.Toggle(Mode);
            Debug.Log($"석천 모드변환: {ReachShift.DisplayName(Mode)}");
            ModeChanged?.Invoke(Mode);
        }

        public void ModifyBasicAttack(ref BasicAttackProfile profile)
        {
            ReachShift.Apply(Mode, _settings, ref profile);
        }

        public bool TryGetSkillLabel(string skillId, out string label)
        {
            if (string.IsNullOrEmpty(_toggleSkillId) || skillId != _toggleSkillId)
            {
                label = null;
                return false;
            }

            label = $"{_toggleSkillName} · {ReachShift.DisplayName(Mode)}";
            return true;
        }
    }
}
