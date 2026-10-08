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
        private bool _isAttackPress;

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

            if (_isAttackPress)
            {
                _controller.ReleaseEngage();
                _isAttackPress = false;
            }
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
            _isAttackPress = false;

            // 누르기 시작한 순간 적을 가리키고 있으면 교전 시작 (추적·공격은 PlayerController가 처리)
            if (!_standStill.IsPressed() && TryGetEnemyUnderCursor(out Transform enemy))
            {
                _isAttackPress = true;
                _controller.AttackTarget(enemy);
                return;
            }

            HandlePrimaryHeld();
        }

        private void OnPrimaryCanceled(InputAction.CallbackContext context)
        {
            _isPrimaryHeld = false;

            // 뗄 때: 아직 공격 전이면 1회 공격 후 해제, 이미 공격했으면 즉시 해제
            if (_isAttackPress)
            {
                _controller.ReleaseEngage();
                _isAttackPress = false;
            }
        }

        private void HandlePrimaryHeld()
        {
            // 교전 중이면 PlayerController가 처리. 대상이 사라지면 커서 방향 이동으로 전환
            if (_isAttackPress && _controller.HasEngageTarget)
            {
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
