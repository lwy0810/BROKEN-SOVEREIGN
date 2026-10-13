using System;
using UnityEngine;

namespace BrokenSovereign.UI
{
    /// <summary>자원 구슬 배치·연출 설정 (Play 중 Inspector 변경 시 즉시 반영)</summary>
    [Serializable]
    public class OrbSettings
    {
        public float Size = 190f;

        [Tooltip("기술 바 양 끝과 구슬 사이 간격")]
        public float GapToBar = 12f;

        [Tooltip("화면 아래쪽과 구슬 사이 여백")]
        public float BottomMargin = 12f;

        public Color HealthColor = new Color(0.72f, 0.07f, 0.09f);
        public Color HealthSurfaceColor = new Color(1f, 0.5f, 0.42f);

        [Tooltip("직업 자원 구슬 색이 바뀔 때(전직 등) 새 색으로 넘어가는 시간")]
        public float ResourceColorBlendTime = 0.6f;

        [Tooltip("수면 띠 색 = 액체 색을 흰색 쪽으로 섞은 색. 섞는 비율")]
        [Range(0f, 1f)] public float SurfaceLighten = 0.55f;
        public Color RimColor = new Color(0.78f, 0.6f, 0.28f);

        [Tooltip("수면을 내려다보는 정도 (클수록 수면 타원이 두꺼워짐)")]
        [Range(0.05f, 0.8f)] public float ViewTilt = 0.3f;

        [Tooltip("값이 바뀐 양에 곱해지는 수면 높이 흔들림 세기 (0이면 흔들림 없음)")]
        [Range(0f, 10f)] public float BounceStrength = 3f;

        [Tooltip("흔들림이 잦아드는 속도 (초당)")]
        public float BounceDecay = 2f;

        [Tooltip("액체 높이가 목표값을 따라가는 속도 (초당 비율)")]
        public float FillFollowSpeed = 1.5f;

        public int FontSize = 22;
    }

    /// <summary>기술 바 배치 설정 (Play 중 Inspector 변경 시 즉시 반영)</summary>
    [Serializable]
    public class SkillBarSettings
    {
        public float SlotSize = 72f;
        public float Spacing = 8f;

        [Tooltip("기술 슬롯과 물약·회피 묶음 사이 간격")]
        public float GroupSpacing = 28f;

        public float BottomMargin = 28f;
        public Color SlotColor = new Color(0.08f, 0.07f, 0.1f, 0.85f);
        public Color FrameColor = new Color(0.78f, 0.6f, 0.28f, 0.9f);
        public Color CooldownColor = new Color(0f, 0f, 0f, 0.65f);

        [Tooltip("켜면 재사용 대기 막이 12시에서 시계 방향으로 걷히며 사용 가능 영역이 시계 방향으로 차오름")]
        public bool CooldownSweepClockwise = true;
        public Color LockedColor = new Color(0f, 0f, 0f, 0.7f);
        public int KeyFontSize = 16;
        public int CooldownFontSize = 24;
    }

    /// <summary>선택 대상 표시 설정: 발밑 원 테두리와 화면 상단 체력 게이지 (Play 중 Inspector 변경 시 즉시 반영)</summary>
    [Serializable]
    public class TargetSettings
    {
        [Header("대상 원 테두리")]
        public Color RingColor = Color.white;
        public float RingWidth = 0.06f;

        [Tooltip("대상 충돌체 바깥으로 더 벌리는 거리 (m)")]
        public float RingPadding = 0.25f;

        [Tooltip("바닥에 묻히지 않도록 띄우는 높이 (m)")]
        public float RingHeightOffset = 0.03f;

        [Header("상단 체력 게이지")]
        public Vector2 BarSize = new Vector2(420f, 20f);

        [Tooltip("화면 위쪽과 게이지 사이 여백 (이름 표시 공간 포함)")]
        public float TopMargin = 48f;

        public Color BarColor = new Color(0.72f, 0.07f, 0.09f);
        public Color BarBackgroundColor = new Color(0f, 0f, 0f, 0.7f);
        public Color FrameColor = new Color(0.78f, 0.6f, 0.28f, 0.9f);
        public int NameFontSize = 20;
        public int PercentFontSize = 15;
    }
}
