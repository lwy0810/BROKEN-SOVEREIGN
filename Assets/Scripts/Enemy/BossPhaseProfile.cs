using System;
using UnityEngine;

namespace BrokenSovereign.Enemy
{
    /// <summary>이랑진군 패턴 종류 (ErlangBoss가 실행)</summary>
    public enum ErlangPattern
    {
        TripleSlash,   // 삼첨양인도 3연 베기
        EyeBeam,       // 제3의 눈 광선
        LeapSlam,      // 도약 내려찍기 (2페이즈~)
        HoundCharge    // 효천견 돌진 (3페이즈~)
    }

    /// <summary>패턴 한 개의 선택 조건: 가중치·재사용 대기·사용 거리</summary>
    [Serializable]
    public class BossPatternRule
    {
        public ErlangPattern Pattern;
        [Min(0f)] public float Weight = 1f;
        [Tooltip("이 패턴을 다시 쓰기까지 (초)")]
        [Min(0f)] public float Cooldown = 8f;
        [Tooltip("대상과의 거리가 이 범위 안일 때만 (m)")]
        [Min(0f)] public float MinRange;
        [Min(0f)] public float MaxRange = 14f;
    }

    /// <summary>페이즈 한 개: 시작 체력 비율·패턴 간격·연속 횟수·쓸 수 있는 패턴</summary>
    [Serializable]
    public class BossPhase
    {
        public string Title = "1페이즈";
        public string Subtitle = string.Empty;
        [Tooltip("체력 비율이 이 값 이하가 되면 이 페이즈 (1페이즈 = 1)")]
        [Range(0f, 1f)] public float StartRatio = 1f;
        [Tooltip("패턴이 끝난 뒤 다음 패턴까지 (초)")]
        [Min(0f)] public float Interval = 7f;
        [Tooltip("한 번에 이어서 쓰는 패턴 수")]
        [Min(1)] public int Chain = 1;
        [Tooltip("예비 동작 시간 배율 (작을수록 빠름)")]
        [Range(0.3f, 1.5f)] public float WindupScale = 1f;
        public BossPatternRule[] Patterns = Array.Empty<BossPatternRule>();
    }

    /// <summary>
    /// 보스 페이즈 데이터 (10/25). 체력 비율로 페이즈가 바뀌고, 페이즈마다 패턴 후보·간격·연속 횟수가 다름.
    /// 패턴 선택 = 후보 중 (재사용 대기 끝 + 거리 조건 + 직전 패턴 아님) 가중치 무작위. 다른 보스도 같은 구조로 재사용.
    /// 에셋이 없으면 ErlangBoss가 CreateDefault()를 씀.
    /// </summary>
    [CreateAssetMenu(fileName = "BossPhaseProfile", menuName = "BrokenSovereign/Boss Phase Profile")]
    public class BossPhaseProfile : ScriptableObject
    {
        [Tooltip("페이즈 전환 연출 (무적·번개·배너) 시간 (초)")]
        [Min(0f)] public float TransitionTime = 2f;
        public BossPhase[] Phases = Array.Empty<BossPhase>();

        /// <summary>체력 비율에 맞는 페이즈 번호 (0부터)</summary>
        public int PhaseAt(float healthRatio)
        {
            int phase = 0;

            for (int i = 0; i < Phases.Length; i++)
            {
                if (healthRatio <= Phases[i].StartRatio)
                {
                    phase = i;
                }
            }

            return phase;
        }

        /// <summary>이랑진군 기본값: 3페이즈 (100~70% / 70~35% / 35%~)</summary>
        public static BossPhaseProfile CreateDefault()
        {
            var profile = CreateInstance<BossPhaseProfile>();
            profile.name = "ErlangShenPhasesDefault";
            profile.Phases = new[]
            {
                new BossPhase
                {
                    Title = "1페이즈", Subtitle = "천계의 무신", StartRatio = 1f, Interval = 7f, Chain = 1, WindupScale = 1f,
                    Patterns = new[]
                    {
                        Rule(ErlangPattern.TripleSlash, 1f, 6f, 0f, 6f),
                        Rule(ErlangPattern.EyeBeam, 1f, 8f, 3f, 16f),
                    }
                },
                new BossPhase
                {
                    Title = "2페이즈", Subtitle = "진군의 노여움", StartRatio = 0.7f, Interval = 5.5f, Chain = 1, WindupScale = 0.85f,
                    Patterns = new[]
                    {
                        Rule(ErlangPattern.TripleSlash, 1f, 6f, 0f, 6f),
                        Rule(ErlangPattern.EyeBeam, 1f, 8f, 3f, 16f),
                        Rule(ErlangPattern.LeapSlam, 1.4f, 9f, 5f, 18f),
                    }
                },
                new BossPhase
                {
                    Title = "3페이즈", Subtitle = "효천견을 부르다", StartRatio = 0.35f, Interval = 4f, Chain = 2, WindupScale = 0.75f,
                    Patterns = new[]
                    {
                        Rule(ErlangPattern.TripleSlash, 1f, 5f, 0f, 6f),
                        Rule(ErlangPattern.EyeBeam, 0.8f, 7f, 3f, 16f),
                        Rule(ErlangPattern.LeapSlam, 1.2f, 8f, 5f, 18f),
                        Rule(ErlangPattern.HoundCharge, 1.5f, 10f, 2f, 16f),
                    }
                },
            };
            return profile;
        }

        private static BossPatternRule Rule(ErlangPattern pattern, float weight, float cooldown, float min, float max)
        {
            return new BossPatternRule { Pattern = pattern, Weight = weight, Cooldown = cooldown, MinRange = min, MaxRange = max };
        }
    }
}
