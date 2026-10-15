using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>
    /// 선택한 영웅의 길. 영웅 기술 장착과 고유 메커니즘 규칙의 수명을 관리합니다.
    /// 선택 요청은 반신 돌파 시 PlayerProgression이 보내고(HeroPathSelectionRequested), 선택 화면이 SelectPath를 호출합니다.
    /// 직업이 바뀌어 기술 구성이 다시 만들어지면 PlayerJob이 RefreshSkill로 영웅 기술을 다시 장착합니다.
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public class HeroPathController : MonoBehaviour
    {
        private SkillCaster _caster;
        private HeroRule _rule;

        public HeroPathDefinition Current { get; private set; }

        public bool HasPath => Current != null;

        /// <summary>영웅의 길을 선택했을 때</summary>
        public event Action<HeroPathDefinition> PathSelected;

        private void Awake()
        {
            _caster = GetComponent<SkillCaster>();
        }

        private void OnDestroy()
        {
            _rule?.Deactivate();
            _rule = null;
        }

        /// <summary>영웅의 길 선택 (한 번만). 선택하면 true</summary>
        public bool SelectPath(HeroPathDefinition path)
        {
            if (path == null || HasPath)
            {
                return false;
            }

            Current = path;

            if (path.Rule != null)
            {
                var context = new HeroRuleContext(gameObject, _caster, GetComponent<CharacterStats>());
                _rule = path.Rule.CreateRule(context);
                _rule?.Activate();
            }

            RefreshSkill();
            Debug.Log($"영웅의 길 선택: {path.DisplayName} ({path.Title})");
            PathSelected?.Invoke(path);
            return true;
        }

        /// <summary>영웅 기술을 지정 슬롯에 다시 장착 (직업 전환으로 기술 구성이 초기화된 뒤 호출)</summary>
        public void RefreshSkill()
        {
            if (Current == null || Current.HeroSkill == null || _caster == null)
            {
                return;
            }

            SkillLoadout loadout = _caster.Loadout;
            loadout.SetCandidates(Current.HeroSkillSlot, new[] { Current.HeroSkill });
            loadout.Unlock(Current.HeroSkill);
            loadout.Equip(Current.HeroSkillSlot, Current.HeroSkill, false);
        }
    }
}
