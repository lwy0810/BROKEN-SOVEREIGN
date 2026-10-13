namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 상태 판단에 쓰는 한 프레임의 감지 정보. EnemyController가 매 프레임 채워 EnemyBrain에 전달합니다.
    /// 거리는 수평 거리 기준입니다.
    /// </summary>
    public readonly struct EnemySense
    {
        public readonly bool IsDead;

        /// <summary>살아 있는 대상이 있는지</summary>
        public readonly bool HasTarget;

        public readonly float DistanceToTarget;
        public readonly float DistanceFromHome;

        /// <summary>이번 프레임에 피격되었는지 (감지 거리 밖에서 맞아도 추적 시작)</summary>
        public readonly bool WasDamaged;

        /// <summary>공격 간격이 지나 새 공격을 시작할 수 있는지</summary>
        public readonly bool CanAttack;

        /// <summary>진행 중인 공격(예비 동작·후딜레이 포함)이 끝났는지</summary>
        public readonly bool IsAttackFinished;

        public EnemySense(bool isDead, bool hasTarget, float distanceToTarget, float distanceFromHome,
            bool wasDamaged, bool canAttack, bool isAttackFinished)
        {
            IsDead = isDead;
            HasTarget = hasTarget;
            DistanceToTarget = distanceToTarget;
            DistanceFromHome = distanceFromHome;
            WasDamaged = wasDamaged;
            CanAttack = canAttack;
            IsAttackFinished = isAttackFinished;
        }
    }
}
