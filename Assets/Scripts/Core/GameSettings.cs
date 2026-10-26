using System;
using UnityEngine;

namespace BrokenSovereign.Core
{
    /// <summary>지도(M) 표시 방식</summary>
    public enum MapDisplayMode
    {
        Fullscreen,  // 전체 화면 지도 (게임 입력 차단, 마우스 끌기·휠 확대)
        Overlay      // 반투명 지도를 게임 화면 위에 겹침 (계속 이동 가능)
    }

    /// <summary>
    /// 플레이어 설정값 (10/26 설정 창, Figma 14-A). PlayerPrefs에 저장되어 다음 실행에도 유지됩니다.
    /// - 사운드: 전체·효과음·배경 음악 음량 (SoundPlayer가 읽음)
    /// - 화면: 전체 화면·해상도·그래픽 품질 (Apply에서 Screen·QualitySettings에 적용)
    /// - 게임: 피해 숫자 표시(DamageNumberView), 마우스 감도(카메라 휠 줌·지도 휠 줌·지도 끌기 배율)
    /// 값을 바꾸면 즉시 저장·적용되고 Changed가 발생합니다.
    /// </summary>
    public static class GameSettings
    {
        private const string MAP_MODE_KEY = "Settings.MapDisplayMode";
        private const string MASTER_KEY = "Settings.MasterVolume";
        private const string SFX_KEY = "Settings.SfxVolume";
        private const string MUSIC_KEY = "Settings.MusicVolume";
        private const string FULLSCREEN_KEY = "Settings.Fullscreen";
        private const string RESOLUTION_KEY = "Settings.Resolution";
        private const string QUALITY_KEY = "Settings.QualityHigh";
        private const string DAMAGE_KEY = "Settings.DamageNumbers";
        private const string SENSITIVITY_KEY = "Settings.MouseSensitivity";

        public const float DEFAULT_MASTER = 1f;
        public const float DEFAULT_SFX = 0.9f;
        public const float DEFAULT_MUSIC = 0.55f;
        public const float MIN_SENSITIVITY = 0.5f;
        public const float MAX_SENSITIVITY = 2f;

        /// <summary>해상도 선택지 (설정 창 순서)</summary>
        public static readonly Vector2Int[] RESOLUTIONS = { new Vector2Int(1280, 720), new Vector2Int(1600, 900), new Vector2Int(1920, 1080) };

        /// <summary>설정이 바뀌었을 때</summary>
        public static event Action Changed;

        public static MapDisplayMode MapMode
        {
            get => (MapDisplayMode)PlayerPrefs.GetInt(MAP_MODE_KEY, (int)MapDisplayMode.Fullscreen);
            set => SetInt(MAP_MODE_KEY, (int)value);
        }

        public static float MasterVolume { get => PlayerPrefs.GetFloat(MASTER_KEY, DEFAULT_MASTER); set => SetFloat(MASTER_KEY, Mathf.Clamp01(value)); }
        public static float SfxVolume { get => PlayerPrefs.GetFloat(SFX_KEY, DEFAULT_SFX); set => SetFloat(SFX_KEY, Mathf.Clamp01(value)); }
        public static float MusicVolume { get => PlayerPrefs.GetFloat(MUSIC_KEY, DEFAULT_MUSIC); set => SetFloat(MUSIC_KEY, Mathf.Clamp01(value)); }

        public static bool Fullscreen
        {
            get => PlayerPrefs.GetInt(FULLSCREEN_KEY, 1) == 1;
            set { SetInt(FULLSCREEN_KEY, value ? 1 : 0); ApplyDisplay(); }
        }

        /// <summary>RESOLUTIONS 번호</summary>
        public static int ResolutionIndex
        {
            get => Mathf.Clamp(PlayerPrefs.GetInt(RESOLUTION_KEY, RESOLUTIONS.Length - 1), 0, RESOLUTIONS.Length - 1);
            set { SetInt(RESOLUTION_KEY, Mathf.Clamp(value, 0, RESOLUTIONS.Length - 1)); ApplyDisplay(); }
        }

        /// <summary>그래픽 품질: false = 낮음(첫 품질 단계), true = 높음(마지막 품질 단계)</summary>
        public static bool HighQuality
        {
            get => PlayerPrefs.GetInt(QUALITY_KEY, 1) == 1;
            set { SetInt(QUALITY_KEY, value ? 1 : 0); ApplyQuality(); }
        }

        public static bool ShowDamageNumbers { get => PlayerPrefs.GetInt(DAMAGE_KEY, 1) == 1; set => SetInt(DAMAGE_KEY, value ? 1 : 0); }

        /// <summary>마우스 감도 배율 (0.5 ~ 2, 기본 1): 카메라 휠 줌·지도 휠 줌·지도 끌기</summary>
        public static float MouseSensitivity
        {
            get => Mathf.Clamp(PlayerPrefs.GetFloat(SENSITIVITY_KEY, 1f), MIN_SENSITIVITY, MAX_SENSITIVITY);
            set => SetFloat(SENSITIVITY_KEY, Mathf.Clamp(value, MIN_SENSITIVITY, MAX_SENSITIVITY));
        }

        /// <summary>[기본값]: 지도 방식을 뺀 모든 설정을 기본값으로</summary>
        public static void ResetToDefaults()
        {
            foreach (string key in new[] { MASTER_KEY, SFX_KEY, MUSIC_KEY, FULLSCREEN_KEY, RESOLUTION_KEY, QUALITY_KEY, DAMAGE_KEY, SENSITIVITY_KEY })
            {
                PlayerPrefs.DeleteKey(key);
            }

            PlayerPrefs.Save();
            ApplyDisplay();
            ApplyQuality();
            Changed?.Invoke();
        }

        // 게임 시작 시 저장된 화면·품질 설정 적용
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void ApplyOnStart()
        {
            ApplyDisplay();
            ApplyQuality();
        }

        private static void ApplyDisplay()
        {
#if !UNITY_EDITOR
            Vector2Int size = RESOLUTIONS[ResolutionIndex];
            Screen.SetResolution(size.x, size.y, Fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }

        private static void ApplyQuality()
        {
            int count = QualitySettings.names.Length;

            if (count > 0)
            {
                int level = HighQuality ? count - 1 : 0;

                if (QualitySettings.GetQualityLevel() != level)
                {
                    QualitySettings.SetQualityLevel(level, true);
                }
            }
        }

        private static void SetInt(string key, int value)
        {
            PlayerPrefs.SetInt(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }

        private static void SetFloat(string key, float value)
        {
            PlayerPrefs.SetFloat(key, value);
            PlayerPrefs.Save();
            Changed?.Invoke();
        }
    }
}
