using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 보스 표시 정보 (상단 보스 체력바 BossHudView가 읽음). 보스 오브젝트에 붙입니다.
    /// 교전 여부는 EnemyController 상태(추적·공격)로 판단합니다. 없으면 체력이 깎인 뒤부터 교전.
    /// 보호막·격 수확 결정은 보스 규칙(10/21 이랑진군)이 SetShield·SetCrystals로 갱신합니다.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Health))]
    public class BossStatus : MonoBehaviour, IInvulnerabilitySource
    {
        private static readonly List<BossStatus> _active = new List<BossStatus>();

        [SerializeField] private string _bossName = "이랑진군";
        [SerializeField] private string _title = "천계의 무신";

        private Health _health;
        private EnemyController _enemy;
        private bool _wasDamaged;

        /// <summary>씬에 있는 보스 목록 (UI 조회용)</summary>
        public static IReadOnlyList<BossStatus> Active => _active;

        public string BossName => _bossName;
        public string Title => _title;
        public Health Health => _health;

        /// <summary>보호막 (격 수확 3단계: 최대 체력 20%)</summary>
        public float Shield { get; private set; }
        public float ShieldMax { get; private set; }

        /// <summary>소환된 격 결정 수와 흡수까지 남은 시간</summary>
        public int ActiveCrystals { get; private set; }
        public float CrystalTimeRemaining { get; private set; }

        /// <summary>흡수한 결정 수 (강화 단계)</summary>
        public int AbsorbedStacks { get; private set; }

        public bool IsDead => _health == null || _health.IsDead;

        /// <summary>등장 연출 중 (10/25): 무적, 체력바 회색 (BossAppearTrigger가 켜고 끔)</summary>
        public bool IsIntro { get; set; }

        /// <summary>IInvulnerabilitySource: 등장 연출 중에는 피해 무시</summary>
        public bool IsInvulnerable => IsIntro;

        public bool IsEngaged
        {
            get
            {
                if (IsDead)
                {
                    return false;
                }

                if (IsIntro)
                {
                    return true;   // 등장 연출 중에도 체력바 표시 (회색)
                }

                if (_enemy != null)
                {
                    return _enemy.State == EnemyState.Chase || _enemy.State == EnemyState.Attack;
                }

                return _wasDamaged;
            }
        }

        /// <summary>보스가 쓰러졌을 때</summary>
        public static event Action<BossStatus> AnyDefeated;

        /// <summary>페이즈 전환 (10/26): 보스, 페이즈 번호(0부터), 페이즈 이름, 부제</summary>
        public static event Action<BossStatus, int, string, string> AnyPhaseChanged;

        /// <summary>현재 페이즈 (0부터)</summary>
        public int Phase { get; private set; }

        /// <summary>다음 페이즈가 시작되는 체력 비율들 (예: 0.7, 0.35) — HUD 체력바 위 빨간 역삼각형 (10/26)</summary>
        public float[] PhaseRatios { get; private set; } = Array.Empty<float>();

        private void Awake()
        {
            _health = GetComponent<Health>();
            _enemy = GetComponent<EnemyController>();
        }

        private void OnEnable()
        {
            _active.Add(this);
            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
            _health.Restored += OnRestored;
        }

        private void OnDisable()
        {
            _active.Remove(this);
            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
            _health.Restored -= OnRestored;
        }

        public void Configure(string bossName, string title)
        {
            _bossName = bossName;
            _title = title;
        }

        public void SetShield(float shield, float shieldMax)
        {
            ShieldMax = Mathf.Max(0f, shieldMax);
            Shield = Mathf.Clamp(shield, 0f, ShieldMax);
        }

        public void SetCrystals(int count, float timeRemaining, int absorbedStacks)
        {
            ActiveCrystals = Mathf.Max(0, count);
            CrystalTimeRemaining = Mathf.Max(0f, timeRemaining);
            AbsorbedStacks = Mathf.Max(0, absorbedStacks);
        }

        private void OnDamaged(DamageInfo info)
        {
            _wasDamaged = true;
        }

        private void OnDied()
        {
            SetShield(0f, 0f);
            SetCrystals(0, 0f, AbsorbedStacks);
            AnyDefeated?.Invoke(this);
        }

        public void SetPhaseRatios(float[] ratios)
        {
            PhaseRatios = ratios ?? Array.Empty<float>();
        }

        /// <summary>ErlangBoss가 페이즈 전환 시 호출 → HUD 배너</summary>
        public void NotifyPhase(int phase, string title, string subtitle)
        {
            Phase = phase;
            AnyPhaseChanged?.Invoke(this, phase, title, subtitle);
        }

        private void OnRestored()
        {
            _wasDamaged = false;
            Phase = 0;
        }
    }
}
