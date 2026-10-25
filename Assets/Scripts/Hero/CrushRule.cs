using BrokenSovereign.Combat;
using BrokenSovereign.Enemy;

namespace BrokenSovereign.Hero
{
    /// <summary>
    /// 야로미르 분쇄 규칙 (패시브 「철퇴의 무게」): 플레이어의 모든 피해가 적의 버팀 게이지를 깎음.
    /// 처음 피해를 준 적에게 등급별 버팀 게이지(PoiseReceiver)를 붙이고, 피해 계수 × 비율만큼 감소시킵니다.
    /// </summary>
    public class CrushRule : HeroRule
    {
        private readonly CrushRuleDefinition _definition;

        public CrushRule(HeroRuleContext context, CrushRuleDefinition definition) : base(context)
        {
            _definition = definition;
        }

        public override void Activate()
        {
            Health.AnyDamaged += OnAnyDamaged;
        }

        public override void Deactivate()
        {
            Health.AnyDamaged -= OnAnyDamaged;
        }

        private void OnAnyDamaged(Health victim, DamageInfo info)
        {
            if (victim == null || victim.IsDead || info.Attacker == null || info.Attacker != Context.Player ||
                info.Coefficient <= 0f)
            {
                return;
            }

            if (!victim.TryGetComponent(out EnemyController enemy))
            {
                return;
            }

            // 성물(무쇠 철퇴) 1단계: 버팀 피해·무력화 시간 증가
            HeroRelic relic = Context.Player.GetComponent<HeroRelic>();
            float poiseMultiplier = relic != null ? relic.PoiseMultiplier : 1f;
            float staggerBonus = relic != null ? relic.StaggerBonus : 0f;

            if (!victim.TryGetComponent(out PoiseReceiver receiver))
            {
                receiver = victim.gameObject.AddComponent<PoiseReceiver>();
                EnemyRank rank = enemy.Definition != null ? enemy.Definition.Rank : EnemyRank.Normal;
                receiver.Configure(_definition.MaxPoise(rank), _definition.StaggerDuration(rank) + staggerBonus,
                    _definition.DamageTakenBonus, rank != EnemyRank.Normal, _definition.RecoverDelay,
                    _definition.RecoverRatio, _definition.ImmunityDuration);
            }

            receiver.Hit(info.Coefficient * _definition.PoisePerCoefficient * poiseMultiplier);
        }
    }
}
