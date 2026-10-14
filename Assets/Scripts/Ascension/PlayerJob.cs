using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Serialization;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 플레이어 현재 직업. 직업이 바뀌면 SkillCaster의 기술 구성(SkillLoadout)과 직업 게이지(신앙 등)를 교체합니다.
    /// 마나는 모든 직업 공용이라 전환해도 유지됩니다.
    /// 직업 선택 화면(JobSelectView) 구현 전까지 F1로 무직 ↔ 전직 직업을 전환합니다 (디버그).
    /// </summary>
    [RequireComponent(typeof(SkillCaster))]
    public class PlayerJob : MonoBehaviour
    {
        [Tooltip("직업 없음 상태 (공용 기술)")]
        [SerializeField] private JobDefinition _unemployed;

        [Tooltip("직업 선택 화면에서 고를 수 있는 1차 직업 (프로토타입: 성기사). F1 디버그 전환 대상")]
        [FormerlySerializedAs("_debugJob")]
        [SerializeField] private JobDefinition _firstJob;

        [Tooltip("켜면 Play 시작부터 디버그 직업으로 시작")]
        [SerializeField] private bool _startWithDebugJob;

        [SerializeField] private bool _enableDebugKey = true;

        [Tooltip("해금 조건 구현 전까지 직업의 모든 후보 기술을 해금 (디버그)")]
        [SerializeField] private bool _unlockAllCandidates = true;

        private SkillCaster _caster;
        private FaithController _faith;
        private CharacterStats _stats;

        public JobDefinition Current { get; private set; }

        /// <summary>직업 선택 화면에서 고를 수 있는 1차 직업</summary>
        public JobDefinition SelectableJob => _firstJob;

        /// <summary>무직이 아님 (1차 직업 선택 완료)</summary>
        public bool HasJob => Current != null && Current != _unemployed;

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
            ChangeJob(_startWithDebugJob && _firstJob != null ? _firstJob : _unemployed);
        }

        private void Update()
        {
            if (!_enableDebugKey || Keyboard.current == null || !Keyboard.current.f1Key.wasPressedThisFrame)
            {
                return;
            }

            ChangeJob(Current == _firstJob ? _unemployed : _firstJob);
        }

        /// <summary>직업 선택 화면에서 1차 직업 선택 (무직일 때만)</summary>
        public void ChooseJob(JobDefinition job)
        {
            if (HasJob)
            {
                return;
            }

            ChangeJob(job);
        }

        /// <summary>직업 교체: 슬롯 후보를 직업 구성으로 바꾸고 기본 기술 장착, 자원을 켜고 끔</summary>
        public void ChangeJob(JobDefinition job)
        {
            if (job == null)
            {
                Debug.LogWarning($"{name}: 전환할 JobDefinition이 비어 있습니다.", this);
                return;
            }

            Current = job;
            BuildLoadout(job);

            ApplyResource(job);

            // 주 스탯 (성기사: 힘). 피해 배율 = 1 + 주 스탯 × 0.001
            if (_stats != null)
            {
                _stats.Sheet.SetMainStat(job.MainStat);
            }

            Debug.Log($"직업 전환: {job.DisplayName}");
            JobChanged?.Invoke(job);
        }

        // 후보 지정 → 해금 → 기본 기술 장착. 전직은 전투 중에도 기본 기술로 바로 교체
        private void BuildLoadout(JobDefinition job)
        {
            SkillLoadout loadout = _caster.Loadout;
            loadout.ClearSlots();

            foreach (SkillSlotCandidates slot in job.SkillSlots)
            {
                loadout.SetCandidates(slot.Slot, slot.AllCandidates());
                loadout.Unlock(slot.Default);

                if (_unlockAllCandidates)
                {
                    foreach (SkillDefinition skill in slot.AllCandidates())
                    {
                        loadout.Unlock(skill);
                    }
                }

                if (slot.Default != null)
                {
                    loadout.Equip(slot.Slot, slot.Default, false);
                }
            }

            WarnIfDefaultMissing(job, loadout);
        }

        // 같은 기술을 여러 슬롯의 기본 기술로 지정하면 마지막 슬롯에만 장착되고 앞 슬롯은 비게 됨
        private void WarnIfDefaultMissing(JobDefinition job, SkillLoadout loadout)
        {
            foreach (SkillSlotCandidates slot in job.SkillSlots)
            {
                if (slot.Default != null && loadout.GetEquipped(slot.Slot) != slot.Default)
                {
                    Debug.LogWarning($"{job.DisplayName}: {slot.Slot} 슬롯의 기본 기술 '{slot.Default.DisplayName}'이 " +
                        "다른 슬롯에도 기본 기술로 지정되어 이 슬롯이 비었습니다. JobDefinition의 Skill Slots를 확인하세요.", job);
                }
            }
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
