using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 플레이어 경험치·레벨과 격 단계.
    /// 적 처치 시 경험치 획득 → 레벨업 시 능력치 자동 분배(스탯 분배 UI 대신, GDD 11장) → 조건 충족 시 격 돌파.
    /// 무직 상태에서 직업 선택 레벨에 도달하면 직업 선택을 요청합니다 (JobSelectionRequested → 직업 선택 화면).
    /// 레벨 보너스는 레벨이 오른 시점의 직업 기준으로 누적되어 유지됩니다 (무직 때 얻은 힘·민첩·지능은 전직 후에도 유지).
    /// 레벨·격 보너스는 능력치 출처(IStatModifierSource)로 등록합니다.
    /// </summary>
    [RequireComponent(typeof(PlayerJob))]
    public class PlayerProgression : MonoBehaviour, IDebugInfoProvider
    {
        [Serializable]
        public class LevelSettings
        {
            [Tooltip("레벨 1 → 2 필요 경험치")]
            [Min(1)] public int BaseExperience = 100;

            [Tooltip("레벨마다 필요 경험치 증가율")]
            [Min(1f)] public float Growth = 1.25f;

            [Min(1)] public int MaxLevel = 15;

            [Tooltip("레벨당 주 스탯 증가 (직업이 있을 때)")]
            public float MainStatPerLevel = 5f;

            [Tooltip("레벨당 힘·민첩·지능 각각 증가 (무직일 때)")]
            public float AllStatsPerLevel = 2f;

            public float MaxHealthPerLevel = 10f;

            [Tooltip("무직 상태에서 이 레벨에 도달하면 직업 선택 화면")]
            [Min(1)] public int JobSelectLevel = 2;
        }

        [Serializable]
        public class AscensionSettings
        {
            [Min(1)] public int SuperhumanLevel = 5;
            [Min(1)] public int DemigodLevel = 10;

            [Tooltip("격 단계당 최대 체력 증가")]
            public float MaxHealthPerTier = 50f;

            [Tooltip("격 단계당 공격력 증가")]
            public float AttackPowerPerTier = 5f;
        }

        [SerializeField] private LevelSettings _level = new LevelSettings();
        [SerializeField] private AscensionSettings _ascension = new AscensionSettings();

        [Tooltip("F2: 다음 레벨까지 경험치 획득 (디버그)")]
        [SerializeField] private bool _enableDebugKey = true;

        [Tooltip("레벨업·격 돌파 시 최종 능력치를 Console에 출력 (디버그)")]
        [SerializeField] private bool _logStatsOnLevelUp = true;

        // 출처를 레벨·격으로 나눠 등록 (디버그 출력에서 출처별 표시)
        private sealed class NamedSource : IStatModifierSource, INamedStatSource
        {
            public readonly List<StatModifier> Modifiers = new List<StatModifier>();

            public NamedSource(string name)
            {
                SourceName = name;
            }

            public string SourceName { get; }

            public IEnumerable<StatModifier> GetModifiers() => Modifiers;
        }

        private readonly NamedSource _levelSource = new NamedSource("레벨");
        private readonly NamedSource _tierSource = new NamedSource("격");
        private readonly Dictionary<StatType, float> _levelBonus = new Dictionary<StatType, float>();
        private ProgressionModel _model;
        private PlayerJob _job;
        private CharacterStats _stats;
        private Health _health;
        private bool _isRegistered;

        public int Level => Model.Level;
        public int Experience => Model.Experience;
        public int ExperienceToNext => Model.ExperienceToNext;
        public float Progress => Model.Progress;
        public bool IsMaxLevel => Model.IsMaxLevel;
        public AscensionTier Tier { get; private set; } = AscensionTier.Mortal;

        /// <summary>무직이고 직업 선택 레벨에 도달함 (직업 선택 화면을 열어야 함)</summary>
        public bool IsJobSelectionPending => _job != null && !_job.HasJob && Level >= _level.JobSelectLevel;

        /// <summary>레벨이 올랐을 때 (새 레벨)</summary>
        public event Action<int> LeveledUp;

        /// <summary>격 돌파했을 때 (새 단계)</summary>
        public event Action<AscensionTier> Ascended;

        /// <summary>직업 선택이 필요해졌을 때</summary>
        public event Action JobSelectionRequested;

        private ProgressionModel Model =>
            _model ??= new ProgressionModel(_level.BaseExperience, _level.Growth, _level.MaxLevel);

        private void Awake()
        {
            _job = GetComponent<PlayerJob>();
            _stats = GetComponent<CharacterStats>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            Health.AnyDied += OnAnyDied;
            _job.JobChanged += OnJobChanged;
        }

        private void OnDisable()
        {
            Health.AnyDied -= OnAnyDied;
            _job.JobChanged -= OnJobChanged;
        }

        private void Start()
        {
            if (_stats != null)
            {
                _stats.Sheet.AddSource(_levelSource);
                _stats.Sheet.AddSource(_tierSource);
                _isRegistered = true;
            }

            RebuildModifiers();
        }

        private void OnDestroy()
        {
            if (_isRegistered && _stats != null)
            {
                _stats.Sheet.RemoveSource(_levelSource);
                _stats.Sheet.RemoveSource(_tierSource);
            }
        }

        private void Update()
        {
            if (_enableDebugKey && Keyboard.current != null && Keyboard.current.f2Key.wasPressedThisFrame)
            {
                AddExperience(Mathf.Max(1, ExperienceToNext - Experience));
            }
        }

        public string DebugInfo => IsMaxLevel
            ? $"레벨 {Level} (최대), 격 {AscensionModel.DisplayName(Tier)}"
            : $"레벨 {Level} (경험치 {Experience} / {ExperienceToNext}), 격 {AscensionModel.DisplayName(Tier)}";

        /// <summary>경험치 획득. 레벨이 오르면 체력 전부 회복, 직업 선택·격 돌파 확인</summary>
        public void AddExperience(int amount)
        {
            int gained = Model.Add(amount);

            if (gained <= 0)
            {
                return;
            }

            // 오른 레벨마다 지금 직업 기준으로 보너스 누적 (이후 전직해도 유지)
            for (int i = 0; i < gained; i++)
            {
                AccumulateLevelBonus();
            }

            RebuildModifiers();

            if (_health != null)
            {
                _health.Heal(_health.Max);
            }

            LeveledUp?.Invoke(Level);

            if (IsJobSelectionPending)
            {
                JobSelectionRequested?.Invoke();
            }

            TryAscend();
            LogStats();
        }

        // 레벨·격 보너스가 모두 반영된 뒤 출력 (돌파 보너스 포함)
        private void LogStats()
        {
            if (_logStatsOnLevelUp && _stats != null)
            {
                _stats.DebugLogStats();
            }
        }

        // 적 처치 경험치 (플레이어가 죽인 것만 따지지 않음: 프로토타입은 적 사망 = 플레이어 처치)
        private void OnAnyDied(Health victim)
        {
            if (victim == null || victim == _health || !victim.TryGetComponent(out EnemyController enemy))
            {
                return;
            }

            if (enemy.Definition != null)
            {
                AddExperience(enemy.Definition.ExperienceReward);
            }
        }

        // 레벨 1회분 보너스: 직업이 있으면 주 스탯, 무직이면 힘·민첩·지능 균등 + 최대 체력
        private void AccumulateLevelBonus()
        {
            StatType? mainStat = _stats != null ? _stats.Sheet.MainStat : null;

            if (mainStat.HasValue)
            {
                AddLevelBonus(mainStat.Value, _level.MainStatPerLevel);
            }
            else
            {
                AddLevelBonus(StatType.Strength, _level.AllStatsPerLevel);
                AddLevelBonus(StatType.Agility, _level.AllStatsPerLevel);
                AddLevelBonus(StatType.Intelligence, _level.AllStatsPerLevel);
            }

            AddLevelBonus(StatType.MaxHealth, _level.MaxHealthPerLevel);
        }

        private void AddLevelBonus(StatType stat, float value)
        {
            _levelBonus.TryGetValue(stat, out float current);
            _levelBonus[stat] = current + value;
        }

        // 전직: 이미 얻은 레벨 보너스는 그대로 두고, 직업 조건이 생겼으니 돌파 확인
        private void OnJobChanged(JobDefinition job)
        {
            RebuildModifiers();
            TryAscend();
        }

        private void TryAscend()
        {
            bool hasJob = _job != null && _job.HasJob;
            AscensionTier? next = AscensionModel.GetNext(Tier, Level, hasJob, _ascension.SuperhumanLevel,
                _ascension.DemigodLevel);

            while (next.HasValue)
            {
                Tier = next.Value;
                RebuildModifiers();
                Ascended?.Invoke(Tier);
                next = AscensionModel.GetNext(Tier, Level, hasJob, _ascension.SuperhumanLevel, _ascension.DemigodLevel);
            }
        }

        private void RebuildModifiers()
        {
            _levelSource.Modifiers.Clear();
            _tierSource.Modifiers.Clear();

            foreach (KeyValuePair<StatType, float> bonus in _levelBonus)
            {
                _levelSource.Modifiers.Add(new StatModifier(bonus.Key, ModifierType.Flat, bonus.Value));
            }

            int tiers = (int)Tier;

            if (tiers > 0)
            {
                _tierSource.Modifiers.Add(new StatModifier(StatType.MaxHealth, ModifierType.Flat,
                    _ascension.MaxHealthPerTier * tiers));
                _tierSource.Modifiers.Add(new StatModifier(StatType.AttackPower, ModifierType.Flat,
                    _ascension.AttackPowerPerTier * tiers));
            }

            if (_isRegistered)
            {
                _stats.Sheet.NotifySourcesChanged();
            }
        }
    }
}
