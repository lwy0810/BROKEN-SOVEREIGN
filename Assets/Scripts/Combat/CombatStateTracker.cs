namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 전투 상태 판정 (순수 클래스). 마지막 전투 행동(공격 적중·피격) 후 일정 시간이 지나면 비전투로 봅니다.
    /// 시작 상태는 비전투입니다.
    /// </summary>
    public class CombatStateTracker
    {
        private readonly float _outOfCombatDelay;
        private float _sinceLastAction;

        public CombatStateTracker(float outOfCombatDelay)
        {
            _outOfCombatDelay = outOfCombatDelay;
            _sinceLastAction = float.MaxValue;
        }

        public bool InCombat => _sinceLastAction < _outOfCombatDelay;

        public void NotifyCombatAction()
        {
            _sinceLastAction = 0f;
        }

        public void Tick(float deltaTime)
        {
            if (_sinceLastAction < float.MaxValue)
            {
                _sinceLastAction += deltaTime;
            }
        }
    }
}
