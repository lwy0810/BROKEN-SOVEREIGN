using System;

namespace BrokenSovereign.Combat
{
    /// <summary>기절 상태이상을 받을 수 있는 대상 (적 AI 등)</summary>
    public interface IStunnable
    {
        bool IsStunned { get; }

        void Stun(float duration);

        /// <summary>기절 즉시 종료 (분쇄 일격 처형 등)</summary>
        void EndStun();

        /// <summary>기절이 걸렸을 때 (지속 시간)</summary>
        event Action<float> Stunned;
    }
}
