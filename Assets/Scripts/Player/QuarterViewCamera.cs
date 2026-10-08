using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 고정 각도 쿼터뷰 카메라. 대상을 부드럽게 따라갑니다.
    /// </summary>
    public class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 18f;
        [SerializeField] private float _pitch = 50f;
        [SerializeField] private float _yaw = 45f;
        [SerializeField] private float _followSmoothTime = 0.12f;

        private Vector3 _velocity;

        private void Start()
        {
            if (_target != null)
            {
                transform.SetPositionAndRotation(GetDesiredPosition(), GetRotation());
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity, _followSmoothTime);
            transform.rotation = GetRotation();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        private Quaternion GetRotation()
        {
            return Quaternion.Euler(_pitch, _yaw, 0f);
        }

        private Vector3 GetDesiredPosition()
        {
            return _target.position - GetRotation() * Vector3.forward * _distance;
        }
    }
}
