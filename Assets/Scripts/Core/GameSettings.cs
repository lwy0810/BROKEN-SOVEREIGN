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
    /// 플레이어 설정값. 지금은 지도 표시 방식만 있으며, 추후 시스템 설정 화면에서 바꿉니다.
    /// PlayerPrefs에 저장되어 다음 실행에도 유지됩니다.
    /// </summary>
    public static class GameSettings
    {
        private const string MAP_MODE_KEY = "Settings.MapDisplayMode";

        public static MapDisplayMode MapMode
        {
            get => (MapDisplayMode)PlayerPrefs.GetInt(MAP_MODE_KEY, (int)MapDisplayMode.Fullscreen);
            set
            {
                PlayerPrefs.SetInt(MAP_MODE_KEY, (int)value);
                PlayerPrefs.Save();
            }
        }
    }
}
