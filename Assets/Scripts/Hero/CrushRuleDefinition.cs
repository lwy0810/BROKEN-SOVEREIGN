using BrokenSovereign.Enemy;
using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>야로미르 분쇄 설정: 등급별 버팀 게이지, 감소량, 회복, 무력화 (GDD 5장 초안)</summary>
    [CreateAssetMenu(fileName = "CrushRule", menuName = "BrokenSovereign/Hero Rule/Crush (야로미르)")]
    public class CrushRuleDefinition : HeroRuleDefinition
    {
        [Header("버팀 게이지")]
        [SerializeField, Min(1f)] private float _normalPoise = 30f;
        [SerializeField, Min(1f)] private float _elitePoise = 100f;
        [SerializeField, Min(1f)] private float _bossPoise = 300f;

        [Tooltip("피해 계수 1당 감소량 (기본 공격 1.0 → 10)")]
        [SerializeField, Min(0f)] private float _poisePerCoefficient = 10f;

        [Tooltip("마지막 감소 후 회복 시작까지 (초)")]
        [SerializeField, Min(0f)] private float _recoverDelay = 3f;

        [Tooltip("초당 회복 비율 (0.1 = 최대치의 10%)")]
        [SerializeField, Min(0f)] private float _recoverRatio = 0.1f;

        [Header("무력화")]
        [SerializeField, Min(0f)] private float _staggerDuration = 3f;
        [SerializeField, Min(0f)] private float _bossStaggerDuration = 2f;

        [Tooltip("무력화 중 받는 피해 증가 (0.25 = 25%)")]
        [SerializeField, Min(0f)] private float _damageTakenBonus = 0.25f;

        [Tooltip("무력화가 끝난 뒤 다시 무력화되지 않는 시간 (초)")]
        [SerializeField, Min(0f)] private float _immunityDuration = 10f;

        public float PoisePerCoefficient => _poisePerCoefficient;
        public float RecoverDelay => _recoverDelay;
        public float RecoverRatio => _recoverRatio;
        public float DamageTakenBonus => _damageTakenBonus;
        public float ImmunityDuration => _immunityDuration;

        public float MaxPoise(EnemyRank rank)
        {
            switch (rank)
            {
                case EnemyRank.Elite:
                    return _elitePoise;
                case EnemyRank.Boss:
                    return _bossPoise;
                default:
                    return _normalPoise;
            }
        }

        public float StaggerDuration(EnemyRank rank)
        {
            return rank == EnemyRank.Boss ? _bossStaggerDuration : _staggerDuration;
        }

        public override HeroRule CreateRule(HeroRuleContext context)
        {
            return new CrushRule(context, this);
        }
    }
}
