namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 기본 공격과 즉시 발동 기술(재사용 대기 0)이 함께 쓰는 공격 간격 타이머.
    /// 어느 쪽을 쓰든 다음 공격·기술까지 같은 간격을 기다리므로 "초당 공격 = 1 ÷ 간격"이 실제 행동 횟수와 같습니다.
    /// 첫 공격은 즉시, 이후 간격마다 1회 (n회 시간 = (n − 1) × 간격).
    /// </summary>
    public interface ISharedAttackTimer
    {
        bool IsAttackReady { get; }
        float AttackRemaining { get; }

        /// <summary>마지막으로 시작한 간격 (HUD 표시용)</summary>
        float AttackDuration { get; }

        /// <summary>공격·기술 사용: 지금부터 interval초 동안 다음 공격·즉시 발동 기술 불가</summary>
        void ConsumeAttack(float interval);
    }
}
