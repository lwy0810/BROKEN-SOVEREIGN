namespace BrokenSovereign.Combat
{
    /// <summary>무력화(버팀 게이지)를 받을 수 있는 대상. 분쇄 일격이 처형 판정에 사용합니다.</summary>
    public interface IStaggerable
    {
        bool IsStaggered { get; }

        /// <summary>버팀 게이지를 최대치 비율만큼 감소 (0.5 = 50%)</summary>
        void ReduceRatio(float ratio);

        /// <summary>무력화 즉시 종료</summary>
        void EndStagger();
    }
}
