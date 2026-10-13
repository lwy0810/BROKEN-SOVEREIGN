namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 일반 적 상태 전이 규칙 (순수 클래스). 현재 상태와 감지 정보로 다음 상태를 정합니다.
    /// 이동·공격 실행은 EnemyController가 담당합니다.
    /// </summary>
    public class EnemyBrain
    {
        public readonly struct Settings
        {
            public readonly float DetectRange;
            public readonly float LoseRange;
            public readonly float LeashDistance;
            public readonly float AttackRange;
            public readonly float HomeArriveDistance;

            public Settings(float detectRange, float loseRange, float leashDistance, float attackRange,
                float homeArriveDistance)
            {
                DetectRange = detectRange;
                LoseRange = loseRange;
                LeashDistance = leashDistance;
                AttackRange = attackRange;
                HomeArriveDistance = homeArriveDistance;
            }
        }

        private readonly Settings _settings;

        public EnemyBrain(Settings settings)
        {
            _settings = settings;
        }

        public EnemyState Evaluate(EnemyState current, in EnemySense sense)
        {
            if (sense.IsDead)
            {
                return EnemyState.Dead;
            }

            switch (current)
            {
                case EnemyState.Dead:
                    // 부활
                    return EnemyState.Idle;

                case EnemyState.Idle:
                    return ShouldEngage(sense) ? EnemyState.Chase : EnemyState.Idle;

                case EnemyState.Chase:
                    if (ShouldGiveUp(sense))
                    {
                        return EnemyState.Return;
                    }

                    return sense.DistanceToTarget <= _settings.AttackRange && sense.CanAttack
                        ? EnemyState.Attack
                        : EnemyState.Chase;

                case EnemyState.Attack:
                    // 공격은 시작하면 끝까지 진행 (예비 동작 중 회피로 피하는 구조)
                    if (!sense.IsAttackFinished)
                    {
                        return EnemyState.Attack;
                    }

                    return ShouldGiveUp(sense) ? EnemyState.Return : EnemyState.Chase;

                case EnemyState.Return:
                    // 복귀 중에는 대상 무시
                    return sense.DistanceFromHome <= _settings.HomeArriveDistance ? EnemyState.Idle : EnemyState.Return;

                default:
                    return current;
            }
        }

        private bool ShouldEngage(in EnemySense sense)
        {
            return sense.HasTarget && (sense.WasDamaged || sense.DistanceToTarget <= _settings.DetectRange);
        }

        // 대상 사망·이탈, 시작 위치에서 너무 멀리 끌려감
        private bool ShouldGiveUp(in EnemySense sense)
        {
            return !sense.HasTarget
                || sense.DistanceToTarget > _settings.LoseRange
                || sense.DistanceFromHome > _settings.LeashDistance;
        }
    }
}
