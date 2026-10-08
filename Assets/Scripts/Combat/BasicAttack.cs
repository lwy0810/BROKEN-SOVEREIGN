using System;
using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기본 공격 (GDD 6.1). 사거리와 공격 간격만 관리하고, 피해 판정은 Health 구현 시 연결합니다.
    /// 원거리 기본 공격이 필요하면 Range를 늘리거나 ITargetedAction을 구현한 별도 클래스로 교체합니다.
    /// </summary>
    [Serializable]
    public class BasicAttack : ITargetedAction
    {
        [SerializeField] private float _range = 2f;
        [SerializeField] private float _interval = 0.6f;

        private float _nextReadyTime;

        public float Range => _range;
        public float Duration => _interval;
        public bool IsReady => Time.time >= _nextReadyTime;

        public bool TryExecute(Transform target, Vector3 point)
        {
            if (!IsReady)
            {
                return false;
            }

            _nextReadyTime = Time.time + _interval;

            string targetName = target != null ? target.name : "제자리";
            Debug.Log($"BasicAttack: 기본 공격 → {targetName} (피해 판정은 Health 구현 시 연결)");
            return true;
        }
    }
}
