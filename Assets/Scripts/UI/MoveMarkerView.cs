using BrokenSovereign.Player;
using UnityEngine;
using UnityEngine.Rendering;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 바닥을 클릭한 지점에 이동 표식(원)을 1초 동안 표시합니다. 표식은 1개만 쓰며, 다시 클릭하면 새 지점으로 옮겨 처음부터 표시.
    /// 원이 줄어들며 사라집니다 (LineRenderer). HudView가 생성합니다.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class MoveMarkerView : MonoBehaviour
    {
        private const int SEGMENTS = 48;
        private const float DURATION = 1f;
        private const float START_RADIUS = 0.6f;
        private const float END_RADIUS = 0.3f;
        private const float WIDTH = 0.06f;
        private const float GROUND_OFFSET = 0.05f;

        private static Color COLOR => UiTheme.P.Accent;
        private static readonly string[] SHADER_NAMES = { "Sprites/Default", "Universal Render Pipeline/Unlit" };

        private LineRenderer _line;
        private Material _material;
        private PlayerInputHandler _input;
        private Vector3 _center;
        private float _startTime;

        public void Initialize(PlayerInputHandler input)
        {
            _line = GetComponent<LineRenderer>();
            _line.useWorldSpace = true;
            _line.loop = true;
            _line.positionCount = SEGMENTS;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            _line.alignment = LineAlignment.TransformZ;
            _line.startWidth = _line.endWidth = WIDTH;
            transform.rotation = Quaternion.Euler(90f, 0f, 0f);

            foreach (string shaderName in SHADER_NAMES)
            {
                Shader shader = Shader.Find(shaderName);

                if (shader != null)
                {
                    _material = new Material(shader);
                    _line.sharedMaterial = _material;
                    break;
                }
            }

            _line.enabled = false;
            _input = input;
            _input.GroundClicked += Show;
        }

        private void OnDestroy()
        {
            if (_input != null)
            {
                _input.GroundClicked -= Show;
            }

            if (_material != null)
            {
                Destroy(_material);
            }
        }

        private void Show(Vector3 point)
        {
            _center = point + Vector3.up * GROUND_OFFSET;
            _startTime = Time.time;
            _line.enabled = true;
            Draw(0f);
        }

        private void LateUpdate()
        {
            if (!_line.enabled)
            {
                return;
            }

            float t = (Time.time - _startTime) / DURATION;

            if (t >= 1f)
            {
                _line.enabled = false;
                return;
            }

            Draw(t);
        }

        // 빠르게 줄었다가 천천히 멈춤, 마지막 30% 페이드
        private void Draw(float t)
        {
            float shrink = 1f - (1f - t) * (1f - t);
            float radius = Mathf.Lerp(START_RADIUS, END_RADIUS, shrink);
            Color color = COLOR;
            color.a *= t < 0.7f ? 1f : 1f - (t - 0.7f) / 0.3f;
            _line.startColor = _line.endColor = color;

            if (_material != null)
            {
                _material.color = color;
            }

            for (int i = 0; i < SEGMENTS; i++)
            {
                float angle = i * Mathf.PI * 2f / SEGMENTS;
                _line.SetPosition(i, _center + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius);
            }
        }
    }
}
