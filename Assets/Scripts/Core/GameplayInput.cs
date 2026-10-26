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

        /// <summary>마우스가 HUD 버튼(미니맵 +/- 등) 위에 있음: 클릭 이동·공격 시작을 막음 (10/25)</summary>
        public static bool PointerOverHud
        {
            get => _pointerOverHud || PointerOverMenu || PointerOverBar || PointerOverItem || PointerOverToast;
            set => _pointerOverHud = value;
        }

        /// <summary>메뉴 버튼·메뉴 창 위 (10/26). 미니맵이 매 프레임 PointerOverHud를 덮어쓰므로 따로 둠</summary>
        public static bool PointerOverMenu { get; set; }

        /// <summary>마우스가 기술 바 판 위 (10/26): 바 클릭이 바닥 이동으로 새지 않도록</summary>
        public static bool PointerOverBar { get; set; }

        /// <summary>마우스가 바닥 아이템·상자 이름표 위 (10/26): 클릭은 줍기로</summary>
        public static bool PointerOverItem { get; set; }

        /// <summary>마우스가 업적 달성 팝업 위 (10/26): 클릭은 업적 창 열기로</summary>
        public static bool PointerOverToast { get; set; }

        private static bool _pointerOverHud;

        // 도메인 리로드를 끈 Play에서도 시작할 때 초기화
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnLoad()
        {
            IsBlocked = false;
            PointerOverHud = false;
            PointerOverMenu = false;
            PointerOverBar = false;
            PointerOverItem = false;
            PointerOverToast = false;
        }
    }
}
