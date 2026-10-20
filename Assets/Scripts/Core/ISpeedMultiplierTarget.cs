namespace BrokenSovereign.Core
{
    /// <summary>이동 속도 배율을 받는 대상 (ClickMover). 난천무 이동 속도 감소 등</summary>
    public interface ISpeedMultiplierTarget
    {
        void SetSpeedMultiplier(float multiplier);
    }
}
