using System;
using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using BrokenSovereign.Hero;
using BrokenSovereign.Item;
using BrokenSovereign.Player;
using BrokenSovereign.Skill;
using BrokenSovereign.Town;
using UnityEngine;

namespace BrokenSovereign.Achievement
{
    /// <summary>
    /// 업적 진행 추적 (10/26). 플레이어에 붙음 (HudView가 없으면 추가).
    /// 처치·정예·보스·페이즈·웨이포인트·레벨·격·영웅의 길·회피·물약·사망을 세고, 목표에 닿으면 달성 처리.
    /// 달성 보상: 업적 포인트(신격 트리용) + 스탯 보너스(능력치 출처 "업적") + 칭호.
    /// 저장은 하지 않음 (프로토타입: 플레이 세션 동안 유지).
    /// 10/26: 시작 상태(시작 레벨·처음부터 열린 웨이포인트)는 세지 않고 플레이 중 이벤트로만 진행.
    ///   추가 집계: 기술 사용 · 치명타 · 최대 일격 · 금화 · 영웅/전설 아이템 · 신격 노드 · 체력 절반 이상 보스 처치
    /// </summary>
    public class AchievementTracker : MonoBehaviour, IStatModifierSource, INamedStatSource
    {
        private const int RECENT_COUNT = 4;

        /// <summary>업적 하나의 진행 상태</summary>
        public sealed class Entry
        {
            public AchievementDefinition Definition;
            public int Progress;
            public bool IsDone;
            public int DoneOrder;
            public int Goal;

            public float Ratio => Goal > 0 ? Mathf.Clamp01((float)Progress / Goal) : (IsDone ? 1f : 0f);
        }

        private readonly List<Entry> _entries = new List<Entry>();
        private readonly Dictionary<AchievementCounter, int> _counters = new Dictionary<AchievementCounter, int>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private readonly List<Entry> _recent = new List<Entry>();

        private Health _health;
        private PlayerProgression _progression;
        private HeroPathController _hero;
        private PlayerController _player;
        private CharacterStats _stats;
        private SkillCaster _caster;
        private Inventory _inventory;
        private GyeokTree _gyeok;
        private int _doneCount;

        public string SourceName => "업적";

        public IReadOnlyList<Entry> Entries => _entries;

        /// <summary>최근 달성 (최신 먼저, 최대 4개)</summary>
        public IReadOnlyList<Entry> Recent => _recent;

        public int DoneCount => _doneCount;

        public int PointsEarned { get; private set; }

        /// <summary>마지막으로 얻은 칭호 (없으면 빈 문자열)</summary>
        public string ActiveTitle { get; private set; } = string.Empty;

        /// <summary>업적을 달성했을 때</summary>
        public event Action<AchievementDefinition> Completed;

        /// <summary>진행 수치가 바뀌었을 때 (창 갱신)</summary>
        public event Action Changed;

        private void Awake()
        {
            _health = GetComponent<Health>();
            _progression = GetComponent<PlayerProgression>();
            _hero = GetComponent<HeroPathController>();
            _player = GetComponent<PlayerController>();
            _stats = GetComponent<CharacterStats>();
            _caster = GetComponent<SkillCaster>();
            _inventory = GetComponent<Inventory>();
            _gyeok = GetComponent<GyeokTree>();

            foreach (AchievementDefinition definition in AchievementCatalog.All)
            {
                _entries.Add(new Entry { Definition = definition, Goal = definition.Goal });
            }
        }

        private void OnEnable()
        {
            Health.AnyDied += OnAnyDied;
            BossStatus.AnyDefeated += OnBossDefeated;
            BossStatus.AnyPhaseChanged += OnBossPhase;
            Waypoint.AnyDiscovered += OnWaypointDiscovered;
            Health.AnyDamaged += OnAnyDamaged;

            if (_caster != null)
            {
                _caster.SkillCast += OnSkillCast;
            }

            if (_inventory != null)
            {
                _inventory.ItemAcquired += OnItemAcquired;
                _inventory.GoldAcquired += OnGoldAcquired;
            }

            if (_gyeok != null)
            {
                _gyeok.Changed += OnGyeokChanged;
            }

            if (_progression != null)
            {
                _progression.LeveledUp += OnLeveledUp;
                _progression.Ascended += OnAscended;
            }

            if (_hero != null)
            {
                _hero.PathSelected += OnPathSelected;
            }

            if (_player != null)
            {
                _player.Dodged += OnDodged;
                _player.PotionUsed += OnPotionUsed;
            }
        }

