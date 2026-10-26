using System.Collections;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Effects;
using BrokenSovereign.Sound;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 하위 신 이랑진군 보스 패턴 (10/25 페이즈 개편).
    /// 평소에는 EnemyController 기본 공격으로 싸우고, 교전 중 페이즈 간격마다 패턴을 고름 (BossPhaseProfile).
    /// - 페이즈: 체력 비율로 1(100~70%) → 2(70~35%) → 3(35%~). 바뀔 때 무적·회색 체력바·번개·배너 (TransitionTime 초)
    /// - 패턴 선택: 현재 페이즈 후보 중 재사용 대기 끝 + 거리 조건 + 직전 패턴 제외, 가중치 무작위. 3페이즈는 2개 연속
    /// 패턴: 1 삼첨양인도 3연 베기 / 2 제3의 눈 광선 / 3 도약 내려찍기(2페이즈~) / 4 효천견 돌진(3페이즈~)
    /// 패턴 중 기절(분쇄 무력화)하면 즉시 중단.
    /// 10/26 이펙트: 광선 = 눈빛 모임 → 출렁이는 겹 빛줄기 (ErlangVfx.Beam).
    ///   내려찍기 = 착지 후 원 표시 → 1초 뒤 번개가 떨어지고 충격파가 원 끝까지 퍼짐 (파동이 닿을 때 피해)
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    [RequireComponent(typeof(Health))]
    public class ErlangBoss : MonoBehaviour
    {
        [Tooltip("페이즈·패턴 데이터. 비우면 기본값 (BossPhaseProfile.CreateDefault)")]
        [SerializeField] private BossPhaseProfile _profile;
        [Tooltip("교전 시작 후 첫 패턴까지 (초)")]
        [SerializeField, Min(0f)] private float _firstPatternDelay = 4f;

        [Header("1. 삼첨양인도 3연 베기")]
        [SerializeField, Min(0.5f)] private float _slashRange = 4.5f;
        [SerializeField, Range(30f, 360f)] private float _slashAngle = 120f;
        [SerializeField, Min(0.1f)] private float _slashWindup = 0.65f;
        [SerializeField, Min(0f)] private float _slashStep = 1.2f;
        [SerializeField, Min(0f)] private float _slashCoefficient = 1.3f;

        [Header("2. 제3의 눈 광선")]
        [SerializeField, Min(1f)] private float _beamLength = 16f;
        [SerializeField, Min(0.5f)] private float _beamWidth = 2.2f;
        [SerializeField, Min(0.1f)] private float _beamWindup = 1.3f;
        [SerializeField, Min(0f)] private float _beamCoefficient = 2.4f;
        [SerializeField] private float _eyeHeight = 2.25f;
        [SerializeField] private Color _beamColor = new Color(1f, 0.85f, 0.35f, 1f);

        [Header("3. 도약 내려찍기")]
        [SerializeField, Min(0.1f)] private float _leapWindup = 0.9f;
        [SerializeField, Min(0.1f)] private float _leapAirTime = 0.55f;
        [SerializeField, Min(0f)] private float _leapHeight = 5f;
        [SerializeField, Min(0.5f)] private float _leapRadius = 4.5f;
        [SerializeField, Min(0f)] private float _leapCoefficient = 2f;
        [Tooltip("착지 후 원 표시 → 번개까지 (초)")]
        [SerializeField, Min(0f)] private float _leapStrikeDelay = 1f;
        [Tooltip("번개 뒤 충격파가 원 끝까지 퍼지는 시간 (초)")]
        [SerializeField, Min(0.05f)] private float _leapWaveTime = 0.4f;

        [Header("4. 효천견 돌진")]
        [SerializeField, Min(1)] private int _houndCount = 3;
        [SerializeField, Range(0f, 60f)] private float _houndSpread = 22f;
        [SerializeField, Min(1f)] private float _houndLength = 18f;
        [SerializeField, Min(0.5f)] private float _houndWidth = 1.6f;
        [SerializeField, Min(0.1f)] private float _houndWindup = 1f;
        [SerializeField, Min(0.05f)] private float _houndRunTime = 0.35f;
        [SerializeField, Min(0f)] private float _houndCoefficient = 1.6f;
        [SerializeField] private Color _houndColor = new Color(0.55f, 0.75f, 1f, 1f);

        [Header("공통")]
        [SerializeField, Min(0f)] private float _recovery = 0.7f;
        [SerializeField] private Color _warningColor = new Color(1f, 0.15f, 0.1f, 0.35f);
        [SerializeField] private Color _lightningColor = new Color(0.75f, 0.88f, 1f);

        private EnemyController _controller;
        private EnemyAnimator _animator;
        private Health _health;
        private BossStatus _status;
        private float _engagedSince = -1f;
        private float _nextPatternTime;
        private bool _running;
        private Coroutine _chain;
        private bool _transitioning;
        private int _phase;
        private ErlangPattern? _lastPattern;
        private readonly Dictionary<ErlangPattern, float> _readyAt = new Dictionary<ErlangPattern, float>();
        private readonly List<BossPatternRule> _candidates = new List<BossPatternRule>();

        public bool IsUsingPattern => _running;
        public int Phase => _phase;
        public bool IsEnraged => _phase >= 1;
        private BossPhase CurrentPhase => _profile.Phases.Length > 0 ? _profile.Phases[Mathf.Clamp(_phase, 0, _profile.Phases.Length - 1)] : null;
        private float WindupScale => CurrentPhase != null ? CurrentPhase.WindupScale : 1f;

        private void Awake()
        {
            _controller = GetComponent<EnemyController>();
            _animator = GetComponent<EnemyAnimator>();
            _health = GetComponent<Health>();
            _status = GetComponent<BossStatus>();

            if (_profile == null || _profile.Phases == null || _profile.Phases.Length == 0)
            {
                _profile = BossPhaseProfile.CreateDefault();
            }

            // HUD 체력바 위 페이즈 표식 (1페이즈 제외, 각 페이즈 시작 비율)
            if (_status != null)
            {
                var ratios = new float[Mathf.Max(0, _profile.Phases.Length - 1)];

                for (int i = 0; i < ratios.Length; i++)
                {
                    ratios[i] = _profile.Phases[i + 1].StartRatio;
                }

                _status.SetPhaseRatios(ratios);
            }
        }

        private void OnEnable()
        {
            _health.Restored += OnRestored;
        }

        private void OnDisable()
        {
            _health.Restored -= OnRestored;
            StopAllCoroutines();
            EndPattern();
            _transitioning = false;
        }

        private void Update()
        {
            if (_health.IsDead)
            {
                if (_running)
                {
                    StopAllCoroutines();
                    EndPattern();
                }

                return;
            }

            if (_transitioning)
            {
                return;
            }

            // 페이즈 전환: 체력이 다음 페이즈 기준을 넘으면 패턴을 끊고 전환 연출
            int phase = _profile.PhaseAt(_health.Max > 0f ? _health.Current / _health.Max : 1f);

            if (phase > _phase)
            {
                StopAllCoroutines();
                EndPattern();
                StartCoroutine(PhaseTransition(phase));
                return;
            }

            bool engaged = _controller.State == EnemyState.Chase || _controller.State == EnemyState.Attack;

            if (!engaged)
            {
                _engagedSince = -1f;
                return;
            }

            if (_engagedSince < 0f)
            {
                _engagedSince = Time.time;
                _nextPatternTime = Time.time + _firstPatternDelay;
            }

            // 안전장치: 패턴 코루틴 없이 패턴 상태만 남아 있으면 해제 (10/26)
            if (_running && _chain == null)
            {
                EndPattern();
            }

            // 기본 공격 중·기절 중·간격 전이면 기다림
            if (_running || Time.time < _nextPatternTime || _controller.State == EnemyState.Attack || _controller.IsStunned)
            {
                return;
            }

            if (_controller.Target == null || _controller.TargetHealth == null || _controller.TargetHealth.IsDead)
            {
                return;
            }

            _chain = StartCoroutine(RunChain());
        }

        // ------------------------------------------------------------------
        // 페이즈·패턴 선택
        // ------------------------------------------------------------------

        private IEnumerator PhaseTransition(int phase)
        {
            _transitioning = true;
            _phase = phase;
            _controller.ExternalControl = true;
            _controller.StopMoving();

            if (_status != null)
            {
                _status.IsIntro = true;   // 무적 + 회색 체력바
                _status.NotifyPhase(phase, CurrentPhase?.Title, CurrentPhase?.Subtitle);
            }

            Vector3 at = transform.position;
            AttackAreaDebugDrawer.ShowCircle(at, 5f, at.y, new Color(_lightningColor.r, _lightningColor.g, _lightningColor.b, 0.5f), _profile.TransitionTime);
            LightningStrike.Spawn(at, _lightningColor, 0.5f);

            if (_animator != null)
            {
                _animator.PlayAttack(0.8f);
            }

            yield return new WaitForSeconds(_profile.TransitionTime * 0.5f);
            LightningStrike.Spawn(at, _lightningColor, 0.35f, 1f);
            yield return new WaitForSeconds(_profile.TransitionTime * 0.5f);

            if (_status != null)
            {
                _status.IsIntro = false;
            }

            _controller.ExternalControl = false;
            _transitioning = false;
            _nextPatternTime = Time.time + 1f;   // 전환 직후 바로 새 패턴
            _readyAt.Clear();
        }

        private IEnumerator RunChain()
        {
            int chain = CurrentPhase != null ? Mathf.Max(1, CurrentPhase.Chain) : 1;

            for (int i = 0; i < chain; i++)
            {
                BossPatternRule rule = PickPattern();

                if (rule == null)
                {
                    break;
                }

                _lastPattern = rule.Pattern;
                _readyAt[rule.Pattern] = Time.time + rule.Cooldown;
                yield return StartCoroutine(Run(rule.Pattern));

                // 10/26: 기절·사망으로 끊겨도 반드시 EndPattern (예전엔 yield break로 빠져나가
                // _running·ExternalControl이 켜진 채 남아 보스가 제자리에 멈춰 있던 버그)
                if (Interrupted() || _transitioning)
                {
                    break;
                }
            }

            _chain = null;
            EndPattern();
        }

        // 후보 = 현재 페이즈 패턴 중 재사용 대기 끝 + 거리 조건 + 직전 패턴 아님(후보가 하나뿐이면 허용). 가중치 무작위
        private BossPatternRule PickPattern()
        {
            BossPhase phase = CurrentPhase;
            Transform target = _controller.Target;

            if (phase == null || target == null)
            {
                return null;
            }

            float distance = FlatDistance(transform.position, target.position);
            _candidates.Clear();
            float total = 0f;

            foreach (BossPatternRule rule in phase.Patterns)
            {
                bool ready = !_readyAt.TryGetValue(rule.Pattern, out float at) || Time.time >= at;

                if (ready && rule.Weight > 0f && distance >= rule.MinRange && distance <= rule.MaxRange)
                {
                    _candidates.Add(rule);
                }
            }

            if (_candidates.Count > 1 && _lastPattern.HasValue)
            {
                _candidates.RemoveAll(r => r.Pattern == _lastPattern.Value);
            }

            foreach (BossPatternRule rule in _candidates)
            {
                total += rule.Weight;
            }

            float roll = Random.value * total;

            foreach (BossPatternRule rule in _candidates)
            {
                roll -= rule.Weight;

                if (roll <= 0f)
                {
                    return rule;
                }
            }

            return _candidates.Count > 0 ? _candidates[_candidates.Count - 1] : null;
        }

        private IEnumerator Run(ErlangPattern pattern)
        {
            switch (pattern)
            {
                case ErlangPattern.TripleSlash: return TripleSlash();
                case ErlangPattern.EyeBeam: return EyeBeam();
                case ErlangPattern.LeapSlam: return LeapSlam();
                default: return HoundCharge();
            }
        }

        // ------------------------------------------------------------------
        // 패턴
        // ------------------------------------------------------------------

        private IEnumerator TripleSlash()
        {
            BeginPattern();
            float windup = _slashWindup * WindupScale;

            for (int i = 0; i < 3; i++)
            {
                Vector3 forward = FaceTarget();

                if (_slashStep > 0f && AgentReady())
                {
                    _controller.Agent.Move(forward * _slashStep);
                }

                AttackAreaDebugDrawer.ShowCone(transform.position, forward, _slashRange, _slashAngle,
                    transform.position.y, _warningColor, windup);
                PlayAttack(windup + 0.2f);
                yield return new WaitForSeconds(windup);

                if (Interrupted())
                {
                    yield break;
                }

                SoundPlayer.PlayAt(SoundId.BOSS_SLASH, transform.position);
                PlayEffect(EffectLibrary.Instance?.BossSlash, transform.position + forward * (_slashRange * 0.5f) + Vector3.up, 1.6f, forward);
                Transform target = _controller.Target;

                if (target != null && AttackArea.IsInCone(transform.position, forward, _slashRange, _slashAngle, target.position))
                {
                    Hit(_slashCoefficient);
                }

                yield return new WaitForSeconds(0.2f);
            }

            yield return new WaitForSeconds(_recovery);
        }

        private IEnumerator EyeBeam()
        {
            BeginPattern();
            float windup = _beamWindup * WindupScale;
            Vector3 forward = FaceTarget();
            Vector3 origin = transform.position;
            AttackAreaDebugDrawer.ShowLine(origin, forward, _beamLength, _beamWidth, origin.y, _warningColor, windup);
            ErlangVfx.EyeCharge(transform, _eyeHeight, _beamColor, windup);
            PlayAttack(windup + 0.3f);
            yield return new WaitForSeconds(windup);

            if (Interrupted())
            {
                yield break;
            }

            SoundPlayer.PlayAt(SoundId.BOSS_BEAM, origin);
            PlayEffect(EffectLibrary.Instance?.BossBeam, origin + Vector3.up * _eyeHeight + forward * 0.6f, 1.4f, forward);
            PlayEffect(EffectLibrary.Instance?.BossBeam, origin + forward * _beamLength + Vector3.up * 0.5f, 1.2f, forward);
            ErlangVfx.Beam(origin + Vector3.up * _eyeHeight + forward * 0.4f, origin + forward * _beamLength + Vector3.up * 0.3f,
                _beamWidth * 0.7f, _beamColor, 0.55f);
            Transform target = _controller.Target;

            if (target != null && InLine(origin, forward, _beamLength, _beamWidth, target.position))
            {
                Hit(_beamCoefficient);
            }

            yield return new WaitForSeconds(_recovery + 0.3f);
        }

        // 3. 도약 내려찍기: 대상 자리에 원 표시 → 뛰어올라 포물선으로 내려앉음
        //    → 착지 자리에 원 표시 → 1초 뒤 번개 + 원 끝까지 퍼지는 충격파 (파동이 닿으면 피해)
        private IEnumerator LeapSlam()
        {
            BeginPattern();
            Transform target = _controller.Target;

            if (target == null)
            {
                yield break;
            }

            FaceTarget();
            Vector3 landing = target.position;

            if (NavMesh.SamplePosition(landing, out NavMeshHit hit, 3f, NavMesh.AllAreas))
            {
                landing = hit.position;
            }

            float windup = _leapWindup * WindupScale;
            AttackAreaDebugDrawer.ShowCircle(landing, _leapRadius, landing.y, _warningColor, windup + _leapAirTime);
            PlayAttack(windup + _leapAirTime + 0.3f);
            yield return new WaitForSeconds(windup);

            if (Interrupted())
            {
                yield break;
            }

            // 비행: 에이전트를 잠시 끄고 포물선 이동
            NavMeshAgent agent = _controller.Agent;
            bool hadAgent = agent != null && agent.enabled;

            if (hadAgent)
            {
                agent.enabled = false;
            }

            Vector3 start = transform.position;

            for (float t = 0f; t < 1f; t += Time.deltaTime / _leapAirTime)
            {
                Vector3 p = Vector3.Lerp(start, landing, t);
                p.y += Mathf.Sin(t * Mathf.PI) * _leapHeight;
                transform.position = p;
                yield return null;
            }

            transform.position = landing;

            if (hadAgent)
            {
                agent.enabled = true;
                agent.Warp(landing);
            }

            SoundPlayer.PlayAt(SoundId.BOSS_LEAP, landing);
            PlayEffect(EffectLibrary.Instance?.BossLeap, landing, EffectLibrary.Instance != null ? EffectLibrary.Instance.BossLeapScale : 2f, transform.forward);

            // 착지 → 원 표시 → 1초 뒤 번개
            AttackAreaDebugDrawer.ShowCircle(landing, _leapRadius, landing.y, _warningColor, _leapStrikeDelay);
            PlayAttack(_leapStrikeDelay + 0.3f);
            yield return new WaitForSeconds(_leapStrikeDelay);

            if (Interrupted())
            {
                yield break;
            }

            LightningStrike.Spawn(landing, _lightningColor, 0.35f, 1.2f);
            ErlangVfx.Shockwave(landing, _leapRadius, _lightningColor, _leapWaveTime);
            bool struck = false;

            // 충격파가 퍼지며 닿는 순간 피해 (한 번만)
            for (float t = 0f; t < 1f && !struck; t += Time.deltaTime / _leapWaveTime)
            {
                target = _controller.Target;
                float front = _leapRadius * (1f - (1f - t) * (1f - t));

                if (target != null && FlatDistance(landing, target.position) <= Mathf.Min(_leapRadius, front + 0.3f))
                {
                    struck = true;
                    Hit(_leapCoefficient);
                }

                yield return null;
            }

            yield return new WaitForSeconds(_recovery + 0.2f);
        }

        // 4. 효천견 돌진: 대상 쪽 부채꼴로 직선 3줄 표시 → 분신 개가 빛줄기로 달려 나감 → 줄 위 피해 (한 번만)
        private IEnumerator HoundCharge()
        {
            BeginPattern();
            Vector3 forward = FaceTarget();
            Vector3 origin = transform.position;
            float windup = _houndWindup * WindupScale;
            var directions = new List<Vector3>();

            for (int i = 0; i < _houndCount; i++)
            {
                float angle = _houndCount == 1 ? 0f : Mathf.Lerp(-_houndSpread, _houndSpread, (float)i / (_houndCount - 1));
                Vector3 dir = Quaternion.Euler(0f, angle, 0f) * forward;
                directions.Add(dir);
                AttackAreaDebugDrawer.ShowLine(origin, dir, _houndLength, _houndWidth, origin.y, _warningColor, windup + _houndRunTime);
            }

            PlayAttack(windup + 0.3f);
            yield return new WaitForSeconds(windup);

            if (Interrupted())
            {
                yield break;
            }

            SoundPlayer.PlayAt(SoundId.BOSS_HOUND, origin);

            foreach (Vector3 dir in directions)
            {
                PlayEffect(EffectLibrary.Instance?.BossHound, origin + dir * 2f + Vector3.up * 0.6f, 1.3f, dir);
            }

            bool hitOnce = false;

            for (float t = 0f; t < 1f; t += Time.deltaTime / _houndRunTime)
            {
                foreach (Vector3 dir in directions)
                {
                    Vector3 head = origin + dir * (_houndLength * t) + Vector3.up * 0.8f;
                    ShowStreak(head - dir * 2.5f, head, 0.2f, 0.9f, _houndColor, 0.06f);
                }

                Transform target = _controller.Target;

                if (!hitOnce && target != null)
                {
                    foreach (Vector3 dir in directions)
                    {
                        if (InLine(origin, dir, _houndLength * t, _houndWidth, target.position))
                        {
                            hitOnce = true;
                            Hit(_houndCoefficient);
                            break;
                        }
                    }
                }

                yield return null;
            }

            yield return new WaitForSeconds(_recovery);
        }

        // ------------------------------------------------------------------
        // 공용
        // ------------------------------------------------------------------

        private void BeginPattern()
        {
            _running = true;
            _controller.ExternalControl = true;
            _controller.StopMoving();
        }

        private void EndPattern()
        {
            _running = false;
            _chain = null;

            // 도약 비행 중에 끊기면 에이전트가 꺼진 채 남으므로 다시 켜서 현재 위치에 붙임
            NavMeshAgent agent = _controller != null ? _controller.Agent : null;

            if (agent != null && !agent.enabled)
            {
                agent.enabled = true;

                if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 4f, NavMesh.AllAreas))
                {
                    agent.Warp(hit.position);
                }
            }

            if (_controller != null)
            {
                _controller.ExternalControl = false;
            }

            float interval = CurrentPhase != null ? CurrentPhase.Interval : 7f;
            _nextPatternTime = Time.time + interval;
        }

        private bool Interrupted()
        {
            return _health.IsDead || _controller.IsStunned;
        }

        private bool AgentReady()
        {
            return _controller.Agent != null && _controller.Agent.enabled && _controller.Agent.isOnNavMesh;
        }

        // 10/26: 패턴 이펙트 (EffectLibrary 칸이 비어 있으면 없음)
        private static void PlayEffect(GameObject prefab, Vector3 position, float scale, Vector3 forward)
        {
            forward.y = 0f;
            EffectPlayer.Play(prefab, position, scale, forward.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(forward) : (Quaternion?)null);
        }

        private void PlayAttack(float duration)
        {
            if (_animator != null)
            {
                _animator.PlayAttack(duration);
            }
        }

        private Vector3 FaceTarget()
        {
            Transform target = _controller.Target;
            Vector3 forward = target != null ? target.position - transform.position : transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.0001f ? forward.normalized : transform.forward;
            transform.rotation = Quaternion.LookRotation(forward);
            return forward;
        }

        private void Hit(float coefficient)
        {
            Health target = _controller.TargetHealth;

            if (target == null || target.IsDead || _controller.Definition == null)
            {
                return;
            }

            DamageInfo info = AttackDamage.Build(gameObject, _controller.Stats != null ? _controller.Stats.Sheet : null,
                coefficient, _controller.Definition.FallbackAttackPower, false);
            target.ApplyDamage(info);
        }

        private static bool InLine(Vector3 origin, Vector3 forward, float length, float width, Vector3 point)
        {
            Vector3 offset = point - origin;
            offset.y = 0f;
            float along = Vector3.Dot(offset, forward);

            if (along < 0f || along > length)
            {
                return false;
            }

            float side = (offset - forward * along).magnitude;
            return side <= width * 0.5f;
        }

        // 빛줄기 (광선·효천견 잔상): 잠깐 보이는 선
        private static void ShowStreak(Vector3 from, Vector3 to, float startWidth, float endWidth, Color color, float life)
        {
            var line = new GameObject("ErlangStreak").AddComponent<LineRenderer>();
            line.useWorldSpace = true;
            line.positionCount = 2;
            line.SetPosition(0, from);
            line.SetPosition(1, to);
            line.startWidth = startWidth;
            line.endWidth = endWidth;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;
            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                line.material = new Material(shader);
                Destroy(line.material, life);
            }

            line.startColor = line.endColor = color;
            Destroy(line.gameObject, life);
        }

        private void OnRestored()
        {
            StopAllCoroutines();
            EndPattern();
            _engagedSince = -1f;
            _phase = 0;
            _transitioning = false;
            _lastPattern = null;
            _readyAt.Clear();

            if (_status != null)
            {
                _status.IsIntro = false;
            }
        }

        private static float FlatDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }
}
