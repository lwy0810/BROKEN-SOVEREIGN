using UnityEngine;

namespace BrokenSovereign.Core
{
    /// <summary>
    /// 전투 입력 차단 (직업 선택 화면 등 전체 화면 UI가 열렸을 때).
    /// PlayerInputHandler가 확인하고, UI가 열고 닫을 때 설정합니다.
    /// </summary>
    public static class GameplayInput
    {
        public static bool IsBlocked { get; set; }

        // 도메인 리로드를 끈 Play에서도 시작할 때 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            IsBlocked = false;
        }
    }
}