        private void OnDisable()
        {
            Health.AnyDied -= OnAnyDied;
            BossStatus.AnyDefeated -= OnBossDefeated;
            BossStatus.AnyPhaseChanged -= OnBossPhase;
            Waypoint.AnyDiscovered -= OnWaypointDiscovered;
            Health.AnyDamaged -= OnAnyDamaged;

            if (_caster != null)
            {
                _caster.SkillCast -= OnSkillCast;
            }

            if (_inventory != null)
            {
                _inventory.ItemAcquired -= OnItemAcquired;
                _inventory.GoldAcquired -= OnGoldAcquired;
            }

            if (_gyeok != null)
            {
                _gyeok.Changed -= OnGyeokChanged;
            }

            if (_progression != null)
            {
                _progression.LeveledUp -= OnLeveledUp;
                _progression.Ascended -= OnAscended;
            }

            if (_hero != null)
            {
                _hero.PathSelected -= OnPathSelected;
            }

            if (_player != null)
            {
                _player.Dodged -= OnDodged;
                _player.PotionUsed -= OnPotionUsed;
            }
        }

        private void Start()
        {
            if (_stats != null)
            {
                _stats.Sheet.AddSource(this);
            }

            // 10/26: 시작 시점의 레벨·격은 세지 않음 (레벨업·돌파 이벤트로만 진행).
            // 웨이포인트는 처음부터 열린 곳을 빼고 셈
            RefreshWaypoints();
            Evaluate();
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.Sheet.RemoveSource(this);
            }
        }

        public IEnumerable<StatModifier> GetModifiers() => _modifiers;

        public int GetCounter(AchievementCounter counter)
        {
            return _counters.TryGetValue(counter, out int value) ? value : 0;
        }

        /// <summary>분류별 (달성, 전체). category가 null이면 전체</summary>
        public (int done, int total) Count(AchievementCategory? category)
        {
            int done = 0, total = 0;

            foreach (Entry entry in _entries)
            {
                if (category.HasValue && entry.Definition.Category != category.Value)
                {
                    continue;
                }

                total++;

                if (entry.IsDone)
                {
                    done++;
                }
            }

            return (done, total);
        }

        // ------------------------------------------------------------------
        // 이벤트
        // ------------------------------------------------------------------

        private void OnAnyDied(Health victim)
        {
            if (victim == null)
            {
                return;
            }

            if (victim == _health)
            {
                Add(AchievementCounter.Deaths, 1);
                return;
            }

            if (!victim.TryGetComponent(out EnemyController enemy))
            {
                return;
            }

            Add(AchievementCounter.Kills, 1, false);

            if (enemy.Definition != null && enemy.Definition.Rank == EnemyRank.Elite)
            {
                Add(AchievementCounter.EliteKills, 1, false);
            }

            Evaluate();
        }

        private void OnBossDefeated(BossStatus boss)
        {
            if (_health != null && _health.Max > 0f && _health.Current >= _health.Max * 0.5f)
            {
                Add(AchievementCounter.BossKillsHealthy, 1, false);
            }

            Add(AchievementCounter.BossKills, 1);
        }

        // 플레이어가 준 피해: 치명타 횟수 · 가장 큰 일격
        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            if (victim == null || victim == _health || info.Attacker != gameObject)
            {
                return;
            }

            if (info.IsCritical)
            {
                Add(AchievementCounter.Crits, 1, false);
            }

