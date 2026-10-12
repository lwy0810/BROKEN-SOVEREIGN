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

        [Tooltip("현재 체력. 0 이하로 시작하면 최대 체력으로 채움. Play 중 변경하면 즉시 반영 (0이 되면 사망 처리)")]
        [SerializeField] private float _current;

        private bool _hasDied;
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
            _current = _current <= 0f ? _max : Mathf.Min(_current, _max);
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
