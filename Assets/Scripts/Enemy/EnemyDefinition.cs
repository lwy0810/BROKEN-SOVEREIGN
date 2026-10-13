using UnityEngine;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 적 AI 설정 (감지·추적·공격). 체력·공격력 등 능력치는 CharacterStatsData 에셋에서 관리합니다.
    /// 같은 종류의 적은 같은 에셋을 공유합니다.
    /// </summary>
    [CreateAssetMenu(fileName = "EnemyDefinition", menuName = "BrokenSovereign/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [Header("감지·추적")]
        [Tooltip("이 거리(m) 안에 대상이 들어오면 추적 시작")]
        [SerializeField, Min(0f)] private float _detectRange = 8f;

        [Tooltip("추적 포기 거리 = 감지 거리 × 이 값")]
        [SerializeField, Min(1f)] private float _loseRangeMultiplier = 2f;

        [Tooltip("시작 위치에서 이 거리(m)보다 멀리 끌려가면 추적 포기")]
        [SerializeField, Min(0f)] private float _leashDistance = 20f;

        [Tooltip("시작 위치 도착 판정 거리(m)")]
        [SerializeField, Min(0.1f)] private float _homeArriveDistance = 0.5f;

        [Tooltip("추적 경로 갱신 간격(초)")]
        [SerializeField, Min(0.05f)] private float _repathInterval = 0.2f;

        [Tooltip("공격 대기·예비 동작 중 회전 속도(도/초)")]
        [SerializeField, Min(0f)] private float _turnSpeed = 540f;

        [Header("공격")]
        [SerializeField, Min(0.1f)] private float _attackRange = 1.8f;

        [Tooltip("타격 판정 각도 (전방 부채꼴)")]
        [SerializeField, Range(10f, 360f)] private float _attackAngle = 90f;

        [Tooltip("예비 동작 시간(초). 범위 표시 후 이 시간이 지나면 타격")]
        [SerializeField, Min(0f)] private float _attackWindup = 0.5f;

        [Tooltip("타격 후 멈춰 있는 시간(초)")]
        [SerializeField, Min(0f)] private float _attackRecovery = 0.4f;

        [Tooltip("공격 시작 간격(초)")]
        [SerializeField, Min(0.1f)] private float _attackInterval = 2f;

        [Tooltip("피해 계수")]
        [SerializeField, Min(0f)] private float _attackCoefficient = 1f;

        [Tooltip("CharacterStats가 없을 때 사용하는 공격력")]
        [SerializeField, Min(0f)] private float _fallbackAttackPower = 40f;

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showAttackArea = true;
        [SerializeField] private Color _attackAreaColor = new Color(1f, 0.1f, 0.1f, 0.35f);

        public float DetectRange => _detectRange;
        public float LoseRange => _detectRange * _loseRangeMultiplier;
        public float LeashDistance => _leashDistance;
        public float HomeArriveDistance => _homeArriveDistance;
        public float RepathInterval => _repathInterval;
        public float TurnSpeed => _turnSpeed;
        public float AttackRange => _attackRange;
        public float AttackAngle => _attackAngle;
        public float AttackWindup => _attackWindup;
        public float AttackRecovery => _attackRecovery;
        public float AttackInterval => _attackInterval;
        public float AttackCoefficient => _attackCoefficient;
        public float FallbackAttackPower => _fallbackAttackPower;
        public bool ShowAttackArea => _showAttackArea;
        public Color AttackAreaColor => _attackAreaColor;

        public EnemyBrain.Settings ToBrainSettings()
        {
            return new EnemyBrain.Settings(DetectRange, LoseRange, _leashDistance, _attackRange, _homeArriveDistance);
        }
    }
}