            SetMax(AchievementCounter.BiggestHit, Mathf.RoundToInt(info.Amount));
            Evaluate();
        }

        private void OnSkillCast(SkillSlot slot, SkillDefinition skill)
        {
            Add(AchievementCounter.SkillCasts, 1);
        }

        private void OnItemAcquired(ItemDefinition item, int count)
        {
            if (item == null || item.Rarity < ItemRarity.Epic)
            {
                return;
            }

            Add(AchievementCounter.EpicItems, count, false);

            if (item.Rarity == ItemRarity.Legendary)
            {
                Add(AchievementCounter.LegendaryItems, count, false);
            }

            Evaluate();
        }

        private void OnGoldAcquired(int amount)
        {
            if (amount > 0)
            {
                Add(AchievementCounter.GoldEarned, amount);
            }
        }

        private void OnGyeokChanged()
        {
            SetMax(AchievementCounter.GyeokNodes, _gyeok.ActiveCount);
            Evaluate();
        }

        // phase는 0부터 (1 = 두 번째 페이즈) → 페이즈 번호로 저장
        private void OnBossPhase(BossStatus boss, int phase, string title, string subtitle)
        {
            SetMax(AchievementCounter.BossPhase, phase + 1);
            Evaluate();
        }

        private void OnWaypointDiscovered(Waypoint waypoint)
        {
            RefreshWaypoints();
            Evaluate();
        }

        private void OnLeveledUp(int level)
        {
            SetMax(AchievementCounter.Level, level);
            Evaluate();
        }

        private void OnAscended(AscensionTier tier)
        {
            SetMax(AchievementCounter.Tier, (int)tier);
            Evaluate();
        }

        private void OnPathSelected(HeroPathDefinition path)
        {
            SetMax(AchievementCounter.HeroPath, 1);
            Evaluate();
        }

        private void OnDodged(Vector3 direction)
        {
            Add(AchievementCounter.Dodges, 1);
        }

        private void OnPotionUsed(int charges)
        {
            Add(AchievementCounter.Potions, 1);
        }

        // ------------------------------------------------------------------
        // 진행 · 달성
        // ------------------------------------------------------------------

        private void RefreshWaypoints()
        {
            int discovered = 0;

            foreach (Waypoint waypoint in Waypoint.All)
            {
                if (waypoint != null && waypoint.IsDiscovered && !waypoint.DiscoveredAtStart)
                {
                    discovered++;
                }
            }

            _counters[AchievementCounter.WaypointsDiscovered] = discovered;
            _counters[AchievementCounter.AllWaypoints] = discovered;
        }

        private static int CountFieldWaypoints()
        {
            int count = 0;

            foreach (Waypoint waypoint in Waypoint.All)
            {
                if (waypoint != null && !waypoint.DiscoveredAtStart)
                {
                    count++;
                }
            }

            return count;
        }

        private void Add(AchievementCounter counter, int amount, bool evaluate = true)
        {
            _counters[counter] = GetCounter(counter) + amount;

            if (evaluate)
            {
                Evaluate();
            }
        }

        private void SetMax(AchievementCounter counter, int value)
        {
            _counters[counter] = Mathf.Max(GetCounter(counter), value);
        }

        private void Evaluate()
        {
            bool statsChanged = false;

            foreach (Entry entry in _entries)
            {
                if (entry.IsDone)
                {
                    continue;
                }

                AchievementDefinition definition = entry.Definition;

                // 웨이포인트 전부: 처음부터 열린 곳을 뺀 씬의 웨이포인트 수가 목표 (없으면 진행 안 함)
                if (definition.Counter == AchievementCounter.AllWaypoints)
                {
                    entry.Goal = CountFieldWaypoints();
                }

                entry.Progress = Mathf.Min(GetCounter(definition.Counter), Mathf.Max(entry.Goal, 0));

                if (entry.Goal > 0 && entry.Progress >= entry.Goal)
                {
                    Complete(entry);
                    statsChanged |= definition.Rewards.Length > 0;
                }
            }

            if (statsChanged && _stats != null)
            {
                _stats.Sheet.NotifySourcesChanged();
            }

            Changed?.Invoke();
        }

        private void Complete(Entry entry)
        {
            AchievementDefinition definition = entry.Definition;
            entry.IsDone = true;
            entry.DoneOrder = ++_doneCount;
            PointsEarned += definition.Points;
            _modifiers.AddRange(definition.Rewards);

            if (!string.IsNullOrEmpty(definition.Title))
            {
                ActiveTitle = definition.Title;
            }

            _recent.Insert(0, entry);

            if (_recent.Count > RECENT_COUNT)
            {
                _recent.RemoveAt(_recent.Count - 1);
            }

            Debug.Log($"업적 달성: {definition.Name} (+{definition.Points})");
            Completed?.Invoke(definition);
        }
    }
}
