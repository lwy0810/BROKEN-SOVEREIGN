using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 물약 사용 횟수 관리. 체력 회복은 Combat(Health) 구현 시 연결합니다.
    /// </summary>
    public class PotionUser : MonoBehaviour
    {
        [SerializeField] private int _maxCharges = 3;
        [SerializeField] private float _cooldown = 1f;

        private int _charges;
        private float _nextAvailableTime;

        public int Charges => _charges;

        private void Awake()
        {
            _charges = _maxCharges;
        }

        public bool TryUse()
        {
            if (_charges <= 0)
            {
                Debug.Log("PotionUser: 남은 물약 없음");
                return false;
            }

            if (Time.time < _nextAvailableTime)
            {
                return false;
            }

            _charges--;
            _nextAvailableTime = Time.time + _cooldown;
            Debug.Log($"PotionUser: 물약 사용, 남은 횟수 {_charges} (체력 회복은 Health 구현 시 연결)");
            return true;
        }
    }
}
