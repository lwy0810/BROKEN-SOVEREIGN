using System;
using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 웨이포인트 (워프 지점). 플레이어가 가까이 가면 발견(활성화)되고, 발견한 웨이포인트끼리 이동할 수 있습니다.
    /// 발견 여부는 이번 실행 동안만 유지합니다 (저장 기능 도입 시 연결). 마을 광장 웨이포인트는 처음부터 발견.
    /// 상호작용은 플레이어의 WaypointTraveler(F 키)가, 목록 창은 WaypointWindowView가 담당합니다.
    /// 배치: BrokenSovereign > Levels > 웨이포인트 배치 (현재 씬) (LevelTools.Waypoints)
    /// </summary>
    public class Waypoint : MonoBehaviour
    {
        private static readonly List<Waypoint> _all = new List<Waypoint>();
        private static readonly HashSet<string> _discovered = new HashSet<string>();

        [SerializeField] private string _id = "town";
        [SerializeField] private string _displayName = "마을 광장";
        [Tooltip("목록 정렬 순서 (작을수록 위)")]
        [SerializeField] private int _order;
        [SerializeField] private bool _discoveredAtStart;
        [SerializeField] private float _discoverRadius = 8f;
        [SerializeField] private float _interactRadius = 3.5f;
        [Tooltip("도착 위치 (웨이포인트 중심에서 앞쪽으로 이 거리)")]
        [SerializeField] private float _arriveOffset = 3f;
        [SerializeField] private float _promptHeight = 2.5f;
        [Tooltip("이 거리 안(석단 위)에 서면 목록 창이 열림. 0 = 모형 크기로 자동 (10/25)")]
        [SerializeField] private float _triggerRadius;

        private float _autoTriggerRadius = -1f;

        public static IReadOnlyList<Waypoint> All => _all;

        /// <summary>위치에서 가장 가까운 웨이포인트 (10/26 부활 지점). discoveredOnly면 발견한 것만. 없으면 null</summary>
        public static Waypoint Nearest(Vector3 position, bool discoveredOnly)
        {
            Waypoint best = null;
            float bestDistance = float.MaxValue;

            foreach (Waypoint waypoint in _all)
            {
                if (waypoint == null || !waypoint.isActiveAndEnabled || (discoveredOnly && !waypoint.IsDiscovered))
                {
                    continue;
                }

                float distance = (waypoint.transform.position - position).sqrMagnitude;

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = waypoint;
                }
            }

            return best;
        }

        /// <summary>새 웨이포인트를 발견했을 때 (안내 문구용)</summary>
        public static event Action<Waypoint> AnyDiscovered;

        public string Id => _id;
        public string DisplayName => _displayName;
        public int Order => _order;
        public float DiscoverRadius => _discoverRadius;
        public float InteractRadius => _interactRadius;
        public bool IsDiscovered => _discovered.Contains(_id);
        public Vector3 PromptPosition => transform.position + Vector3.up * _promptHeight;

        /// <summary>목록 창이 열리는 거리 = 석단 반경 (모형 렌더러 크기 기준, 1.2~3m)</summary>
        public float TriggerRadius
        {
            get
            {
                if (_triggerRadius > 0f)
                {
                    return _triggerRadius;
                }

                if (_autoTriggerRadius < 0f)
                {
                    float extent = 0f;

                    foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
                    {
                        if (renderer is ParticleSystemRenderer)
                        {
                            continue;
                        }

                        Vector3 e = renderer.bounds.extents;
                        Vector3 offset = renderer.bounds.center - transform.position;
                        extent = Mathf.Max(extent, Mathf.Max(Mathf.Abs(offset.x) + e.x, Mathf.Abs(offset.z) + e.z));
                    }

                    _autoTriggerRadius = Mathf.Clamp(extent * 0.85f + 0.2f, 1.2f, 3f);
                }

                return _autoTriggerRadius;
            }
        }

        /// <summary>도착 지점 (웨이포인트 앞쪽, 석단 바깥)</summary>
        public Vector3 ArrivePosition => transform.position + transform.forward * _arriveOffset;

        public void Configure(string id, string displayName, int order, bool discoveredAtStart)
        {
            _id = id;
            _displayName = displayName;
            _order = order;
            _discoveredAtStart = discoveredAtStart;
        }

        private void OnEnable()
        {
            _all.Add(this);
            _all.Sort((a, b) => a._order.CompareTo(b._order));

            if (_discoveredAtStart)
            {
                _discovered.Add(_id);
            }
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        /// <summary>발견 처리. 처음 발견이면 true</summary>
        public bool Discover()
        {
            if (!_discovered.Add(_id))
            {
                return false;
            }

            AnyDiscovered?.Invoke(this);
            return true;
        }

        // 도메인 리로드를 끈 Play 모드에서도 이전 실행의 발견 기록이 남지 않도록
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _all.Clear();
            _discovered.Clear();
            AnyDiscovered = null;
        }

        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.45f, 0.75f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, _interactRadius);
            Gizmos.DrawSphere(ArrivePosition, 0.3f);
        }
    }
}
