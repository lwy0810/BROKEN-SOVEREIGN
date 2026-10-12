using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 테스트용 기본 자원(마나, 파란색): 기술 시전 시 감소, 초당 회복. HUD 자원 구슬 동작 확인용.
    /// 시전을 막지는 않습니다. 전직 시 이 컴포넌트를 끄고 직업 자원(신앙 등)을 켜면 HUD 구슬 색과 값이 바뀝니다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class TestEnergyGauge : MonoBehaviour, IResourceGauge
    {
        [SerializeField] private float _max = 100f;
        [SerializeField] private float _costPerSkill = 15f;
        [SerializeField] private float _regenPerSecond = 8f;
        [SerializeField] private Color _displayColor = new Color(0.16f, 0.38f, 0.95f);

        private PlayerController _controller;
        private float _current;

        public float Current => _current;
        public float Max => _max;
        public Color DisplayColor => _displayColor;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _current = _max;
        }

        private void OnEnable()
        {
            _controller.SkillUsed += OnSkillUsed;
        }

        private void OnDisable()
        {
            _controller.SkillUsed -= OnSkillUsed;
        }

        private void Update()
        {
            _current = Mathf.Min(_max, _current + _regenPerSecond * Time.deltaTime);
        }

        private void OnSkillUsed(SkillSlot slot, Vector3 point)
        {
            _current = Mathf.Max(0f, _current - _costPerSkill);
        }
    }
}
