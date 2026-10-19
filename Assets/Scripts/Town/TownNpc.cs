using System.Collections.Generic;
using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// 마을 NPC (상인·대장장이·치유사). 이름·대사·상호작용 거리를 가지고, 대화 중에는 플레이어 쪽으로 몸을 돌립니다.
    /// 기능은 같은 오브젝트의 INpcService가 담당합니다 (없으면 대사만 표시).
    /// NpcInteractor가 활성 NPC 목록(All)에서 가장 가까운 NPC를 찾습니다.
    /// </summary>
    public class TownNpc : MonoBehaviour
    {
        private const float TURN_SPEED = 6f;

        private static readonly List<TownNpc> _all = new List<TownNpc>();

        [SerializeField] private string _displayName = "상인";
        [SerializeField] private string _title = "잡화상";
        [SerializeField, TextArea] private string[] _lines = { "어서 오시게." };
        [SerializeField] private float _interactRadius = 3f;
        [SerializeField] private float _promptHeight = 2.3f;

        private INpcService _service;
        private Quaternion _restRotation;
        private Transform _lookTarget;
        private int _lineIndex;

        public static IReadOnlyList<TownNpc> All => _all;

        public string DisplayName => _displayName;
        public string Title => _title;
        public float InteractRadius => _interactRadius;
        public Vector3 PromptPosition => transform.position + Vector3.up * _promptHeight;

        /// <summary>NPC 기능 (치료 등). 없으면 null</summary>
        public INpcService Service => _service;

        private void Awake()
        {
            _service = GetComponent<INpcService>();
            _restRotation = transform.rotation;
        }

        private void OnEnable()
        {
            _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        /// <summary>대사를 차례로 돌려 줍니다 (대화할 때마다 다음 대사)</summary>
        public string NextLine()
        {
            if (_lines == null || _lines.Length == 0)
            {
                return string.Empty;
            }

            string line = _lines[_lineIndex % _lines.Length];
            _lineIndex++;
            return line;
        }

        /// <summary>대화 시작·종료. 대상이 있으면 그쪽을 보고, 없으면 원래 방향으로 돌아갑니다.</summary>
        public void LookAt(Transform target)
        {
            _lookTarget = target;
        }

        private void Update()
        {
            Quaternion goal = _restRotation;

            if (_lookTarget != null)
            {
                Vector3 dir = _lookTarget.position - transform.position;
                dir.y = 0f;

                if (dir.sqrMagnitude > 0.01f)
                {
                    goal = Quaternion.LookRotation(dir);
                }
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, goal, 1f - Mathf.Exp(-TURN_SPEED * Time.deltaTime));
        }
    }
}
