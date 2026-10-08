using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// Gameplay 입력 맵을 읽어 커서 아래 대상(바닥·적)을 판별하고 PlayerController에 전달합니다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerInputHandler : MonoBehaviour
    {
        private const string MAP_NAME = "Gameplay";
        private const float RAY_DISTANCE = 500f;

        [SerializeField] private InputActionAsset _inputActions;
        [SerializeField] private Camera _camera;
        [SerializeField] private LayerMask _groundMask;
        [SerializeField] private LayerMask _enemyMask;

        private PlayerController _controller;
        private InputActionMap _map;
        private InputAction _point;
        private InputAction _primary;
        private InputAction _secondary;
        private InputAction _standStill;
        private InputAction _dodge;
        private InputAction _potion;
        private InputAction[] _skillKeys;

        private bool _isPrimaryHeld;
        private bool _isSecondaryHeld;
        private Transform _lockedTarget;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();

            if (_camera == null)
            {
                _camera = Camera.main;
            }

            _map = _inputActions.FindActionMap(MAP_NAME, true);
            _point = _map.FindAction("Point", true);
            _primary = _map.FindAction("Primary", true);
            _secondary = _map.FindAction("Secondary", true);
            _standStill = _map.FindAction("StandStill", true);
            _dodge = _map.FindAction("Dodge", true);
            _potion = _map.FindAction("Potion", true);
            _skillKeys = new[]
            {
                _map.FindAction("Skill1", true),
                _map.FindAction("Skill2", true),
                _map.FindAction("Skill3", true),
                _map.FindAction("Skill4", true)
            };
        }

        private void OnEnable()
        {
            _primary.started += OnPrimaryStarted;
            _primary.canceled += OnPrimaryCanceled;
            _secondary.started += OnSecondaryStarted;
            _secondary.canceled += OnSecondaryCanceled;
            _dodge.performed += OnDodgePerformed;
            _potion.performed += OnPotionPerformed;

            foreach (InputAction skillKey in _skillKeys)
            {
                skillKey.performed += OnSkillKeyPerformed;
            }

            _map.Enable();
        }

        private void OnDisable()
        {
            _primary.started -= OnPrimaryStarted;
            _primary.canceled -= OnPrimaryCanceled;
            _secondary.started -= OnSecondaryStarted;
            _secondary.canceled -= OnSecondaryCanceled;
            _dodge.performed -= OnDodgePerformed;
            _potion.performed -= OnPotionPerformed;

            foreach (InputAction skillKey in _skillKeys)
            {
                skillKey.performed -= OnSkillKeyPerformed;
            }

            _map.Disable();
            _isPrimaryHeld = false;
            _isSecondaryHeld = false;
            _lockedTarget = null;
        }

        private void Update()
        {
            // 우클릭 유지가 좌클릭 유지보다 우선
            if (_isSecondaryHeld)
            {
                HandleSecondaryHeld();
            }
            else if (_isPrimaryHeld)
            {
                HandlePrimaryHeld();
            }
        }

        private void OnPrimaryStarted(InputAction.CallbackContext context)
        {
            _isPrimaryHeld = true;
            _lockedTarget = null;

            // 누르기 시작한 순간 적을 가리키고 있으면, 떼기 전까지 그 적을 대상으로 고정
            if (!_standStill.IsPressed() && TryGetEnemyUnderCursor(out Transform enemy))
            {
                _lockedTarget = enemy;
            }

            HandlePrimaryHeld();
        }

        private void OnPrimaryCanceled(InputAction.CallbackContext context)
        {
            _isPrimaryHeld = false;
            _lockedTarget = null;
        }

        private void HandlePrimaryHeld()
        {
            if (_lockedTarget != null)
            {
                _controller.AttackTarget(_lockedTarget);
                return;
            }

            Vector3 point = GetCursorGroundPoint();

            if (_standStill.IsPressed())
            {
                _controller.AttackInPlace(point);
            }
            else
            {
                _controller.MoveTo(point);
            }
        }

        private void OnSecondaryStarted(InputAction.CallbackContext context)
        {
            _isSecondaryHeld = true;
            HandleSecondaryHeld();
        }

        private void OnSecondaryCanceled(InputAction.CallbackContext context)
        {
            _isSecondaryHeld = false;
            _controller.StopMoving();
        }

        /// <summary>
        /// 우클릭 유지: 커서 방향으로 이동하며 주력 기술 반복 시전. Shift를 누르고 있으면 제자리 시전.
        /// </summary>
        private void HandleSecondaryHeld()
        {
            _controller.CastWhileHeld(SkillSlot.RightClick, GetCursorGroundPoint(), !_standStill.IsPressed());
        }

        private void OnSkillKeyPerformed(InputAction.CallbackContext context)
        {
            int index = System.Array.IndexOf(_skillKeys, context.action);

            if (index < 0)
            {
                return;
            }

            _controller.UseSkill(SkillSlot.Key1 + index, GetCursorGroundPoint());
        }

        private void OnDodgePerformed(InputAction.CallbackContext context)
        {
            _controller.Dodge(GetCursorGroundPoint());
        }

        private void OnPotionPerformed(InputAction.CallbackContext context)
        {
            _controller.UsePotion();
        }

        private Ray GetCursorRay()
        {
            return _camera.ScreenPointToRay(_point.ReadValue<Vector2>());
        }

        private bool TryGetEnemyUnderCursor(out Transform enemy)
        {
            enemy = null;

            if (!Physics.Raycast(GetCursorRay(), out RaycastHit hit, RAY_DISTANCE, _groundMask | _enemyMask))
            {
                return false;
            }

            if ((_enemyMask.value & (1 << hit.collider.gameObject.layer)) == 0)
            {
                return false;
            }

            enemy = hit.collider.transform;
            return true;
        }

        private Vector3 GetCursorGroundPoint()
        {
            Ray ray = GetCursorRay();

            if (Physics.Raycast(ray, out RaycastHit hit, RAY_DISTANCE, _groundMask))
            {
                return hit.point;
            }

            // 바닥을 맞히지 못하면 플레이어 높이의 가상 평면으로 대체
            Plane plane = new Plane(Vector3.up, transform.position);
            return plane.Raycast(ray, out float enter) ? ray.GetPoint(enter) : transform.position;
        }
    }
}
