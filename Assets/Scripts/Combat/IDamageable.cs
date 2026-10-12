namespace BrokenSovereign.Combat
{
    /// <summary>
    /// 피해를 받을 수 있는 대상 (플레이어, 적, 파괴 가능 오브젝트).
    /// </summary>
    public interface IDamageable
    {
        bool IsDead { get; }

        void ApplyDamage(DamageInfo info);
    }
}
