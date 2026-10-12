using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 커서 방향 회피. 짧은 시간 동안 일정 거리를 돌진합니다.
    /// 회피 지속 시간 전체 동안 무적입니다 (GDD 6.1).
    /// </summary>
    [RequireComponent(typeof(ClickMover))]
    public class DodgeAction : MonoBehaviour, IInvulnerabilitySource
    {
        [SerializeField] private float _distance = 4f;
        [SerializeField] private float _duration = 0.2f;
        [SerializeField] private float _cooldown = 1f;

        private ClickMover _mover;
        private Vector3 _direction;
        private float _remainingTime;
        private float _nextAvailableTime;

        public bool IsDodging => _remainingTime > 0f;
        public bool IsInvulnerable => IsDodging;
        public Vector3 Direction => _direction;
        public float CooldownRemaining => Mathf.Max(0f, _nextAvailableTime - Time.time);
        public float Cooldown => _cooldown;

        private void Awake()
        {
            _mover = GetComponent<ClickMover>();
        }

        private void Update()
        {
            if (!IsDodging)
            {
                return;
            }

            float deltaTime = Mathf.Min(Time.deltaTime, _remainingTime);
            _mover.MoveBy(_direction * (_distance / _duration) * deltaTime);
            _remainingTime -= deltaTime;
        }

        public bool TryDodge(Vector3 direction)
        {
            direction.y = 0f;

            if (IsDodging || Time.time < _nextAvailableTime || direction.sqrMagnitude < 0.0001f)
            {
                return false;
            }

            _direction = direction.normalized;
            _remainingTime = _duration;
            _nextAvailableTime = Time.time + _cooldown;

            _mover.Stop();
            _mover.FaceTowards(transform.position + _direction);
            Debug.Log($"DodgeAction: 회피 {_direction}");
            return true;
        }
    }
}
