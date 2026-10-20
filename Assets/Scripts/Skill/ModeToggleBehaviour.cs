using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 모드 전환 (즉발). 시전자의 IModeToggle(석천 StaffReachMode 등)을 전환합니다.
    /// 사용 기술: 모드변환 (석천 5번)
    /// </summary>
    [CreateAssetMenu(fileName = "ModeToggleBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Mode Toggle")]
    public class ModeToggleBehaviour : SkillBehaviour
    {
        public override void Begin(SkillContext context)
        {
            if (context.Caster.TryGetComponent(out IModeToggle toggle))
            {
                toggle.ToggleMode();
                return;
            }

            Debug.LogWarning($"{context.Definition.DisplayName}: 전환할 모드가 없습니다 (IModeToggle 없음).", context.Caster);
        }
    }
}
