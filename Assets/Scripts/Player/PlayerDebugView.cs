using System.Collections;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 임시 디버그 시각화. 기본 도형 플레이어에서 공격·기술·회피·물약 동작을 눈으로 확인하기 위한 컴포넌트.
    /// 실제 애니메이션·이펙트로 교체할 때 이 컴포넌트만 제거합니다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerDebugView : MonoBehaviour
    {
        private static readonly int BASE_COLOR_ID = Shader.PropertyToID("_BaseColor");

        [Header("연결")]
        [SerializeField] private Renderer _bodyRenderer;
        [SerializeField] private Transform _weaponPivot;
        [SerializeField] private Transform _headAnchor;

        [Header("기본 공격")]
        [SerializeField] private float _swingDuration = 0.15f;
        [SerializeField] private float _swingAngle = 110f;

        [Header("기술·회피·물약")]
        [SerializeField] private float _flashDuration = 0.2f;

        [Tooltip("기술 시전 지점·물약 사용 시 디버그 구체(DebugMarker) 표시. 기본 끔")]
        [SerializeField] private bool _showMarkers;
        [SerializeField] private float _markerDuration = 0.5f;
        [SerializeField] private float _markerSize = 0.4f;
        [SerializeField] private Color _dodgeColor = new Color(0.55f, 0.85f, 1f);
        [SerializeField] private float _dodgeTintDuration = 0.2f;
        [SerializeField] private Color _potionColor = new Color(0.3f, 1f, 0.4f);
        [SerializeField] private float _potionRiseHeight = 0.8f;
        [SerializeField] private float _potionDuration = 0.6f;

        [Header("화면 표시")]
        [SerializeField] private bool _showHud = true;

        private PlayerController _controller;
        private DodgeAction _dodge;
        private PotionUser _potion;
        private MaterialPropertyBlock _propertyBlock;
        private Color _bodyBaseColor = Color.white;
        private Quaternion _weaponRestRotation;
        private Coroutine _swingRoutine;
        private Coroutine _tintRoutine;
        private GUIStyle _hudStyle;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _dodge = GetComponent<DodgeAction>();
            _potion = GetComponent<PotionUser>();
            _propertyBlock = new MaterialPropertyBlock();

            if (_bodyRenderer != null && _bodyRenderer.sharedMaterial != null
                && _bodyRenderer.sharedMaterial.HasProperty(BASE_COLOR_ID))
            {
                _bodyBaseColor = _bodyRenderer.sharedMaterial.GetColor(BASE_COLOR_ID);
            }

            if (_weaponPivot != null)
            {
                _weaponRestRotation = _weaponPivot.localRotation;
            }
        }

        private void OnEnable()
        {
            _controller.BasicAttacked += OnBasicAttacked;
            _controller.SkillUsed += OnSkillUsed;
            _controller.Dodged += OnDodged;
            _controller.PotionUsed += OnPotionUsed;
        }

        private void OnDisable()
        {
            _controller.BasicAttacked -= OnBasicAttacked;
            _controller.SkillUsed -= OnSkillUsed;
            _controller.Dodged -= OnDodged;
            _controller.PotionUsed -= OnPotionUsed;
        }

        private void OnBasicAttacked(Vector3 point)
        {
            if (_weaponPivot == null)
            {
                return;
            }

            if (_swingRoutine != null)
            {
                StopCoroutine(_swingRoutine);
            }

            _swingRoutine = StartCoroutine(SwingRoutine());
        }

        private void OnSkillUsed(SkillSlot slot, Vector3 point)
        {
            Color color = GetSlotColor(slot);
            StartTint(color, _flashDuration);

            if (_showMarkers)
            {
                SpawnMarker(point + Vector3.up * (_markerSize * 0.5f), color, _markerDuration);
            }
        }

        private void OnDodged(Vector3 direction)
        {
            StartTint(_dodgeColor, _dodgeTintDuration);
        }

        private void OnPotionUsed(int remaining)
        {
            if (!_showMarkers)
            {
                return;
            }

            Vector3 start = _headAnchor != null ? _headAnchor.position : transform.position + Vector3.up * 2f;
            GameObject marker = SpawnMarker(start, _potionColor, _potionDuration);
            StartCoroutine(RiseRoutine(marker.transform, start));
        }

        private IEnumerator SwingRoutine()
        {
            float elapsed = 0f;

            while (elapsed < _swingDuration)
            {
                elapsed += Time.deltaTime;
                float angle = Mathf.Sin(Mathf.Clamp01(elapsed / _swingDuration) * Mathf.PI) * _swingAngle;
                _weaponPivot.localRotation = _weaponRestRotation * Quaternion.Euler(angle, 0f, 0f);
                yield return null;
            }

            _weaponPivot.localRotation = _weaponRestRotation;
            _swingRoutine = null;
        }

        private IEnumerator RiseRoutine(Transform marker, Vector3 start)
        {
            float elapsed = 0f;

            while (elapsed < _potionDuration && marker != null)
            {
                elapsed += Time.deltaTime;
                marker.position = start + Vector3.up * (_potionRiseHeight * (elapsed / _potionDuration));
                yield return null;
            }
        }

        private void StartTint(Color color, float duration)
        {
            if (_bodyRenderer == null)
            {
                return;
            }

            if (_tintRoutine != null)
            {
                StopCoroutine(_tintRoutine);
            }

            _tintRoutine = StartCoroutine(TintRoutine(color, duration));
        }

        private IEnumerator TintRoutine(Color color, float duration)
        {
            SetBodyColor(color);
            yield return new WaitForSeconds(duration);
            SetBodyColor(_bodyBaseColor);
            _tintRoutine = null;
        }

        private void SetBodyColor(Color color)
        {
            _bodyRenderer.GetPropertyBlock(_propertyBlock);
            _propertyBlock.SetColor(BASE_COLOR_ID, color);
            _bodyRenderer.SetPropertyBlock(_propertyBlock);
        }

        private GameObject SpawnMarker(Vector3 position, Color color, float lifetime)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "DebugMarker";
            marker.transform.position = position;
            marker.transform.localScale = Vector3.one * _markerSize;

            // 표식이 클릭 레이캐스트를 가로막지 않도록 충돌체 제거
            Destroy(marker.GetComponent<Collider>());

            Renderer markerRenderer = marker.GetComponent<Renderer>();
            MaterialPropertyBlock block = new MaterialPropertyBlock();
            block.SetColor(BASE_COLOR_ID, color);
            markerRenderer.SetPropertyBlock(block);

            Destroy(marker, lifetime);
            return marker;
        }

        private static Color GetSlotColor(SkillSlot slot)
        {
            return slot switch
            {
                SkillSlot.RightClick => new Color(1f, 0.25f, 0.25f),
                SkillSlot.Key1 => new Color(1f, 0.6f, 0.15f),
                SkillSlot.Key2 => new Color(0.3f, 0.9f, 0.3f),
                SkillSlot.Key3 => new Color(0.7f, 0.35f, 1f),
                SkillSlot.Key4 => Color.white,
                SkillSlot.Key5 => new Color(1f, 0.85f, 0.2f),
                SkillSlot.Key6 => new Color(0.3f, 0.85f, 1f),
                _ => Color.gray
            };
        }

        private void OnGUI()
        {
            // OnGUI는 모든 Canvas 위에 그려지므로 직업 선택 화면 등 전체 화면 UI가 열려 있으면 숨김
            if (GameplayInput.IsBlocked)
            {
                return;
            }

            if (!_showHud)
            {
                return;
            }

            _hudStyle ??= new GUIStyle(GUI.skin.label) { fontSize = 18 };

            string text = $"State: {_controller.State}\n"
                + $"Dodge CD: {_dodge.CooldownRemaining:0.0}s\n"
                + $"Potion: {_potion.Charges}";
            GUI.Label(new Rect(16f, 16f, 320f, 90f), text, _hudStyle);
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetComponent(out PlayerController controller))
            {
                return;
            }

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(transform.position, controller.BasicAttackRange);
        }
    }
}
