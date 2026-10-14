namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 띄우기를 받을 수 있는 대상. 공중에 있는 동안 행동 불능 (기절과 같은 처리) + 모델만 들어 올림.
    /// 보스 저항·무력화 연계는 구현 쪽에서 처리합니다.
    /// </summary>
    public interface IAirborne
    {
        bool IsAirborne { get; }

        void Launch(float duration);
    }
}
