namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기본 공격 수정치 출처 (공격자 오브젝트의 컴포넌트). BasicAttack이 실행·사거리 계산 때 조회합니다.
    /// 전투 코드에 영웅별 분기를 두지 않기 위한 연결 지점 (예: 석천 모드변환 StaffReachMode)
    /// </summary>
    public interface IBasicAttackModifier
    {
        void ModifyBasicAttack(ref BasicAttackProfile profile);
    }

    /// <summary>기본 공격 1회의 판정 방식과 배율</summary>
    public struct BasicAttackProfile
    {
        /// <summary>사거리 추가 (m)</summary>
        public float RangeBonus;

        /// <summary>피해 계수 배율 (0.75 = 75%)</summary>
        public float CoefficientMultiplier;

        /// <summary>방어 관통 (0.3 = 대상 방어력 30% 무시)</summary>
        public float ArmorPenetration;

        /// <summary>0보다 크면 단일 대상 대신 일직선 다중 타격 (판정 폭 m)</summary>
        public float LineWidth;

        /// <summary>일직선 다중 타격 최대 대상 수</summary>
        public int MaxTargets;

        public static BasicAttackProfile Default => new BasicAttackProfile { CoefficientMultiplier = 1f };

        public bool IsLine => LineWidth > 0f;
    }
}
