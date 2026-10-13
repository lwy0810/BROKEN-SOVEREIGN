using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 플레이어 현재 직업. 직업이 바뀌면 SkillCaster 슬롯 기술과 직업 게이지(신앙 등)를 교체합니다.
    /// 마나는 모든 직업 공용이라 전환해도 유지됩니다.
    /// 직업 선택 화면(JobSelectView) 구현 전까지 F1로 무직 ↔ 전직 직업을 전환합니다 (디버그).
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public class PlayerJob : MonoBehaviour
    {
        [Tooltip("직업 없음 상태 (공용 기술)")]
        [SerializeField] private JobDefinition _unemployed;

        [Tooltip("F1로 전환할 직업 (성기사)")]
        [SerializeField] private JobDefinition _debugJob;

        [Tooltip("켜면 Play 시작부터 디버그 직업으로 시작")]
        [SerializeField] private bool _startWithDebugJob;

        [SerializeField] private bool _enableDebugKey = true;

        private SkillCaster _caster;
        private FaithController _faith;
        private CharacterStats _stats;

        public JobDefinition Current { get; private set; }

        /// <summary>직업이 바뀌었을 때 (새 직업). 시작 시 첫 적용도 포함</summary>
        public event Action<JobDefinition> JobChanged;

        private void Awake()
        {
            _caster = GetComponent<SkillCaster>();
            _faith = GetComponent<FaithController>();
            _stats = GetComponent<CharacterStats>();
        }

        private void Start()
        {
            ChangeJob(_startWithDebugJob && _debugJob != null ? _debugJob : _unemployed);
        }

        private void Update()
        {
            if (!_enableDebugKey || Keyboard.current == null || !Keyboard.current.f1Key.wasPressedThisFrame)
            {
                return;
            }

            ChangeJob(Current == _debugJob ? _unemployed : _debugJob);
        }

        /// <summary>직업 교체: 슬롯 기술을 직업 기본 기술로 바꾸고, 자원을 켜고 끔</summary>
        public void ChangeJob(JobDefinition job)
        {
            if (job == null)
            {
                Debug.LogWarning($"{name}: 전환할 JobDefinition이 비어 있습니다.", this);
                return;
            }

            Current = job;
            _caster.ClearSkills();

            foreach (SkillSlotAssignment assignment in job.Skills)
            {
                _caster.SetSkill(assignment.Slot, assignment.Skill);
            }

            ApplyResource(job);

            // 주 스탯 (성기사: 힘). 피해 배율 = 1 + 주 스탯 × 0.001
            if (_stats != null)
            {
                _stats.Sheet.SetMainStat(job.MainStat);
            }

            Debug.Log($"직업 전환: {job.DisplayName}");
            JobChanged?.Invoke(job);
        }

        private void ApplyResource(JobDefinition job)
        {
            if (_faith == null)
            {
                if (job.Resource == JobResourceType.Faith)
                {
                    Debug.LogWarning($"{name}: FaithController가 없어 신앙을 사용할 수 없습니다.", this);
                }

                return;
            }

            bool useFaith = job.Resource == JobResourceType.Faith;
            _faith.ResetGauge();
            _faith.SetBuff(job.GaugeBuff);
            _faith.enabled = useFaith;
        }
    }
}
