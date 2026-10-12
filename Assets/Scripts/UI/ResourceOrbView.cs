using UnityEngine;
using UnityEngine.UI;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 자원 구슬 하나 (체력·직업 자원 공용). ResourceOrb 셰이더에 채움 비율과 연출 값을 전달합니다.
    /// 값이 바뀌면 액체가 목표 높이를 부드럽게 따라가고, 바뀐 양만큼 수면 높이가 잠깐 위아래로 흔들립니다 (수면은 항상 수평).
    /// </summary>
    public class ResourceOrbView : MonoBehaviour
    {
        private static readonly int FILL_ID = Shader.PropertyToID("_Fill");
        private static readonly int LIQUID_COLOR_ID = Shader.PropertyToID("_LiquidColor");
        private static readonly int SURFACE_COLOR_ID = Shader.PropertyToID("_SurfaceColor");
        private static readonly int RIM_COLOR_ID = Shader.PropertyToID("_RimColor");
        private static readonly int VIEW_TILT_ID = Shader.PropertyToID("_ViewTilt");
        private static readonly int BOUNCE_ID = Shader.PropertyToID("_Bounce");
        private static readonly int LOCKED_ID = Shader.PropertyToID("_Locked");

        private Material _material;
        private Text _valueText;
        private OrbSettings _settings;
        private float _targetFill = 1f;
        private float _shownFill = 1f;
        private float _bounce;
        private bool _locked;
        private Color _liquidColor;
        private Color _targetLiquidColor;
        private float _colorBlendSpeed;
        private Color _surfaceColorOverride;
        private bool _hasSurfaceOverride;

        public RectTransform Rect => (RectTransform)transform;

        public void Initialize(Shader shader)
        {
            Image image = HudGraphics.CreateRect("Liquid", transform).gameObject.AddComponent<Image>();
            HudGraphics.Stretch(image.rectTransform);
            image.raycastTarget = false;
            _material = new Material(shader) { name = $"{name}Material" };
            image.material = _material;

            _valueText = HudGraphics.CreateRect("Value", transform).gameObject.AddComponent<Text>();
            HudGraphics.Stretch(_valueText.rectTransform);
            _valueText.font = HudGraphics.Font;
            _valueText.alignment = TextAnchor.MiddleCenter;
            _valueText.color = Color.white;
            _valueText.raycastTarget = false;
            _valueText.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(1.5f, -1.5f);
        }

        /// <summary>
        /// 배치·연출 값 적용. surfaceColor가 null이면 액체 색을 밝게 섞어 수면 띠 색으로 사용합니다.
        /// </summary>
        public void ApplyStyle(OrbSettings settings, Color liquidColor, Color? surfaceColor = null)
        {
            _settings = settings;
            _hasSurfaceOverride = surfaceColor.HasValue;
            _surfaceColorOverride = surfaceColor ?? Color.white;
            _liquidColor = _targetLiquidColor = liquidColor;
            ApplyColors(liquidColor);
            _material.SetColor(RIM_COLOR_ID, settings.RimColor);
            _material.SetFloat(VIEW_TILT_ID, settings.ViewTilt);
            _valueText.fontSize = settings.FontSize;
        }

        public void SetValue(float current, float max)
        {
            SetLocked(false);
            float fill = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            float delta = Mathf.Abs(fill - _targetFill);

            if (delta > 0.0001f && _settings != null)
            {
                _bounce = Mathf.Min(1f, _bounce + delta * _settings.BounceStrength);
            }

            _targetFill = fill;
            _valueText.text = $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
        }

        /// <summary>
        /// 액체 색을 바꿉니다 (전직 시 자원 교체 등). blendTime 동안 부드럽게 넘어갑니다.
        /// </summary>
        public void SetLiquidColor(Color color, float blendTime)
        {
            if (color == _targetLiquidColor)
            {
                return;
            }

            _targetLiquidColor = color;
            _colorBlendSpeed = blendTime > 0f ? 1f / blendTime : float.MaxValue;
        }

        public void SetLocked(bool locked)
        {
            if (_locked == locked)
            {
                return;
            }

            _locked = locked;
            _material.SetFloat(LOCKED_ID, locked ? 1f : 0f);

            if (locked)
            {
                _targetFill = 0f;
                _valueText.text = "-";
            }
        }

        private void Update()
        {
            if (_material == null || _settings == null)
            {
                return;
            }

            _shownFill = Mathf.MoveTowards(_shownFill, _targetFill, _settings.FillFollowSpeed * Time.deltaTime);
            _bounce = Mathf.MoveTowards(_bounce, 0f, _settings.BounceDecay * Time.deltaTime);
            _material.SetFloat(FILL_ID, _shownFill);
            _material.SetFloat(BOUNCE_ID, _bounce);

            if (_liquidColor != _targetLiquidColor)
            {
                _liquidColor = MoveTowardsColor(_liquidColor, _targetLiquidColor, _colorBlendSpeed * Time.deltaTime);
                ApplyColors(_liquidColor);
            }
        }

        private void ApplyColors(Color liquidColor)
        {
            Color surface = _hasSurfaceOverride
                ? _surfaceColorOverride
                : Color.Lerp(liquidColor, Color.white, _settings != null ? _settings.SurfaceLighten : 0.55f);
            _material.SetColor(LIQUID_COLOR_ID, liquidColor);
            _material.SetColor(SURFACE_COLOR_ID, surface);
        }

        private static Color MoveTowardsColor(Color from, Color to, float step)
        {
            return new Color(
                Mathf.MoveTowards(from.r, to.r, step),
                Mathf.MoveTowards(from.g, to.g, step),
                Mathf.MoveTowards(from.b, to.b, step),
                Mathf.MoveTowards(from.a, to.a, step));
        }

        private void OnDestroy()
        {
            if (_material != null)
            {
                Destroy(_material);
            }
        }
    }
}
