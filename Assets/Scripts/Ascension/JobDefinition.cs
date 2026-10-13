using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 직업 정의 (무직·1차·2차 직업). 슬롯별 기본 기술, 직업 게이지와 게이지 충만 버프를 지정합니다.
    /// 후보 기술 장착(SkillLoadout)은 다음 단계에서 이 기본 구성 위에 덮어씁니다.
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
        [SerializeField] private List<SkillSlotAssignment> _skills = new List<SkillSlotAssignment>();

        public string JobId => _jobId;
        public string DisplayName => _displayName;
        public JobResourceType Resource => _resource;
        public IReadOnlyList<SkillSlotAssignment> Skills => _skills;
        public GaugeBuffSettings GaugeBuff => _gaugeBuff;

        /// <summary>주 스탯. 무직이면 null</summary>
        public StatType? MainStat => _hasMainStat ? _mainStat : (StatType?)null;
    }
}
