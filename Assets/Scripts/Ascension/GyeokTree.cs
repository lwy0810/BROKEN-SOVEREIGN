using System;
using System.Collections.Generic;
using BrokenSovereign.Achievement;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Hero;
using UnityEngine;

namespace BrokenSovereign.Ascension
{
    /// <summary>신격 트리 노드 모양·등급</summary>
    public enum GyeokNodeKind
    {
        Start,
        Normal,
        Rare,
        Glyph
    }

    /// <summary>신격 트리 가지</summary>
    public enum GyeokBranch
    {
        Trunk,
        Common,
        Hero,
        Demigod
    }

    /// <summary>신격 트리 노드 하나 (Figma 17-B 배치: 시작 노드 기준 격자 좌표, 위가 -y)</summary>
    public sealed class GyeokNode
    {
        public readonly string Id;
        public readonly string Name;
        public readonly GyeokNodeKind Kind;
        public readonly GyeokBranch Branch;
        public readonly Vector2Int Grid;
        public readonly int Cost;
        public readonly StatModifier[] Modifiers;
        /// <summary>주 스탯 보너스 (직업의 주 스탯, 무직이면 힘)</summary>
        public readonly float MainStat;
        public readonly string Extra;
        public readonly List<GyeokNode> Links = new List<GyeokNode>();

        public GyeokNode(string id, string name, GyeokNodeKind kind, GyeokBranch branch, Vector2Int grid, int cost,
            StatModifier[] modifiers, float mainStat = 0f, string extra = null)
        {
            Id = id;
            Name = name;
            Kind = kind;
            Branch = branch;
            Grid = grid;
            Cost = cost;
            Modifiers = modifiers ?? new StatModifier[0];
            MainStat = mainStat;
            Extra = extra;
        }

        public string KindName
        {
            get
            {
                switch (Kind)
                {
                    case GyeokNodeKind.Start: return "시작 노드";
                    case GyeokNodeKind.Rare: return "희귀 노드";
                    case GyeokNodeKind.Glyph: return "문양 노드";
                    default: return "일반 노드";
                }
            }
        }
    }

    /// <summary>
    /// 신격 트리 (10/26, Figma 17-B). 디아블로4 정복자 트리처럼 반신 돌파 시 개방.
    /// 업적 포인트(AchievementTracker.PointsEarned)로 노드를 활성화: 일반·희귀 1, 문양 2.
    /// - 활성 노드와 이어진 노드만 활성화 가능 (시작 노드는 처음부터 활성)
    /// - 비활성화(우클릭)는 남은 노드가 시작 노드와 계속 이어질 때만 (끝 노드부터)
    /// - 영웅의 길 가지는 길을 고른 뒤에만, 길을 바꾸면 영웅 가지 포인트 반환
    /// 노드 효과는 능력치 출처("신격")로 등록합니다.
    /// </summary>
    public class GyeokTree : MonoBehaviour, IStatModifierSource, INamedStatSource
    {
        /// <summary>격의 고동: 공용 가지 활성 노드가 이 수 이상이면 추가 피해 감소</summary>
        public const int PULSE_COMMON_COUNT = 5;
        private const float PULSE_DAMAGE_REDUCTION = 0.04f;

        private readonly List<GyeokNode> _nodes = new List<GyeokNode>();
        private readonly Dictionary<string, GyeokNode> _byId = new Dictionary<string, GyeokNode>();
        private readonly HashSet<GyeokNode> _active = new HashSet<GyeokNode>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        private PlayerProgression _progression;
        private AchievementTracker _achievements;
        private HeroPathController _hero;
        private CharacterStats _stats;

        public string SourceName => "신격";

        public IReadOnlyList<GyeokNode> Nodes => _nodes;

        public GyeokNode StartNode { get; private set; }

        public int PointsSpent { get; private set; }

        public int PointsEarned => _achievements != null ? _achievements.PointsEarned : 0;

        public int PointsAvailable => Mathf.Max(0, PointsEarned - PointsSpent);

        /// <summary>반신 돌파 후 개방</summary>
        public bool IsUnlocked => _progression != null && _progression.Tier >= AscensionTier.Demigod;

        public bool HasHeroPath => _hero != null && _hero.HasPath;

        /// <summary>트리 상태가 바뀌었을 때 (창 갱신)</summary>
        public event Action Changed;

        private void Awake()
        {
            _progression = GetComponent<PlayerProgression>();
            _achievements = GetComponent<AchievementTracker>();
            _hero = GetComponent<HeroPathController>();
            _stats = GetComponent<CharacterStats>();
            BuildNodes();
            _active.Add(StartNode);
        }

