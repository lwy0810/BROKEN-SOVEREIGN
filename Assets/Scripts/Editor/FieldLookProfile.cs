using UnityEngine;

namespace BrokenSovereign.Editor
{
    /// <summary>
    /// 천계 필드 분위기 값 (하늘·조명·안개·구름 바다·석판 색). GDD 13장 필드 구성.
    /// 값은 이 에셋(Assets/Settings/FieldLookProfile.asset)에서 고치고 메뉴 'BrokenSovereign > Field > 현재 씬에 분위기 적용'으로 반영합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "FieldLookProfile", menuName = "BrokenSovereign/Field Look Profile")]
    public class FieldLookProfile : ScriptableObject
    {
        [Header("하늘 (Skybox/Procedural)")]
        public Color SkyTint = new Color(1f, 0.74f, 0.80f);
        public Color SkyGround = new Color(0.98f, 0.82f, 0.74f);
        [Range(0f, 5f)] public float AtmosphereThickness = 1.35f;
        [Range(0f, 1f)] public float SunSize = 0.045f;
        [Range(0f, 8f)] public float SkyExposure = 1.25f;

        [Header("태양 (방향광)")]
        public Color SunColor = new Color(1f, 0.89f, 0.72f);
        public float SunIntensity = 1.45f;
        public Vector3 SunEuler = new Vector3(48f, -35f, 0f);

        [Header("주변광 (Trilight)")]
        public Color AmbientSky = new Color(1f, 0.90f, 0.74f);
        public Color AmbientEquator = new Color(0.96f, 0.78f, 0.80f);
        public Color AmbientGround = new Color(0.66f, 0.58f, 0.74f);

        [Header("안개 (선형)")]
        public Color FogColor = new Color(0.99f, 0.86f, 0.83f);
        public float FogStart = 45f;
        public float FogEnd = 170f;

        [Header("후처리")]
        public float BloomThreshold = 0.95f;
        public float BloomIntensity = 0.55f;
        public Color BloomTint = new Color(1f, 0.92f, 0.80f);
        public float PostExposure = 0.1f;
        public float Saturation = 8f;
        public float Vignette = 0.18f;

        [Header("구름 바다")]
        public Color CloudHigh = new Color(1f, 0.97f, 0.93f);
        public Color CloudLow = new Color(0.86f, 0.68f, 0.76f);
        public Color CloudGlow = new Color(1f, 0.84f, 0.55f);
        public float CloudHeight = -7f;
        public float CloudSize = 320f;

        [Header("석판")]
        public Color StoneTop = new Color(0.93f, 0.91f, 0.87f);
        [Range(0f, 1f)] public float StoneSmoothness = 0.35f;
        public Color StoneRock = new Color(0.78f, 0.70f, 0.72f);
    }
}
