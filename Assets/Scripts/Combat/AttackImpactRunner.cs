using System;
using System.Collections;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 지연 타격 실행기 (10/26): 공격 시작 후 일정 시간 뒤 피해를 적용. BasicAttack이 공격자에 자동으로 붙입니다.
    /// 한 번에 하나의 타격만 대기 (새 공격·취소 시 이전 대기 타격은 버림).
    /// </summary>
    [DisallowMultipleComponent]
    public class AttackImpactRunner : MonoBehaviour
    {
        private Coroutine _pending;

        /// <summary>대기 중인 타격이 있음</summary>
        public bool IsPending => _pending != null;

        public static AttackImpactRunner For(Transform attacker)
        {
            return attacker.TryGetComponent(out AttackImpactRunner runner)
                ? runner
                : attacker.gameObject.AddComponent<AttackImpactRunner>();
        }

        public void Schedule(float delay, Action strike)
        {
            Cancel();

            if (delay <= 0f || !isActiveAndEnabled)
            {
                strike();
                return;
            }

            _pending = StartCoroutine(Run(delay, strike));
        }

        public void Cancel()
        {
            if (_pending != null)
            {
                StopCoroutine(_pending);
                _pending = null;
            }
        }

        private IEnumerator Run(float delay, Action strike)
        {
            yield return new WaitForSeconds(delay);
            _pending = null;
            strike();
        }

        private void OnDisable()
        {
            Cancel();
        }
    }
}
