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
    /// 2차 직업은 상위 직업(_parentJob)의 슬롯 구성을 물려받고, 같은 슬롯을 지정하면 덮어씁니다.
    /// 다음 직업(_advancements)은 격 돌파 시 PlayerJob.TryAdvance가 확인합니다 (프로토타입: 첫 번째 직업으로 자동 전직).
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

        [Header("전직")]
        [Tooltip("상위 직업. 슬롯 구성을 물려받음 (성투사 → 성기사)")]
        [SerializeField] private JobDefinition _parentJob;

        [Tooltip("이 직업으로 전직할 수 있는 격 (성투사: 초인)")]
        [SerializeField] private AscensionTier _requiredTier = AscensionTier.Mortal;

        [Tooltip("다음 직업 후보 (성기사 → 성투사·심판관). 프로토타입은 조건을 만족하는 첫 번째 직업으로 자동 전직")]
        [SerializeField] private List<JobDefinition> _advancements = new List<JobDefinition>();

        // 상위 직업 순환 참조 방지
        private const int MAX_PARENT_DEPTH = 8;

        public string JobId => _jobId;
        public string DisplayName => _displayName;
        public JobResourceType Resource => _resource;
        public IReadOnlyList<SkillSlotCandidates> SkillSlots => _skillSlots;
        public GaugeBuffSettings GaugeBuff => _gaugeBuff;
        public JobDefinition ParentJob => _parentJob;
        public AscensionTier RequiredTier => _requiredTier;
        public IReadOnlyList<JobDefinition> Advancements => _advancements;

        /// <summary>상위 직업 슬롯 + 이 직업 슬롯 (같은 슬롯은 이 직업 우선, 슬롯 순서 정렬)</summary>
        public List<SkillSlotCandidates> ResolveSkillSlots()
        {
            var chain = new List<JobDefinition>();

            for (JobDefinition job = this; job != null && chain.Count < MAX_PARENT_DEPTH; job = job._parentJob)
            {
                if (chain.Contains(job))
                {
                    Debug.LogWarning($"{_displayName}: 상위 직업이 순환 참조입니다.", this);
                    break;
                }

                chain.Add(job);
            }

            // 가장 위 직업부터 덮어쓰기
            var layers = new List<IEnumerable<SkillSlotCandidates>>();

            for (int i = chain.Count - 1; i >= 0; i--)
            {
                layers.Add(chain[i]._skillSlots);
            }

            return MergeSlots(layers);
        }

        /// <summary>격 조건을 만족하는 첫 번째 다음 직업. 없으면 null</summary>
        public JobDefinition FindAdvancement(AscensionTier tier)
        {
            foreach (JobDefinition next in _advancements)
            {
                if (next != null && next._requiredTier <= tier)
                {
                    return next;
                }
            }

            return null;
        }

        /// <summary>슬롯 구성 겹치기 (뒤 구성이 같은 슬롯을 덮어씀). 결과는 슬롯 순서 정렬</summary>
        public static List<SkillSlotCandidates> MergeSlots(IEnumerable<IEnumerable<SkillSlotCandidates>> layers)
        {
            var bySlot = new SortedDictionary<SkillSlot, SkillSlotCandidates>();

            foreach (IEnumerable<SkillSlotCandidates> layer in layers)
            {
                if (layer == null)
                {
                    continue;
                }

                foreach (SkillSlotCandidates slot in layer)
                {
                    bySlot[slot.Slot] = slot;
                }
            }

            return new List<SkillSlotCandidates>(bySlot.Values);
        }

        /// <summary>주 스탯. 무직이면 null</summary>
        public StatType? MainStat => _hasMainStat ? _mainStat : (StatType?)null;
    }
}
