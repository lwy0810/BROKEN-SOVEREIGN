using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 성기사 신앙 게이지 (직업 게이지). HUD 기술 바 위 막대로 표시합니다.
    /// - 내 기본 공격이 피해를 주면 +5, 내가 피해를 받으면 +3
    /// - 마지막 적중·피격 후 5초가 지나면 비전투로 보고 초당 5 감소
    /// - 가득 차면 충만 버프 발동: 지속 동안 능력치 버프 + 모든 기술 강화. 끝나면 0
    /// 버프 내용은 직업(JobDefinition)이 SetBuff로 지정합니다. 직업 전환(PlayerJob)이 켜고 끕니다.
    /// </summary>
    public class FaithController : MonoBehaviour, IJobGauge, ISkillEmpowerSource, IStatModifierSource
    {
        [SerializeField, Min(1f)] private float _max = 100f;
        [SerializeField, Min(0f)] private float _gainPerBasicHit = 5f;
        [SerializeField, Min(0f)] private float _gainPerDamaged = 3f;
        [SerializeField, Min(0f)] private float _decayPerSecond = 5f;

        [Tooltip("마지막 적중·피격 후 이 시간(초)이 지나면 비전투")]
        [SerializeField, Min(0f)] private float _outOfCombatDelay = 5f;

        [SerializeField] private Color _displayColor = new Color(1f, 0.78f, 0.25f);

        [Tooltip("직업이 지정하지 않았을 때 쓰는 버프")]
        [SerializeField] private GaugeBuffSettings _buff = new GaugeBuffSettings();

        private readonly List<StatModifier> _activeModifiers = new List<StatModifier>();
        private FaithModel _model;
        private CombatStateTracker _combat;
        private Health _health;
        private CharacterStats _stats;

        public string GaugeName => "신앙";
        public float Current => Model.Current;
        public float Max => Model.Max;
        public Color DisplayColor => _displayColor;
        public bool IsBuffActive => isActiveAndEnabled && Model.IsBuffActive;
        public string BuffName => _buff.DisplayName;
        public float BuffRemaining => Model.BuffRemaining;
        public bool IsSkillEmpowered => IsBuffActive && _buff.EmpowerSkills;

        private FaithModel Model => _model ??= new FaithModel(_max, _decayPerSecond, _buff.Duration);

        private void Awake()
        {
            _combat = new CombatStateTracker(_outOfCombatDelay);
            _health = GetComponent<Health>();
            _stats = GetComponent<CharacterStats>();
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            EndBuff();
            Model.Reset();
        }

        private void Update()
        {
            _combat.Tick(Time.deltaTime);

            if (Model.Tick(Time.deltaTime, _combat.InCombat))
            {
                EndBuff();
            }
        }

        /// <summary>직업별 충만 버프 지정 (직업 전환 시)</summary>
        public void SetBuff(GaugeBuffSettings buff)
        {
            if (buff == null)
            {
                return;
            }

            EndBuff();
            _buff = buff;
            Model.SetBuffDuration(buff.Duration);
        }

        public void ResetGauge()
        {
            EndBuff();
            Model.Reset();
        }

        public IEnumerable<StatModifier> GetModifiers()
        {
            return _activeModifiers;
        }

        // 피해 전체를 구독해 '내 기본 공격 적중'과 '내 피격'만 골라 충전
        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            float gain = 0f;

            if (victim == _health)
            {
                gain = _gainPerDamaged;
            }
            else if (info.Attacker == gameObject)
            {
                gain = info.IsBasicAttack ? _gainPerBasicHit : 0f;
            }
            else
            {
                return;
            }

            _combat.NotifyCombatAction();

            if (Model.Gain(gain))
            {
                BeginBuff();
            }
        }

        private void BeginBuff()
        {
            _activeModifiers.Clear();

            foreach (StatModifierEntry entry in _buff.Modifiers)
            {
                _activeModifiers.Add(entry.ToModifier());
            }

            if (_stats != null)
            {
                _stats.Sheet.AddSource(this);
            }

            Debug.Log($"{_buff.DisplayName} 발동: {_buff.Duration:0.#}초 동안 버프{(_buff.EmpowerSkills ? " + 기술 강화" : string.Empty)}");
        }

        private void EndBuff()
        {
            if (_activeModifiers.Count == 0)
            {
                return;
            }

            _activeModifiers.Clear();

            if (_stats != null)
            {
                _stats.Sheet.RemoveSource(this);
            }

            Debug.Log($"{_buff.DisplayName} 종료");
        }

        // Play 중 Inspector 변경 반영
        private void OnValidate()
        {
            if (_model == null)
            {
                return;
            }

            _model.SetMax(_max);
            _model.SetBuffDuration(_buff.Duration);
        }
    }
}
