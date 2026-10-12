using System;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 체력. 피해 적용, 회복, 사망 판정을 담당합니다.
    /// 같은 오브젝트의 IInvulnerabilitySource가 무적이면 피해를 무시합니다 (회피 무적, GDD 6.1).
    /// </summary>
    [DisallowMultipleComponent]
    public class Health : MonoBehaviour, IDamageable
    {
        [SerializeField] private float _max = 100f;

        private float _current;
        private IInvulnerabilitySource[] _invulnerabilitySources;

        public float Current => _current;
        public float Max => _max;
        public bool IsDead => _current <= 0f;

        /// <summary>피해가 적용됐을 때</summary>
        public event Action<DamageInfo> Damaged;

        /// <summary>무적으로 피해를 무시했을 때</summary>
        public event Action<DamageInfo> DamageIgnored;

        /// <summary>체력이 0이 됐을 때</summary>
        public event Action Died;

        /// <summary>체력을 최대치로 되돌렸을 때 (부활)</summary>
        public event Action Restored;

        private void Awake()
        {
            _current = _max;
            _invulnerabilitySources = GetComponents<IInvulnerabilitySource>();
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

            _current = Mathf.Max(0f, _current - info.Amount);
            Damaged?.Invoke(info);

            if (IsDead)
            {
                Died?.Invoke();
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
            Restored?.Invoke();
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
