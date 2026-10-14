using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.Serialization;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 직업 정의 (무직·1차·2차 직업). 슬롯별 기본 기술과 교체 후보, 직업 게이지와 게이지 충만 버프를 지정합니다.
    /// 전직 시 PlayerJob이 이 구성으로 SkillLoadout을 만듭니다.
    /// </summary>
    [CreateAssetMenu(fileName = "JobDefinition", menuName = "BrokenSovereign/Job Definition")]
    public class JobDefinition : ScriptableObject
    {
        [SerializeField] private string _jobId = "job";
        [SerializeField] private string _displayName = "직업";
        [Tooltip("주 스탯 사용 여부. 무직은 끔 (주 스탯 피해 보너스 없음)")]
        [SerializeField] private bool _hasMainStat;
        [SerializeField] private StatType _mainStat = StatType.Strength;

        [Tooltip("직업 게이지 (기술 바 위 막대). 무직은 None")]
        [SerializeField] private JobResourceType _resource = JobResourceType.None;

        [Tooltip("게이지가 가득 찼을 때 발동하는 버프")]
        [SerializeField] private GaugeBuffSettings _gaugeBuff = new GaugeBuffSettings();
        [Tooltip("슬롯별 기본 기술과 교체 후보")]
        [FormerlySerializedAs("_skills")]
        [SerializeField] private List<SkillSlotCandidates> _skillSlots = new List<SkillSlotCandidates>();

        public string JobId => _jobId;
        public string DisplayName => _displayName;
        public JobResourceType Resource => _resource;
        public IReadOnlyList<SkillSlotCandidates> SkillSlots => _skillSlots;
        public GaugeBuffSettings GaugeBuff => _gaugeBuff;

        /// <summary>주 스탯. 무직이면 null</summary>
        public StatType? MainStat => _hasMainStat ? _mainStat : (StatType?)null;
    }
}
