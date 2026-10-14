using BrokenSovereign.Core;
using BrokenSovereign.Skill;
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
        private SkillCaster _skillCaster;
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
        private bool _isSkillPress;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _skillCaster = GetComponent<SkillCaster>();

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

            if (_isAttackPress || _isSkillPress)
            {
                _controller.ReleaseEngage();
                _isAttackPress = false;
                _isSkillPress = false;
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
            // 적을 눌러 시작한 입력은 PlayerController가 처리.
            // 대상이 죽거나 사라져도 이동으로 바꾸지 않고 제자리에 멈춤 (다시 눌러야 이동)
            if (_isAttackPress)
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

            // 적에게 건 시전: 아직 시전 전이면 접근해서 1회 시전 후 해제, 이미 시전했으면 즉시 해제
            if (_isSkillPress)
            {
                _controller.ReleaseEngage();
                _isSkillPress = false;
                return;
            }

            _controller.StopMoving();
        }

        /// <summary>
        /// 우클릭 유지: 커서 방향으로 이동하며 주력 기술 반복 시전. Shift를 누르고 있으면 제자리 시전.
        /// 커서가 적 위에 있으면 그 적을 대상으로 교전 시전 (접근 → 사거리 안 정지 → 시전).
        /// </summary>
        private void HandleSecondaryHeld()
        {
            bool standStill = _standStill.IsPressed();

            // 제자리 즉시 시전 기술(잔향 강타 등)은 적을 가리켜도 접근하지 않고 커서 방향으로 바로 시전
            if (_controller.CastsInPlace(SkillSlot.RightClick))
            {
                _controller.CastWhileHeld(SkillSlot.RightClick, GetCursorGroundPoint(), false);
                return;
            }

            if (!standStill && TryGetEnemyUnderCursor(out Transform enemy))
            {
                _isSkillPress = true;
                _controller.CastAtTarget(SkillSlot.RightClick, enemy);
                return;
            }

            // 적에게 건 시전은 대상이 죽거나 사라져도 지면 시전으로 바꾸지 않음 (다시 눌러야 함)
            if (_isSkillPress)
            {
                return;
            }

            _controller.CastWhileHeld(SkillSlot.RightClick, GetCursorGroundPoint(), !standStill);
        }

        private void OnSkillKeyPerformed(InputAction.CallbackContext context)
        {
            int index = System.Array.IndexOf(_skillKeys, context.action);

            if (index < 0)
            {
                return;
            }

            SkillSlot slot = SkillSlot.Key1 + index;

            // Alt + 슬롯 키: 해금된 다음 후보로 교체 (디버그, 기술 선택 화면 구현 전까지)
            // Ctrl + 숫자는 Unity 에디터 창 전환 단축키(Ctrl+1 = Scene 창)와 겹쳐 Alt를 사용
            if (Keyboard.current != null && Keyboard.current.altKey.isPressed)
            {
                CycleSkill(slot);
                return;
            }

            _controller.UseSkill(slot, GetCursorGroundPoint());
        }

        private void CycleSkill(SkillSlot slot)
        {
            if (_skillCaster == null)
            {
                return;
            }

            EquipResult result = _skillCaster.EquipNext(slot);
            SkillDefinition skill = _skillCaster.GetSkill(slot);
            string skillName = skill != null ? skill.DisplayName : "없음";
            Debug.Log(result == EquipResult.Success
                ? $"기술 교체: {slot} → {skillName}"
                : $"기술 교체 실패 ({result}): {slot} 현재 {skillName}");
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
