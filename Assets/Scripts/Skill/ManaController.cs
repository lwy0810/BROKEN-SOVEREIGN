using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 마나: 모든 직업 공용 기술 비용 자원. 자동 회복. HUD 오른쪽 자원 구슬로 표시합니다.
    /// 직업을 바꿔도 현재 값이 유지됩니다.
    /// </summary>
    public class ManaController : MonoBehaviour, IResourceGauge, ISkillResource
    {
        [SerializeField, Min(1f)] private float _max = 100f;
        [SerializeField, Min(0f)] private float _regenPerSecond = 8f;
        [SerializeField] private Color _displayColor = new Color(0.16f, 0.38f, 0.95f);

        private float _current;

        public float Current => _current;
        public float Max => _max;
        public Color DisplayColor => _displayColor;

        private void Awake()
        {
            _current = _max;
        }

        private void Update()
        {
            _current = Mathf.Min(_max, _current + _regenPerSecond * Time.deltaTime);
        }

        public bool CanSpend(float amount)
        {
            return _current >= amount;
        }

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount))
            {
                return false;
            }

            _current -= Mathf.Max(0f, amount);
            return true;
        }

        private void OnValidate()
        {
            _max = Mathf.Max(1f, _max);
            _current = Mathf.Min(_current, _max);
        }
    }
}
