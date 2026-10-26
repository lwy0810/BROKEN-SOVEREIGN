using System;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using BrokenSovereign.Map;
using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.InputSystem;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 마을 귀환 포탈 (T, 10/25). 디아블로식 양방향 포탈.
    /// - 필드에서 T: 1.5초 시전 (움직이거나 피격·사망·전체 화면 창이 열리면 취소) → 앞쪽에 포탈 생성
    /// - 포탈 진입: 마을 웨이포인트 앞으로 이동 + 마을에 복귀 포탈 생성
    /// - 복귀 포탈 진입: 원래 포탈 앞으로 돌아가고 포탈 두 개 모두 사라짐
    /// - 마을 안·보스 전투 중(BossStatus.IsEngaged)에는 사용 불가. 새로 시전하면 이전 포탈은 사라짐
    /// HudView가 플레이어에 붙입니다. 표시는 TownPortalView.
    /// </summary>
    public class TownPortalCaster : MonoBehaviour
    {
        public const float CAST_TIME = 1.5f;
        private const string TOWN_WAYPOINT_ID = "town";
        private const float CAST_MOVE_TOLERANCE = 0.15f;
        private const float FIELD_PORTAL_DISTANCE = 2.2f;
        private const float TOWN_PORTAL_DISTANCE = 3f;
        private const float RETURN_BACK_OFF = 2f;   // 돌아올 때 포탈에서 이만큼 뒤 (바로 다시 타지 않게)

        private InputAction _key;
        private PlayerController _player;
        private Health _health;
        private float _castStart = -1f;
        private Vector3 _castPosition;
        private TownPortal _fieldPortal;
        private TownPortal _townPortal;

        public bool IsCasting => _castStart >= 0f;
        public float CastProgress => IsCasting ? Mathf.Clamp01((Time.time - _castStart) / CAST_TIME) : 0f;

        /// <summary>안내 문구 (사용 불가 사유·취소 등)</summary>
        public event Action<string> Message;

        private void Awake()
        {
            _key = new InputAction("TownPortal", InputActionType.Button, "<Keyboard>/t");
            _player = GetComponent<PlayerController>();
            _health = GetComponent<Health>();
        }

        private void OnEnable()
        {
            _key.Enable();

            if (_health != null)
            {
                _health.Damaged += OnDamaged;
            }
        }

        private void OnDisable()
        {
            _key.Disable();
            _castStart = -1f;

            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
            }
        }

        private void OnDestroy()
        {
            _key.Dispose();
            ClosePortals();
        }

        private void Update()
        {
            if (IsCasting)
            {
                UpdateCast();
                return;
            }

            if (_key.WasPressedThisFrame() && !GameplayInput.IsBlocked)
            {
                TryStartCast();
            }
        }

        private void TryStartCast()
        {
            if (_player != null && _player.IsDead)
            {
                return;
            }

            LevelMap level = LevelMap.Current;
            Vector3 p = transform.position;

            if (level != null && level.IsInTown(new Vector2(p.x, p.z)))
            {
                Message?.Invoke("마을에서는 귀환 포탈을 열 수 없습니다");
                return;
            }

            if (InBossFight())
            {
                Message?.Invoke("보스와 싸우는 중에는 귀환 포탈을 열 수 없습니다");
                return;
            }

            if (FindTownWaypoint() == null)
            {
                Message?.Invoke("돌아갈 마을이 없습니다");
                return;
            }

            if (_player != null)
            {
                _player.StopMoving();
            }

            _castStart = Time.time;
            _castPosition = p;
        }

        private void UpdateCast()
        {
            Vector3 moved = transform.position - _castPosition;
            moved.y = 0f;

            if (moved.sqrMagnitude > CAST_MOVE_TOLERANCE * CAST_MOVE_TOLERANCE || GameplayInput.IsBlocked
                || (_player != null && _player.IsDead) || InBossFight())
            {
                Cancel();
                return;
            }

            if (Time.time - _castStart >= CAST_TIME)
            {
                _castStart = -1f;
                OpenFieldPortal();
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            if (IsCasting)
            {
                Cancel();
            }
        }

        private void Cancel()
        {
            _castStart = -1f;
            Message?.Invoke("귀환 시전 취소");
        }

        private void OpenFieldPortal()
        {
            ClosePortals();
            Vector3 forward = transform.forward;
            Vector3 position = PlayerWarp.OnNavMesh(transform.position + forward * FIELD_PORTAL_DISTANCE);
            _fieldPortal = TownPortal.Create("TownPortal_Field", position, -forward, transform);
            _fieldPortal.Entered += OnFieldPortalEntered;
        }

        private void OnFieldPortalEntered(TownPortal portal)
        {
            Waypoint town = FindTownWaypoint();

            if (town == null)
            {
                return;
            }

            Vector3 arrive = town.ArrivePosition;
            Vector3 forward = town.transform.forward;
            PlayerWarp.Warp(transform, arrive, forward);

            if (_townPortal == null)
            {
                Vector3 position = PlayerWarp.OnNavMesh(arrive + forward * TOWN_PORTAL_DISTANCE);
                _townPortal = TownPortal.Create("TownPortal_Town", position, -forward, transform);
                _townPortal.Entered += OnTownPortalEntered;
            }
        }

        private void OnTownPortalEntered(TownPortal portal)
        {
            if (_fieldPortal == null)
            {
                ClosePortals();
                return;
            }

            Transform field = _fieldPortal.transform;
            PlayerWarp.Warp(transform, field.position + field.forward * RETURN_BACK_OFF, field.forward);
            ClosePortals();
        }

        private void ClosePortals()
        {
            if (_fieldPortal != null)
            {
                Destroy(_fieldPortal.gameObject);
                _fieldPortal = null;
            }

            if (_townPortal != null)
            {
                Destroy(_townPortal.gameObject);
                _townPortal = null;
            }
        }

        private static bool InBossFight()
        {
            foreach (BossStatus boss in BossStatus.Active)
            {
                if (boss != null && !boss.IsDead && boss.IsEngaged)
                {
                    return true;
                }
            }

            return false;
        }

        private static Waypoint FindTownWaypoint()
        {
            foreach (Waypoint waypoint in Waypoint.All)
            {
                if (waypoint.Id == TOWN_WAYPOINT_ID)
                {
                    return waypoint;
                }
            }

            return null;
        }
    }
}