        private void OnEnable()
        {
            if (_hero != null)
            {
                _hero.PathSelected += OnPathSelected;
            }
        }

        private void OnDisable()
        {
            if (_hero != null)
            {
                _hero.PathSelected -= OnPathSelected;
            }
        }

        private void Start()
        {
            if (_stats != null)
            {
                _stats.Sheet.AddSource(this);
                _stats.Sheet.Changed += OnSheetChanged;
            }

            Rebuild();
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.Sheet.Changed -= OnSheetChanged;
                _stats.Sheet.RemoveSource(this);
            }
        }

        public IEnumerable<StatModifier> GetModifiers() => _modifiers;

        public bool IsActive(GyeokNode node) => node != null && _active.Contains(node);

        /// <summary>활성 노드 수 (시작 노드 제외, 10/26 업적용)</summary>
        public int ActiveCount => Mathf.Max(0, _active.Count - 1);

        public int CountActive(GyeokBranch branch)
        {
            int count = 0;

            foreach (GyeokNode node in _active)
            {
                if (node.Branch == branch)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>이어진 활성 노드가 있음 (포인트·개방 여부와 무관)</summary>
        public bool IsReachable(GyeokNode node)
        {
            if (node == null || IsActive(node))
            {
                return false;
            }

            foreach (GyeokNode link in node.Links)
            {
                if (_active.Contains(link))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>활성화할 수 없는 이유 (가능하면 null)</summary>
        public string GetBlockReason(GyeokNode node)
        {
            if (node == null) return "노드를 선택하세요";
            if (!IsUnlocked) return "반신 돌파 시 개방";
            if (IsActive(node)) return "이미 활성화됨";
            if (node.Branch == GyeokBranch.Hero && !HasHeroPath) return "영웅의 길을 먼저 선택하세요";
            if (!IsReachable(node)) return "활성 노드와 이어져야 합니다";
            if (PointsAvailable < node.Cost) return "업적 포인트가 부족합니다";
            return null;
        }

        public bool CanActivate(GyeokNode node) => GetBlockReason(node) == null;

        public bool Activate(GyeokNode node)
        {
            if (!CanActivate(node))
            {
                return false;
            }

            _active.Add(node);
            PointsSpent += node.Cost;
            Rebuild();
            return true;
        }

        /// <summary>비활성화 가능: 시작 노드가 아니고, 빼도 나머지가 시작 노드와 이어짐</summary>
        public bool CanDeactivate(GyeokNode node)
        {
            if (node == null || node == StartNode || !IsActive(node))
            {
                return false;
            }

            _active.Remove(node);
            bool connected = AllConnected();
            _active.Add(node);
            return connected;
        }

        public bool Deactivate(GyeokNode node)
        {
            if (!CanDeactivate(node))
            {
                return false;
            }

            _active.Remove(node);
            PointsSpent -= node.Cost;
            Rebuild();
            return true;
        }

        /// <summary>초기화: 시작 노드만 남기고 포인트 전부 반환</summary>
        public void ResetAll()
        {
            _active.Clear();
            _active.Add(StartNode);
            PointsSpent = 0;
            Rebuild();
        }

        /// <summary>노드 효과 설명 (상세 패널)</summary>
        public string Describe(GyeokNode node)
        {
            if (node == null)
            {
                return string.Empty;
            }

            var builder = new System.Text.StringBuilder();

            if (node.MainStat > 0f)
            {
                builder.Append($"주 스탯 +{node.MainStat:0}\n");
            }

            foreach (StatModifier modifier in node.Modifiers)
            {
                builder.Append(DescribeModifier(modifier)).Append('\n');
            }

            if (!string.IsNullOrEmpty(node.Extra))
            {
                builder.Append(node.Extra).Append('\n');
            }

            if (node.Kind == GyeokNodeKind.Start)
            {
                builder.Append("신격 트리의 시작점\n");
            }

            return builder.ToString().TrimEnd('\n');
        }

        public static string StatName(StatType stat)
        {
            switch (stat)
            {
                case StatType.Strength: return "힘";
                case StatType.Agility: return "민첩";
                case StatType.Intelligence: return "지능";
                case StatType.AttackPower: return "공격력";
                case StatType.DamageBonus: return "기술 피해";
                case StatType.AttackSpeed: return "공격 속도";
                case StatType.CritChance: return "치명타 확률";
                case StatType.CritDamage: return "치명타 피해";
                case StatType.MaxHealth: return "최대 체력";
                case StatType.ElementalResistance: return "원소 저항";
                case StatType.DamageReduction: return "피해 감소";
                case StatType.CooldownReduction: return "재사용 대기 감소";
                default: return stat.ToString();
            }
        }

        private static string DescribeModifier(StatModifier modifier)
        {
            bool ratioStat = modifier.Stat == StatType.DamageBonus || modifier.Stat == StatType.AttackSpeed ||
                modifier.Stat == StatType.CritChance || modifier.Stat == StatType.CritDamage ||
                modifier.Stat == StatType.ElementalResistance || modifier.Stat == StatType.DamageReduction ||
                modifier.Stat == StatType.CooldownReduction;

            if (modifier.Type != ModifierType.Flat || ratioStat)
            {
                return $"{StatName(modifier.Stat)} +{modifier.Value * 100f:0.#}%";
            }

            return $"{StatName(modifier.Stat)} +{modifier.Value:0.#}";
        }

        // ------------------------------------------------------------------

        // 영웅의 길을 바꾸면 영웅 가지 노드를 비우고 포인트 반환
        private void OnPathSelected(HeroPathDefinition path)
        {
            bool removed = false;

            foreach (GyeokNode node in _nodes)
            {
                if (node.Branch == GyeokBranch.Hero && _active.Remove(node))
                {
                    PointsSpent -= node.Cost;
                    removed = true;
                }
            }

            if (removed)
            {
                Rebuild();
            }
            else
            {
                Changed?.Invoke();
            }
        }

        // 주 스탯이 바뀌면 (전직) 주 스탯 보너스 대상 갱신
        private StatType? _lastMainStat;

        private void OnSheetChanged()
        {
            if (_stats != null && _stats.Sheet.MainStat != _lastMainStat)
            {
                Rebuild();
            }
        }

        private void Rebuild()
        {
            _modifiers.Clear();
            _lastMainStat = _stats != null ? _stats.Sheet.MainStat : null;
            StatType main = _lastMainStat ?? StatType.Strength;

            foreach (GyeokNode node in _active)
            {
                _modifiers.AddRange(node.Modifiers);

                if (node.MainStat > 0f)
                {
                    _modifiers.Add(new StatModifier(main, ModifierType.Flat, node.MainStat));
                }

                if (node.Id == "t_pulse" && CountActive(GyeokBranch.Common) >= PULSE_COMMON_COUNT)
                {
                    _modifiers.Add(new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, PULSE_DAMAGE_REDUCTION));
                }
            }

            if (_stats != null)
            {
                _stats.Sheet.NotifySourcesChanged();
            }

            Changed?.Invoke();
        }

        private bool AllConnected()
        {
            var visited = new HashSet<GyeokNode> { StartNode };
            var queue = new Queue<GyeokNode>();
            queue.Enqueue(StartNode);

            while (queue.Count > 0)
            {
                foreach (GyeokNode link in queue.Dequeue().Links)
                {
                    if (_active.Contains(link) && visited.Add(link))
                    {
                        queue.Enqueue(link);
                    }
                }
            }

            return visited.Count == _active.Count;
        }

        // ------------------------------------------------------------------
        // 노드 배치 (Figma 17-B)
        // ------------------------------------------------------------------

        private static StatModifier[] M(StatType stat, float value, ModifierType type = ModifierType.Flat)
        {
            return new[] { new StatModifier(stat, type, value) };
        }

        private GyeokNode Add(string id, string name, GyeokNodeKind kind, GyeokBranch branch, int x, int y,
            StatModifier[] modifiers, float mainStat = 0f, string extra = null)
        {
            int cost = kind == GyeokNodeKind.Start ? 0 : kind == GyeokNodeKind.Glyph ? 2 : 1;
            var node = new GyeokNode(id, name, kind, branch, new Vector2Int(x, y), cost, modifiers, mainStat, extra);
            _nodes.Add(node);
            _byId[id] = node;
            return node;
        }

        private void Link(string a, string b)
        {
            GyeokNode na = _byId[a], nb = _byId[b];
            na.Links.Add(nb);
            nb.Links.Add(na);
        }

        private void BuildNodes()
        {
            const GyeokNodeKind N = GyeokNodeKind.Normal, R = GyeokNodeKind.Rare, G = GyeokNodeKind.Glyph;
            const GyeokBranch T = GyeokBranch.Trunk, C = GyeokBranch.Common, H = GyeokBranch.Hero, D = GyeokBranch.Demigod;

            StartNode = Add("start", "시작", GyeokNodeKind.Start, T, 0, 0, null);

            // 줄기
            Add("t1", "격의 숨결", N, T, 0, -1, M(StatType.MaxHealth, 15f));
            Add("t2", "격의 갈림길", N, T, 0, -2, M(StatType.AttackPower, 3f));
            Add("t3", "격의 숨결", N, T, 0, -3, M(StatType.MaxHealth, 15f));
            Add("t_pulse", "격의 고동", R, T, 0, -4,
                new[] { new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.08f), new StatModifier(StatType.DamageBonus, ModifierType.Flat, 0.05f) },
                extra: $"공용 가지 활성 노드 {PULSE_COMMON_COUNT}개 이상: 추가로 피해 감소 +4%");
            Add("t5", "격의 맥동", N, T, 0, -5, M(StatType.AttackPower, 3f));
            Add("t6", "반신의 문턱", N, T, 0, -6, M(StatType.MaxHealth, 15f));

            // 반신 가지
            Add("d1", "신속한 칼끝", N, D, -1, -7, M(StatType.CritChance, 0.02f));
            Add("d2", "벼려진 일격", N, D, 1, -7, M(StatType.CritDamage, 0.1f));
            Add("d3", "질풍", N, D, -2, -8, M(StatType.AttackSpeed, 0.03f));
            Add("d4", "끊이지 않는 기도", N, D, 2, -8, M(StatType.CooldownReduction, 0.03f, ModifierType.Multiplicative));
            Add("g_demi", "반신의 각성", G, D, 0, -8,
                new[] { new StatModifier(StatType.DamageBonus, ModifierType.Flat, 0.1f), new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.05f) });

            // 공용 · 격의 힘
            Add("c1", "격의 힘", N, C, -1, -2, null, 5f);
            Add("c_body", "굳건한 신체", R, C, -2, -2,
                new[] { new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.1f), new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, 0.03f) });
            Add("c3", "격의 힘", N, C, -2, -3, null, 5f);
            Add("c4", "단련된 육신", N, C, -3, -3, M(StatType.MaxHealth, 15f));
            Add("c5", "격의 힘", N, C, -4, -3, null, 5f);
            Add("c_grace", "신의 가호", R, C, -4, -4,
                new[] { new StatModifier(StatType.ElementalResistance, ModifierType.Flat, 0.05f), new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, 0.03f) });
            Add("c7", "단련된 육신", N, C, -3, -5, M(StatType.MaxHealth, 15f));
            Add("c8", "격의 힘", N, C, -4, -6, null, 5f);
            Add("g_seal", "인장 소켓", G, C, -5, -6, M(StatType.AttackPower, 5f), 15f);

            // 영웅의 길
            Add("h1", "영웅의 발걸음", N, H, 1, -2, M(StatType.AttackPower, 3f));
            Add("h2", "영웅의 숨", N, H, 2, -2, M(StatType.MaxHealth, 15f));
            Add("h3", "영웅의 발걸음", N, H, 3, -2, M(StatType.AttackPower, 3f));
            Add("h4", "날 선 의지", N, H, 4, -3, M(StatType.CritChance, 0.02f));
            Add("h_vein", "분쇄의 맥", R, H, 4, -4, M(StatType.DamageBonus, 0.08f));
            Add("h6", "거센 손놀림", N, H, 5, -5, M(StatType.AttackSpeed, 0.03f));
            Add("h7", "깊은 상처", N, H, 4, -6, M(StatType.CritDamage, 0.1f));
            Add("h8", "영웅의 힘", N, H, 5, -6, null, 5f);
            Add("g_oath", "무쇠의 서약", G, H, 6, -7,
                new[] { new StatModifier(StatType.DamageReduction, ModifierType.Multiplicative, 0.05f), new StatModifier(StatType.MaxHealth, ModifierType.Percent, 0.05f) });

            Link("start", "t1"); Link("t1", "t2"); Link("t2", "t3"); Link("t3", "t_pulse"); Link("t_pulse", "t5"); Link("t5", "t6");
            Link("t6", "d1"); Link("t6", "d2"); Link("d1", "d3"); Link("d1", "g_demi"); Link("d2", "g_demi"); Link("d2", "d4");
            Link("t2", "c1"); Link("c1", "c_body"); Link("c_body", "c3"); Link("c3", "c4"); Link("c4", "c5"); Link("c4", "c_grace");
            Link("c_grace", "c7"); Link("c7", "c8"); Link("c8", "g_seal");
            Link("t2", "h1"); Link("h1", "h2"); Link("h2", "h3"); Link("h3", "h4"); Link("h4", "h_vein"); Link("h_vein", "h6");
            Link("h_vein", "h7"); Link("h6", "h8"); Link("h7", "h8"); Link("h8", "g_oath");
        }
    }
}
