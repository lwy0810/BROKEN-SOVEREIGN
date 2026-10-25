using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 고정 각도 쿼터뷰 카메라. 대상을 부드럽게 따라갑니다.
    /// 마우스 휠로 줌 인·아웃 (거리 _minDistance~_maxDistance, 10/21). 창·지도가 열려 입력이 막혀 있으면 줌하지 않습니다.
    /// </summary>
    public class QuarterViewCamera : MonoBehaviour
    {
        [SerializeField] private Transform _target;
        [SerializeField] private float _distance = 18f;
        [SerializeField] private float _pitch = 50f;
        [SerializeField] private float _yaw = 45f;
        [SerializeField] private float _followSmoothTime = 0.12f;

        [Header("줌 (마우스 휠)")]
        [Tooltip("가장 가까운 거리")]
        [SerializeField] private float _minDistance = 10f;

        [Tooltip("가장 먼 거리")]
        [SerializeField] private float _maxDistance = 28f;

        [Tooltip("휠 한 칸당 거리 변화")]
        [SerializeField] private float _zoomStep = 2f;

        [Tooltip("줌 따라가는 빠르기 (클수록 빠름)")]
        [SerializeField] private float _zoomSmooth = 10f;

        private Vector3 _velocity;
        private float _targetDistance;

        private void Start()
        {
            _distance = Mathf.Clamp(_distance, _minDistance, _maxDistance);
            _targetDistance = _distance;

            if (_target != null)
            {
                transform.SetPositionAndRotation(GetDesiredPosition(), GetRotation());
            }
        }

        private void Update()
        {
            Mouse mouse = Mouse.current;

            if (mouse == null || GameplayInput.IsBlocked)
            {
                return;
            }

            float wheel = mouse.scroll.ReadValue().y;

            if (Mathf.Abs(wheel) > 0.01f)
            {
                // 휠 위 = 가까이 (줌 인)
                _targetDistance = Mathf.Clamp(_targetDistance - Mathf.Sign(wheel) * _zoomStep, _minDistance, _maxDistance);
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                return;
            }

            _distance = Mathf.Lerp(_distance, _targetDistance, 1f - Mathf.Exp(-_zoomSmooth * Time.unscaledDeltaTime));
            transform.position = Vector3.SmoothDamp(transform.position, GetDesiredPosition(), ref _velocity, _followSmoothTime);
            transform.rotation = GetRotation();
        }

        public void SetTarget(Transform target)
        {
            _target = target;
        }

        /// <summary>대상 위치로 즉시 이동 (웨이포인트 순간 이동 후 따라오는 지연 없이)</summary>
        public void Snap()
        {
            if (_target == null)
            {
                return;
            }

            _velocity = Vector3.zero;
            transform.position = GetDesiredPosition();
            transform.rotation = GetRotation();
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
