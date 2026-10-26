using System;
using BrokenSovereign.Core;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 플레이어 쪽 NPC 상호작용: 상호작용 거리 안의 가장 가까운 NPC를 고르고(Focus), F 키로 대화를 시작·종료합니다.
    /// 대화 중에는 전투 입력을 막고(GameplayInput), NPC가 플레이어를 바라봅니다.
    /// 대화 창(UI)은 이벤트를 구독해 표시만 하고, 닫기·기능 실행은 이 컴포넌트를 통해 요청합니다.
    /// </summary>
    public class NpcInteractor : MonoBehaviour
    {
        private const string MAP_NAME = "Gameplay";
        private const string ACTION_NAME = "Interact";

        [SerializeField] private InputActionAsset _inputActions;

        private InputAction _interact;
        private TownNpc _focus;
        private TownNpc _talking;

        /// <summary>F 키로 대화할 수 있는 NPC (없으면 null)</summary>
        public TownNpc Focus => _focus;

        /// <summary>대화 중인 NPC (없으면 null)</summary>
        public TownNpc Talking => _talking;

        public event Action<TownNpc> TalkStarted;
        public event Action TalkEnded;

        /// <summary>NPC 기능 실행 결과 대사</summary>
        public event Action<string> ServiceUsed;

        /// <summary>상점 창 열기·닫기 (10/25)</summary>
        public event Action<TownNpc, ShopType> ShopOpened;
        public event Action ShopClosed;

        /// <summary>상점 창이 열려 있음 (Esc·F는 상점 창이 처리)</summary>
        public bool IsShopping { get; private set; }

        private TownNpc _pending;           // 클릭한 NPC: 다가가면 대화 시작 (10/25)
        private int _shopClosedFrame = -1;

        private void Awake()
        {
            if (_inputActions != null)
            {
                _interact = _inputActions.FindActionMap(MAP_NAME, true).FindAction(ACTION_NAME, false);
            }

            if (_interact == null)
            {
                // 입력 에셋이 연결되지 않았을 때 대비
                _interact = new InputAction(ACTION_NAME, InputActionType.Button, "<Keyboard>/f");
            }
        }

        private void OnEnable()
        {
            _interact.Enable();
        }

        private void OnDisable()
        {
            EndTalk();
            _interact.Disable();
        }

        private void Update()
        {
            if (_talking != null)
            {
                if (IsShopping || Time.frameCount == _shopClosedFrame)
                {
                    return;   // 상점 창이 Esc를 먼저 씀
                }

                if (_interact.WasPressedThisFrame() || IsCancelPressed())
                {
                    EndTalk();
                }

                return;
            }

            // 다른 전체 화면 UI(직업 선택 등)가 열려 있으면 무시
            if (GameplayInput.IsBlocked)
            {
                _focus = null;
                return;
            }

            _focus = FindNearest();

            if (_focus != null && _interact.WasPressedThisFrame())
            {
                BeginTalk(_focus);
                return;
            }

            HandleClick();
        }

        public void BeginTalk(TownNpc npc)
        {
            if (npc == null || _talking != null)
            {
                return;
            }

            _talking = npc;
            _focus = null;

            if (TryGetComponent(out PlayerController player))
            {
                player.StopMoving();
            }

            npc.LookAt(transform);
            GameplayInput.IsBlocked = true;
            TalkStarted?.Invoke(npc);
        }

        public void EndTalk()
        {
            CloseShop();
            _pending = null;

            if (_talking == null)
            {
                return;
            }

            _talking.LookAt(null);
            _talking = null;
            GameplayInput.IsBlocked = false;
            TalkEnded?.Invoke();
        }

        /// <summary>대화 중인 NPC의 기능 실행 (대화 창 버튼)</summary>
        public void UseService()
        {
            if (_talking == null || _talking.Service == null)
            {
                return;
            }

            ServiceUsed?.Invoke(_talking.Service.Use(gameObject));
        }

        /// <summary>다음 대사 ("이야기를 듣는다")</summary>
        public void Talk()
        {
            if (_talking != null)
            {
                ServiceUsed?.Invoke(_talking.NextLine());
            }
        }

        public void OpenShop()
        {
            ShopType type = ShopCatalog.TypeOf(_talking);

            if (_talking == null || type == ShopType.None || IsShopping)
            {
                return;
            }

            IsShopping = true;
            ShopOpened?.Invoke(_talking, type);
        }

        public void CloseShop()
        {
            if (!IsShopping)
            {
                return;
            }

            IsShopping = false;
            _shopClosedFrame = Time.frameCount;
            ShopClosed?.Invoke();
        }

        // 10/25: NPC를 클릭하면 상호작용 거리까지 걸어가서 대화 시작. 다른 곳을 클릭하면 취소
        private void HandleClick()
        {
            Mouse mouse = Mouse.current;
            Camera camera = Camera.main;

            if (mouse != null && camera != null && mouse.leftButton.wasPressedThisFrame && !GameplayInput.PointerOverHud)
            {
                _pending = null;
                Ray ray = camera.ScreenPointToRay(mouse.position.ReadValue());

                // NPC 레이어만 검사 (보이지 않는 건물 차단 상자 등이 가로막지 않게). 레이어가 없으면 기본 레이어
                int mask = LayerMask.GetMask("NPC");

                if (Physics.Raycast(ray, out RaycastHit hit, 300f, mask != 0 ? mask : Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide))
                {
                    TownNpc npc = hit.collider.GetComponentInParent<TownNpc>();

                    if (npc != null && TryGetComponent(out PlayerController player))
                    {
                        _pending = npc;
                        player.MoveTo(npc.transform.position);
                    }
                }
            }

            if (_pending == null)
            {
                return;
            }

            Vector3 offset = _pending.transform.position - transform.position;
            offset.y = 0f;

            if (offset.sqrMagnitude <= _pending.InteractRadius * _pending.InteractRadius)
            {
                TownNpc npc = _pending;
                _pending = null;
                BeginTalk(npc);
            }
        }

        private TownNpc FindNearest()
        {
            TownNpc nearest = null;
            float best = float.MaxValue;
            Vector3 position = transform.position;

            foreach (TownNpc npc in TownNpc.All)
            {
                Vector3 offset = npc.transform.position - position;
                offset.y = 0f;
                float distance = offset.sqrMagnitude;
                float radius = npc.InteractRadius;

                if (distance <= radius * radius && distance < best)
                {
                    best = distance;
                    nearest = npc;
                }
            }

            return nearest;
        }

        private static bool IsCancelPressed()
        {
            Keyboard keyboard = Keyboard.current;
            return keyboard != null && keyboard.escapeKey.wasPressedThisFrame;
        }
    }
}
