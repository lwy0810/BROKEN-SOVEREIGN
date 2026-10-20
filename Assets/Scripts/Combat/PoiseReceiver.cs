using System;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 적의 버팀 게이지·무력화 상태 (야로미르 분쇄 규칙이 처음 피해를 준 적에게 붙임).
    /// 게이지가 0이 되면 IStunnable로 행동 불능을 걸고, 무력화 중 받는 피해를 늘립니다 (IDamageTakenModifier).
    /// 정예·보스만 대상 프레임에 버팀 막대를 표시합니다 (ShowBar).
    /// </summary>
    [DisallowMultipleComponent]
    public class PoiseReceiver : MonoBehaviour, IDamageTakenModifier, IStaggerable
    {
        private PoiseGauge _gauge = new PoiseGauge(30f);
        private float _staggerDuration = 3f;
        private float _damageTakenBonus = 0.25f;
        private IStunnable _stunnable;
        private Health _health;

        public float Ratio => _gauge.Ratio;
        public bool ShowBar { get; private set; }
        public bool IsStaggered => _gauge.IsStaggered(Time.time);
        public bool IsImmune => _gauge.IsImmune(Time.time);

        public float DamageTakenMultiplier => IsStaggered ? 1f + _damageTakenBonus : 1f;

        /// <summary>무력화됐을 때 (무력화 시간)</summary>
        public event Action<float> Staggered;

        private void Awake()
        {
            _stunnable = GetComponent<IStunnable>();
            _health = GetComponent<Health>();
        }

        public void Configure(float max, float staggerDuration, float damageTakenBonus, bool showBar,
            float recoverDelay, float recoverRatio, float immunityDuration)
        {
            _gauge = new PoiseGauge(max, recoverDelay, recoverRatio, immunityDuration);
            _staggerDuration = staggerDuration;
            _damageTakenBonus = damageTakenBonus;
            ShowBar = showBar;
        }

        private void Update()
        {
            _gauge.Tick(Time.time, Time.deltaTime);
        }

        /// <summary>버팀 게이지 감소 (피해 계수 × 10 등)</summary>
        public void Hit(float amount)
        {
            if (_gauge.Reduce(amount, Time.time, _staggerDuration))
            {
                OnStaggered();
            }
        }

        public void ReduceRatio(float ratio)
        {
            if (_gauge.ReduceRatio(ratio, Time.time, _staggerDuration))
            {
                OnStaggered();
            }
        }

        public void EndStagger()
        {
            if (!IsStaggered)
            {
                return;
            }

            _gauge.EndStagger(Time.time);
            _stunnable?.EndStun();
        }

        private void OnStaggered()
        {
            if (_health != null && _health.IsDead)
            {
                return;
            }

            _stunnable?.Stun(_staggerDuration);
            Debug.Log($"{name}: 무력화 ({_staggerDuration:0.0}초)");
            Staggered?.Invoke(_staggerDuration);
        }
    }
}
