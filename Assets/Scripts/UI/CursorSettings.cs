using System;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>
    /// 마우스 커서 설정: 기본 커서 + 레이어별 커서 규칙. 마우스 아래 첫 물체의 레이어가 규칙에 맞으면 그 커서로 바뀝니다.
    /// 규칙은 위에서부터 검사합니다. 새 레이어(예: 상자, 문)는 규칙만 추가하면 됩니다.
    /// 에셋: Assets/Settings/CursorSettings.asset (BrokenSovereign > UI > HUD 아트 연결 메뉴가 생성)
    /// </summary>
    [CreateAssetMenu(menuName = "BrokenSovereign/UI/Cursor Settings", fileName = "CursorSettings")]
    public class CursorSettings : ScriptableObject
    {
        [Serializable]
        public class Rule
        {
            public string Name = "Rule";
            public LayerMask Layers;
            public Texture2D Texture;
            [Tooltip("클릭 지점 (텍스처 왼쪽 위 기준 픽셀)")]
            public Vector2 Hotspot;
        }

        public Texture2D DefaultTexture;
        public Vector2 DefaultHotspot = new Vector2(2f, 2f);

        [Tooltip("마우스 아래를 검사하는 최대 거리 (m)")]
        public float RayDistance = 500f;

        public Rule[] Rules = Array.Empty<Rule>();
    }
}
