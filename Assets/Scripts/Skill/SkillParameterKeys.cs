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
    }
}
