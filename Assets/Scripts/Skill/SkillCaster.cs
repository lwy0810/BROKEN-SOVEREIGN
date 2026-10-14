using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 슬롯에 연결된 기술을 시전합니다:
    /// 재사용 대기·마나 확인 → 마나 소모 → 수치 생성(직업 게이지 버프 중이면 충만 수정치) → SkillBehaviour 실행 → 재사용 대기 시작.
    /// 지속 기술은 끝날 때까지 매 프레임 Tick을 호출합니다.
    /// 슬롯 기술은 SkillLoadout(후보·해금·장착)에서 읽습니다. 직업 전환(PlayerJob)이 Loadout을 구성합니다.
    /// 기술 교체는 비전투 상태에서만 가능합니다 (마지막 시전·적중·피격 후 일정 시간).
    /// </summary>
    public class SkillCaster : MonoBehaviour
    {
        [Tooltip("마지막 전투 행동 후 비전투로 보는 시간 (초). 비전투일 때만 기술 교체 가능")]
        [SerializeField, Min(0f)] private float _outOfCombatDelay = 5f;

        [Tooltip("기술 피해 대상 레이어 (Enemy)")]
        [SerializeField] private LayerMask _targetMask;

        private readonly CooldownTracker _cooldowns = new CooldownTracker();
        private readonly List<SkillContext> _activeCasts = new List<SkillContext>();
        private readonly List<ISkillResource> _resourceBuffer = new List<ISkillResource>();
        private readonly List<ISkillEmpowerSource> _empowerBuffer = new List<ISkillEmpowerSource>();
        private CharacterStats _stats;
        private Health _health;
        private CombatStateTracker _combat;

        /// <summary>기술을 시전했을 때 (슬롯, 기술)</summary>
        public event Action<SkillSlot, SkillDefinition> SkillCast;

        /// <summary>슬롯 기술 구성 (후보·해금·장착)</summary>
        public SkillLoadout Loadout { get; } = new SkillLoadout();

        /// <summary>전투 중이면 기술 교체 불가</summary>
        public bool InCombat => _combat != null && _combat.InCombat;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();
            _health = GetComponent<Health>();
            _combat = new CombatStateTracker(_outOfCombatDelay);
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
        }

        private void Update()
        {
            _combat.Tick(Time.deltaTime);
            _cooldowns.Tick(Time.deltaTime);
            TickActiveCasts(Time.deltaTime);
        }

        /// <summary>진행 중인 지속 기술 (HUD 버프 표시용)</summary>
        public IReadOnlyList<SkillContext> ActiveCasts => _activeCasts;

        /// <summary>이동을 잠그는 기술(돌진 등)이 진행 중인지</summary>
        public bool IsMovementLocked
        {
            get
            {
                foreach (SkillContext context in _activeCasts)
                {
                    if (context.Definition.LocksMovement)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool HasSkill(SkillSlot slot)
        {
            return GetSkill(slot) != null;
        }

        public SkillDefinition GetSkill(SkillSlot slot)
        {
            return Loadout.GetEquipped(slot);
        }

        /// <summary>후보 기술 장착 (비전투일 때만). 재사용 대기는 기술 ID 기준이라 교체해도 유지</summary>
        public EquipResult TryEquip(SkillSlot slot, SkillDefinition skill)
        {
            return Loadout.Equip(slot, skill, InCombat);
        }

        /// <summary>해금된 다음 후보로 교체 (디버그 순환 장착)</summary>
        public EquipResult EquipNext(SkillSlot slot)
        {
            return Loadout.EquipNext(slot, InCombat);
        }

        /// <summary>기술 키 유지 시 이동 방식. 기술이 없으면 StopInRange</summary>
        /// <summary>시전 동작 시간 (이동 불가 시간). 기술이 없으면 0</summary>
        public float GetCastTime(SkillSlot slot)
        {
            SkillDefinition skill = GetSkill(slot);
            return skill != null ? skill.CastTime : 0f;
        }

        public SkillCastMovement GetCastMovement(SkillSlot slot)
        {
            SkillDefinition skill = GetSkill(slot);
            return skill != null ? skill.CastMovement : SkillCastMovement.StopInRange;
        }

        /// <summary>
        /// 시전 사거리 (SkillRange 규칙: castRange → 반경 × 0.8 → 0). 충만 중이면 강화된 수치로 계산.
        /// 기술이 없거나 사거리 수치가 없으면 0
        /// </summary>
        public float GetCastRange(SkillSlot slot)
        {
            SkillDefinition skill = GetSkill(slot);
            return skill != null ? SkillRange.Resolve(skill.CreateParameters(IsEmpowered())) : 0f;
        }

        public bool TryCast(SkillSlot slot, Vector3 cursorPoint)
        {
            SkillDefinition skill = GetSkill(slot);

            if (skill == null || !_cooldowns.IsReady(skill.SkillId))
            {
                return false;
            }

            if (skill.Behaviour == null)
            {
                Debug.LogWarning($"SkillCaster: {skill.DisplayName}에 SkillBehaviour가 없습니다.", skill);
                return false;
            }

            if (skill.ResourceCost > 0f)
            {
                ISkillResource resource = FindActiveResource();

                if (resource == null || !resource.TrySpend(skill.ResourceCost))
                {
                    return false;
                }
            }

            bool empowered = IsEmpowered();

            StatSheet stats = _stats != null ? _stats.Sheet : null;
            var context = new SkillContext(skill, transform, cursorPoint, skill.CreateParameters(empowered), stats,
                _targetMask, empowered);

            skill.Behaviour.Begin(context);
            _activeCasts.Add(context);
            _cooldowns.Start(skill.SkillId, GetCooldown(skill, stats));
            _combat.NotifyCombatAction();
            SkillCast?.Invoke(slot, skill);
            return true;
        }

        /// <summary>HUD 표시용. 기술이 없는 슬롯은 미개방</summary>
        public SkillSlotState GetSlotState(SkillSlot slot)
        {
            SkillDefinition skill = GetSkill(slot);

            if (skill == null)
            {
                return SkillSlotState.Locked;
            }

            return new SkillSlotState(true, _cooldowns.Remaining(skill.SkillId), _cooldowns.Duration(skill.SkillId));
        }

        /// <summary>직업 게이지 버프 등으로 기술 강화 중인지</summary>
        public bool IsEmpowered()
        {
            GetComponents(_empowerBuffer);

            foreach (ISkillEmpowerSource source in _empowerBuffer)
            {
                if (source is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                if (source.IsSkillEmpowered)
                {
                    return true;
                }
            }

            return false;
        }

        // 내 피격·내 공격 적중이면 전투 상태 갱신
        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            if (victim == _health || info.Attacker == gameObject)
            {
                _combat.NotifyCombatAction();
            }
        }

        // 켜져 있는 기술 비용 자원 (마나)
        private ISkillResource FindActiveResource()
        {
            GetComponents(_resourceBuffer);

            foreach (ISkillResource resource in _resourceBuffer)
            {
                if (resource is Behaviour behaviour && !behaviour.isActiveAndEnabled)
                {
                    continue;
                }

                return resource;
            }

            return null;
        }

        // 재사용 대기 감소 능력치 적용 (최대 90%)
        private static float GetCooldown(SkillDefinition skill, StatSheet stats)
        {
            float reduction = stats != null ? Mathf.Clamp(stats.Get(StatType.CooldownReduction), 0f, 0.9f) : 0f;
            return skill.Cooldown * (1f - reduction);
        }

        private void TickActiveCasts(float deltaTime)
        {
            for (int i = _activeCasts.Count - 1; i >= 0; i--)
            {
                SkillContext context = _activeCasts[i];
                context.Elapsed += deltaTime;

                if (context.Definition.Behaviour.Tick(context, deltaTime))
                {
                    continue;
                }

                context.Definition.Behaviour.End(context);
                _activeCasts.RemoveAt(i);
            }
        }
    }
}
