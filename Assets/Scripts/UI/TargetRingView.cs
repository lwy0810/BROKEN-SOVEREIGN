using BrokenSovereign.Combat;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 선택한 대상의 발밑에 흰색 원 테두리를 그립니다 (LineRenderer).
    /// 원 크기는 대상 충돌체 크기에 맞추고, 대상을 따라 움직입니다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class TargetRingView : MonoBehaviour
    {
        private const int SEGMENTS = 64;
        private const float FALLBACK_RADIUS = 0.5f;

        private static readonly string[] SHADER_NAMES = { "Sprites/Default", "Universal Render Pipeline/Unlit" };

        private LineRenderer _line;
        private Material _material;
        private Health _target;
        private TargetSettings _settings;
        private float _radius;
        private float _footOffset;

        public void Initialize(TargetSettings settings)
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = SEGMENTS;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.alignment = LineAlignment.TransformZ;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            Shader shader = FindShader();

            if (shader != null)
            {
                _material = new Material(shader);
                _line.sharedMaterial = _material;
            }

            ApplyStyle(settings);
            SetTarget(null);
        }

        public void ApplyStyle(TargetSettings settings)
        {
            _settings = settings;
            _line.startWidth = _line.endWidth = settings.RingWidth;
            _line.startColor = _line.endColor = settings.RingColor;

            if (_material != null)
            {
                _material.color = settings.RingColor;
            }

            if (_target != null)
            {
                MeasureTarget();
            }
        }

        public void SetTarget(Health target)
        {
            _target = target;
            _line.enabled = target != null;

            if (target != null)
            {
                MeasureTarget();
                UpdateRing();
            }
        }

        private void LateUpdate()
        {
            if (_target == null)
            {
                _line.enabled = false;
                return;
            }

            UpdateRing();
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }

        // 충돌체를 모두 합친 크기로 반지름과 발밑 높이를 구함
        private void MeasureTarget()
        {
            Collider[] colliders = _target.GetComponentsInChildren<Collider>();
            bool hasBounds = false;
            Bounds bounds = default;

            foreach (Collider bodyCollider in colliders)
            {
                if (!bodyCollider.enabled || bodyCollider.isTrigger)
                {
                    continue;
                }

                if (!hasBounds)
                {
                    bounds = bodyCollider.bounds;
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(bodyCollider.bounds);
                }
            }

            if (!hasBounds)
            {
                _radius = FALLBACK_RADIUS + _settings.RingPadding;
                _footOffset = 0f;
                return;
            }

            _radius = Mathf.Max(bounds.extents.x, bounds.extents.z) + _settings.RingPadding;
            _footOffset = bounds.min.y - _target.transform.position.y;
        }

        private void UpdateRing()
        {
            Vector3 center = _target.transform.position;
            center.y += _footOffset + _settings.RingHeightOffset;

            for (int i = 0; i < SEGMENTS; i++)
            {
                float angle = i * Mathf.PI * 2f / SEGMENTS;
                _line.SetPosition(i, center + new Vector3(Mathf.Cos(angle) * _radius, 0f, Mathf.Sin(angle) * _radius));
            }
        }

        private static Shader FindShader()
        {
            foreach (string shaderName in SHADER_NAMES)
            {
                Shader shader = Shader.Find(shaderName);

                if (shader != null)
                {
                    return shader;
                }
            }

            Debug.LogWarning("TargetRingView: 원 테두리용 셰이더를 찾지 못했습니다.");
            return null;
        }
    }
}
