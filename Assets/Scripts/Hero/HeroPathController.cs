using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
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

            // 성물 1단계 각성 (반신 = 영웅의 길 선택 시점)
            HeroRelic relic = GetComponent<HeroRelic>();

            if (relic == null)
            {
                relic = gameObject.AddComponent<HeroRelic>();
            }

            relic.Awaken(path);
            Debug.Log($"영웅의 길 선택: {path.DisplayName} ({path.Title})");
            PathSelected?.Invoke(path);
            return true;
        }

        /// <summary>
        /// 영웅의 길 변경 (10/26: 반신부터 격 창 [길 변경]). 이전 규칙을 끄고 직업 기술 구성을 다시 만든 뒤 새 길을 선택합니다.
        /// 성물은 새 길 기준으로 다시 각성, PathSelected가 다시 발생합니다 (신격 트리는 영웅 가지 포인트 반환).
        /// 아직 길이 없으면 일반 선택과 같음. 바꾸면 true
        /// </summary>
        public bool ChangePath(HeroPathDefinition path)
        {
            if (path == null || path == Current)
            {
                return false;
            }

            if (!HasPath)
            {
                return SelectPath(path);
            }

            _rule?.Deactivate();
            _rule = null;
            Current = null;

            // 영웅 기술이 차지했던 슬롯을 직업 기본 구성으로 되돌림
            BrokenSovereign.Ascension.PlayerJob job = GetComponent<BrokenSovereign.Ascension.PlayerJob>();

            if (job != null)
            {
                job.RebuildLoadout();
            }

            return SelectPath(path);
        }

        /// <summary>영웅 기술을 지정 슬롯에 다시 장착 (직업 전환으로 기술 구성이 초기화된 뒤 호출)</summary>
        public void RefreshSkill()
        {
            if (Current == null || _caster == null)
            {
                return;
            }

            Equip(Current.HeroSkillSlot, Current.HeroSkill);

            foreach (HeroPathDefinition.ExtraSkill extra in Current.ExtraSkills)
            {
                Equip(extra.Slot, extra.Skill);
            }
        }

        private void Equip(SkillSlot slot, SkillDefinition skill)
        {
            if (skill == null)
            {
                return;
            }

            SkillLoadout loadout = _caster.Loadout;
            loadout.SetCandidates(slot, new[] { skill });
            loadout.Unlock(skill);
            loadout.Equip(slot, skill, false);
        }
    }
}
