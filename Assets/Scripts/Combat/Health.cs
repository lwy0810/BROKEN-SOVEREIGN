using System;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 체력. 피해 적용, 회복, 사망 판정을 담당합니다.
    /// 같은 오브젝트의 IInvulnerabilitySource가 무적이면 피해를 무시합니다 (회피 무적, GDD 6.1).
    /// CharacterStats가 있으면 최대 체력·생명력 회복·회피율·방어력·원소 저항·피해 감소를 능력치에서 읽습니다.
    /// 피해 처리 순서: 무적 → 회피율 판정 → 속성 감소(방어력·원소 저항, 방어 관통 반영) × 전체 피해 감소
    /// → 받는 피해 배율(IDamageTakenModifier, 무력화 등) → 체력 차감.
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [Tooltip("최대 체력. CharacterStats가 있으면 능력치 값으로 덮어씀")]
        [SerializeField] private float _max = 100f;

        [Tooltip("현재 체력. 0 이하로 시작하면 최대 체력으로 채움. Play 중 변경하면 즉시 반영 (0이 되면 사망 처리)")]
        [SerializeField] private float _current;

        private bool _hasDied;
        private IInvulnerabilitySource[] _invulnerabilitySources;
        private readonly System.Collections.Generic.List<IDamageTakenModifier> _takenModifiers =
            new System.Collections.Generic.List<IDamageTakenModifier>();
        private CharacterStats _stats;

        public float Current => _current;
        public float Max => _max;
        public bool IsDead => _current <= 0f;

        /// <summary>피해가 적용됐을 때</summary>
        public event Action<DamageInfo> Damaged;

        /// <summary>
        /// 모든 Health의 피해 적용 알림 (피해 입은 쪽, 최종 피해). 자원 충전·전투 상태처럼
        /// 여러 대상의 피격을 한곳에서 받아야 하는 시스템용. EventBus 도입 시 교체 예정
        /// </summary>
        public static event Action<Health, DamageInfo> AnyDamaged;

        /// <summary>무적으로 피해를 무시했을 때</summary>
        public event Action<DamageInfo> DamageIgnored;

        /// <summary>회피율 판정으로 피해를 피했을 때</summary>
        public event Action<DamageInfo> Dodged;

        /// <summary>체력이 0이 됐을 때</summary>
        public event Action Died;

        /// <summary>어떤 Health든 사망했을 때 (경험치 획득 등 전역 구독용)</summary>
        public static event Action<Health> AnyDied;

        /// <summary>체력을 최대치로 되돌렸을 때 (부활)</summary>
        public event Action Restored;

        private void Awake()
        {
            _stats = GetComponent<CharacterStats>();

            if (_stats != null)
            {
                _max = Mathf.Max(1f, _stats.Sheet.Get(StatType.MaxHealth));
                _stats.Sheet.Changed += OnStatsChanged;
            }

            _current = _current <= 0f ? _max : Mathf.Min(_current, _max);
            _invulnerabilitySources = GetComponents<IInvulnerabilitySource>();
        }

        private void OnDestroy()
        {
            if (_stats != null)
            {
                _stats.Sheet.Changed -= OnStatsChanged;
            }
        }

        private void Update()
        {
            if (_stats == null || IsDead)
            {
                return;
            }

            float regen = _stats.Sheet.Get(StatType.LifeRegen);

            if (regen > 0f)
            {
                Heal(regen * Time.deltaTime);
            }
        }

        // 최대 체력이 바뀌면 현재 체력 비율 유지
        private void OnStatsChanged()
        {
            float newMax = Mathf.Max(1f, _stats.Sheet.Get(StatType.MaxHealth));

            if (Mathf.Approximately(newMax, _max))
            {
                return;
            }

            float ratio = _max > 0f ? _current / _max : 1f;
            _max = newMax;

            if (!IsDead)
            {
                _current = Mathf.Max(0.01f, ratio * _max);
            }
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (IsDead || info.Amount <= 0f)
            {
                return;
            }

            if (IsInvulnerable())
            {
                DamageIgnored?.Invoke(info);
                return;
            }

            DamageInfo final = info;

            if (_stats != null)
            {
                StatSheet sheet = _stats.Sheet;

                if (UnityEngine.Random.value < sheet.Get(StatType.DodgeChance))
                {
                    Dodged?.Invoke(info);
                    return;
                }

                final = info.WithAmount(DamageCalculator.ApplyMitigation(info.Amount,
                    sheet.GetMitigation(info.Type, info.ArmorPenetration)));
            }

            // 받는 피해 배율 (무력화 +25% 등). 런타임에 붙는 컴포넌트라 매번 조회
            GetComponents(_takenModifiers);

            foreach (IDamageTakenModifier modifier in _takenModifiers)
            {
                final = final.WithAmount(final.Amount * Mathf.Max(0f, modifier.DamageTakenMultiplier));
            }

            _current = Mathf.Max(0f, _current - final.Amount);
            Damaged?.Invoke(final);
            AnyDamaged?.Invoke(this, final);

            if (IsDead)
            {
                RaiseDied();
            }
        }

        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f)
            {
                return;
            }

            _current = Mathf.Min(_max, _current + amount);
        }

        /// <summary>체력을 최대치로 되돌립니다 (부활·재시작).</summary>
        public void RestoreFull()
        {
            _current = _max;
            _hasDied = false;
            Restored?.Invoke();
        }

        // Inspector 값 정리. Play 중 현재 체력을 직접 바꾸면 사망·부활 이벤트도 맞춰 발생
        private void OnValidate()
        {
            _max = Mathf.Max(1f, _max);

            // 편집 모드: 비어 있으면(기존 씬 데이터 포함) 최대 체력으로 표시
            if (!Application.isPlaying)
            {
                _current = _current <= 0f ? _max : Mathf.Min(_current, _max);
                return;
            }

            _current = Mathf.Clamp(_current, 0f, _max);

            if (_invulnerabilitySources == null)
            {
                return;
            }

            if (IsDead && !_hasDied)
            {
                RaiseDied();
            }
            else if (!IsDead && _hasDied)
            {
                _hasDied = false;
                Restored?.Invoke();
            }
        }

        private void RaiseDied()
        {
            _hasDied = true;
            Died?.Invoke();
            AnyDied?.Invoke(this);
        }

        private bool IsInvulnerable()
        {
            foreach (IInvulnerabilitySource source in _invulnerabilitySources)
            {
                if (source.IsInvulnerable)
                {
                    return true;
                }
            }

            return false;
        }

        [ContextMenu("Debug: 피해 100")]
        private void DebugDamage()
        {
            ApplyDamage(new DamageInfo(null, 100f, false));
        }
    }
}
