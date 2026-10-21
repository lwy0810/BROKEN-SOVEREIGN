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
        public int MaxCharges => _maxCharges;
        public float Cooldown => _cooldown;
        public float CooldownRemaining => Mathf.Max(0f, _nextAvailableTime - Time.time);

        private void Awake()
        {
            _charges = _maxCharges;
        }

        /// <summary>물약을 최대치로 채웁니다 (마을 치유사).</summary>
        public void Refill()
        {
            _charges = _maxCharges;
        }

        /// <summary>물약 칸 충전 (가방의 치유 물약 사용). 실제로 채운 수 반환</summary>
        public int AddCharges(int amount)
        {
            int add = Mathf.Clamp(_maxCharges - _charges, 0, Mathf.Max(0, amount));
            _charges += add;
            return add;
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
