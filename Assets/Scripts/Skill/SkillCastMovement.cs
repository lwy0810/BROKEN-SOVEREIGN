namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 키를 누르고 있을 때의 이동 방식 (우클릭 유지 시전 등).
    /// 새 방식(예: 시전 중 감속 이동, 커서 추적 돌진)은 값을 추가하고 PlayerController.CastWhileHeld에서 분기합니다.
    /// </summary>
    public enum SkillCastMovement
    {
        /// <summary>커서·대상이 사거리 밖이면 다가가고, 사거리 안이면 멈춰서 시전 (근접 강타 등)</summary>
        StopInRange,

        /// <summary>사거리와 상관없이 커서 쪽으로 계속 이동하며 반복 시전 (휠윈드 등 유지형)</summary>
        MoveWhileCasting,

        /// <summary>이동·접근 없이 그 자리에서 커서 방향으로 즉시 시전. 적을 클릭해도 다가가지 않음 (잔향 강타 등)</summary>
        InPlace
    }
}
