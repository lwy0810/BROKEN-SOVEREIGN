namespace BrokenSovereign.Enemy
{
    /// <summary>일반 적 AI 상태</summary>
    public enum EnemyState
    {
        /// <summary>시작 위치에서 대기. 감지 거리 안에 들어오거나 피격되면 추적</summary>
        Idle,

        /// <summary>대상 추적. 공격 사거리 안이면 멈춰서 공격 대기</summary>
        Chase,

        /// <summary>예비 동작 → 타격 → 후딜레이. 시작하면 끝까지 진행</summary>
        Attack,

        /// <summary>추적 포기 후 시작 위치로 복귀 (체력 회복, 대상 무시)</summary>
        Return,

        /// <summary>사망</summary>
        Dead
    }
}
