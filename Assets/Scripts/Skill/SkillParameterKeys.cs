namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 수치 키 이름. 에셋·코드·분기 수정치가 같은 이름을 쓰도록 여기서만 정의합니다.
    /// </summary>
    public static class SkillParameterKeys
    {
        /// <summary>피해 계수 (1.5 = 공격력의 150%)</summary>
        public const string COEFFICIENT = "coefficient";

        /// <summary>범위 반경 (m)</summary>
        public const string RADIUS = "radius";

        /// <summary>부채꼴 각도 (도)</summary>
        public const string ANGLE = "angle";

        /// <summary>이동 거리 (m, 돌진 등)</summary>
        public const string DISTANCE = "distance";

        /// <summary>지속 시간 (초, 돌진·버프)</summary>
        public const string DURATION = "duration";

        /// <summary>경로 판정 반경 (m)</summary>
        public const string HIT_RADIUS = "hitRadius";

        /// <summary>기절 시간 (초)</summary>
        public const string STUN_DURATION = "stunDuration";

        /// <summary>시전 사거리(m): 대상·커서 지점까지 이 거리 안에 들어오면 시전. 없으면 반경 × 0.8 (SkillRange)</summary>
        public const string CAST_RANGE = "castRange";

        /// <summary>받는 피해 감소 (0.3 = 30%)</summary>
        public const string DAMAGE_REDUCTION = "damageReduction";

        /// <summary>피격 시 직업 게이지 충전량 증가 (1 = +100%, 2배)</summary>
        public const string GAUGE_GAIN_ON_DAMAGED = "gaugeGainOnDamaged";

        /// <summary>연속 파동 수 (격진)</summary>
        public const string WAVE_COUNT = "waveCount";

        /// <summary>파동 간격 (초)</summary>
        public const string WAVE_INTERVAL = "waveInterval";

        /// <summary>파동마다 늘어나는 반경 (m)</summary>
        public const string RADIUS_STEP = "radiusStep";

        /// <summary>대상 공격력 감소율 (0.2 = 20% 감소, 디버프)</summary>
        public const string ATTACK_POWER_REDUCTION = "attackPowerReduction";

        /// <summary>대상 방어력 감소율 (0.2 = 20% 감소, 디버프)</summary>
        public const string ARMOR_REDUCTION = "armorReduction";

        /// <summary>띄우기 시간 (초). 공중에 있는 동안 행동 불능</summary>
        public const string AIRBORNE_DURATION = "airborneDuration";

        /// <summary>지속 시전의 반복 타격 간격 (초, 난천무)</summary>
        public const string TICK_INTERVAL = "tickInterval";

        /// <summary>지속 시전 중 이동 속도 배율 (0.8 = 20% 감소)</summary>
        public const string MOVE_SPEED_MULTIPLIER = "moveSpeedMultiplier";

        /// <summary>지속 시전 유지 비용 (초당 자원)</summary>
        public const string COST_PER_SECOND = "costPerSecond";

        /// <summary>시작 후 유지 비용이 없는 시간 (초, 난천무 충만)</summary>
        public const string FREE_DURATION = "freeDuration";

        /// <summary>마무리 타격 계수 (난천무 충만 종료 내려찍기). 0이면 없음</summary>
        public const string FINISH_COEFFICIENT = "finishCoefficient";

        /// <summary>마무리 타격 반경 (m)</summary>
        public const string FINISH_RADIUS = "finishRadius";

        /// <summary>처형 계수 (분쇄 일격: 무력화된 대상)</summary>
        public const string EXECUTE_COEFFICIENT = "executeCoefficient";

        /// <summary>처형 충격파 반경 (m)</summary>
        public const string SHOCKWAVE_RADIUS = "shockwaveRadius";

        /// <summary>처형 충격파 계수</summary>
        public const string SHOCKWAVE_COEFFICIENT = "shockwaveCoefficient";

        /// <summary>적중 시 버팀 게이지 즉시 감소 비율 (0.5 = 50%, 분쇄 일격 충만)</summary>
        public const string POISE_BREAK_RATIO = "poiseBreakRatio";

        /// <summary>확산(강화 분기 A, 10/26): 대상 주변에서 함께 맞힐 적 수</summary>
        public const string SPREAD_COUNT = "spreadCount";

        /// <summary>확산: 대상 주변 탐색 반경 (m)</summary>
        public const string SPREAD_RADIUS = "spreadRadius";

        /// <summary>확산: 맞은 적들이 나눠 받는 총 피해 배율 (1.6 = 160%를 맞은 수로 나눔, 한 명당 최대 100%)</summary>
        public const string SPREAD_TOTAL = "spreadTotal";
    }
}
