using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>
    /// 영웅의 길 정의 (GDD 5장). 반신 돌파 시 직업의 후보 중 하나를 선택합니다.
    /// 영웅 기술을 지정한 슬롯(기본 4번)에 장착하고, 고유 메커니즘 규칙(HeroRuleDefinition)을 활성화합니다.
    /// 영웅 이름은 홍보물에 노출하지 않고 이 선택 화면에서 처음 공개합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "HeroPathDefinition", menuName = "BrokenSovereign/Hero Path Definition")]
    public class HeroPathDefinition : ScriptableObject
    {
        /// <summary>영웅 기술 외에 함께 장착하는 기술 (석천 5번 모드변환 등)</summary>
        [System.Serializable]
        public struct ExtraSkill
        {
            public SkillSlot Slot;
            public SkillDefinition Skill;

            public ExtraSkill(SkillSlot slot, SkillDefinition skill)
            {
                Slot = slot;
                Skill = skill;
            }
        }

        [SerializeField] private string _heroId = "hero";
        [SerializeField] private string _displayName = "영웅";

        [Tooltip("칭호 (예: 투전성자)")]
        [SerializeField] private string _title = "";

        [Tooltip("고유 메커니즘 이름 (예: 사거리 전환)")]
        [SerializeField] private string _mechanicName = "";

        [TextArea(2, 4)]
        [SerializeField] private string _description = "";

        [Header("기술")]
        [Tooltip("영웅 기술. 비우면 슬롯을 열지 않음 (기술 구현 전)")]
        [SerializeField] private SkillDefinition _heroSkill;

        [SerializeField] private SkillSlot _heroSkillSlot = SkillSlot.Key5;

        [Tooltip("함께 장착하는 기술 (예: 석천 5번 모드변환)")]
        [SerializeField] private ExtraSkill[] _extraSkills = new ExtraSkill[0];

        [Header("고유 메커니즘")]
        [Tooltip("비우면 규칙 없음 (규칙 구현 전)")]
        [SerializeField] private HeroRuleDefinition _rule;

        public string HeroId => _heroId;
        public string DisplayName => _displayName;
        public string Title => _title;
        public string MechanicName => _mechanicName;
        public string Description => _description;
        public SkillDefinition HeroSkill => _heroSkill;
        public SkillSlot HeroSkillSlot => _heroSkillSlot;
        public HeroRuleDefinition Rule => _rule;
        public System.Collections.Generic.IReadOnlyList<ExtraSkill> ExtraSkills => _extraSkills;

#if UNITY_EDITOR
        public void EditorSetup(string id, string displayName, string title, string mechanicName, string description)
        {
            _heroId = id;
            _displayName = displayName;
            _title = title;
            _mechanicName = mechanicName;
            _description = description;
        }

        public void EditorSetSkills(SkillDefinition heroSkill, SkillSlot heroSkillSlot, ExtraSkill[] extraSkills,
            HeroRuleDefinition rule)
        {
            _heroSkill = heroSkill;
            _heroSkillSlot = heroSkillSlot;
            _extraSkills = extraSkills;
            _rule = rule;
        }
#endif
    }
}
