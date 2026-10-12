using UnityEngine;

namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 대상에게 다가가 사거리 안에서 실행하는 행동 (근접·원거리 기본 공격, 대상 지정 기술).
    /// 접근·추적은 호출하는 쪽(PlayerController)이 맡고, 이 인터페이스는 사거리와 실행만 정의합니다.
    /// 지점 지정 범위기·유지형 기술(휠윈드 등)은 대상이 없으므로 이 인터페이스가 아니라 기술 시전 경로를 사용합니다.
    /// </summary>
    public interface ITargetedAction
    {
        /// <summary>실행 가능한 최대 수평 거리</summary>
        float Range { get; }

        /// <summary>실행 후 행동이 이어지는 시간. 이 시간 동안 Attacking 상태를 유지합니다 (애니메이션 길이에 대응).</summary>
        float Duration { get; }

        /// <summary>
        /// 실행 시도. 재사용 대기 등으로 실행되지 않으면 false를 반환합니다.
        /// 대상이 없거나 빗나가도 실행 자체가 이뤄졌으면 true입니다.
        /// </summary>
        /// <param name="attacker">실행하는 주체</param>
        /// <param name="target">대상. 제자리 공격이면 null</param>
        /// <param name="point">공격 지점 (대상 위치 또는 커서 지점)</param>
        bool TryExecute(Transform attacker, Transform target, Vector3 point);
    }
}
