using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using BrokenSovereign.Player;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrokenSovereign.Effects
{
    /// <summary>
    /// 게임 사건 → 이펙트 연결 (10/26, GameSounds와 같은 방식). 게임 시작 시 자동 생성, 씬 전환에도 유지.
    /// - 기술 시전: SkillDefinition의 시전 이펙트 (Player = 발밑, Target = 커서·대상 지점, Follow = 따라다님)
    /// - 플레이어가 준 피해: 1.5초 안에 쓴 기술(기본 공격 제외)의 타격 이펙트, 없으면 기본 타격 / 치명타 이펙트
    /// - 일반 적 사망, 보스 페이즈 전환, 회피 바람 (EffectLibrary). 10/26: 물약 빛 이펙트 제거, 회피 바람 추가
    /// 보스 패턴(삼연참·광선·도약·효천견)은 ErlangBoss가 EffectPlayer를 직접 호출합니다.
    /// </summary>
    public class GameEffects : MonoBehaviour
    {
        private const float SKILL_HIT_WINDOW = 1.5f;
        private const float HIT_HEIGHT = 1.1f;

        private PlayerController _player;
        private SkillCaster _caster;
        private DodgeAction _dodge;
        private Health _playerHealth;
        private float _nextPlayerSearch;

        private SkillDefinition _lastSkill;
        private SkillSlot _lastSlot;
        private float _lastSkillTime = -10f;

        // 시전 이벤트(SkillCast)가 지점 이벤트(SkillUsed)보다 먼저 와서, Target 이펙트는 같은 프레임 끝까지 지점을 기다림
        private SkillDefinition _pendingTarget;
        private Vector3? _pendingPoint;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GameEffects>() != null)
            {
                return;
            }

            var go = new GameObject("GameEffects");
            DontDestroyOnLoad(go);
            go.AddComponent<GameEffects>();
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            Health.AnyDied += OnAnyDied;
            BossStatus.AnyPhaseChanged += OnBossPhase;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
            BossStatus.AnyPhaseChanged -= OnBossPhase;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Unhook();
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Unhook();
            _nextPlayerSearch = 0f;
        }

        private void Update()
        {
            if (_player == null && Time.unscaledTime >= _nextPlayerSearch)
            {
                _nextPlayerSearch = Time.unscaledTime + 1f;
                Hook();
            }
        }

        private void LateUpdate()
        {
            if (_pendingTarget == null)
            {
                return;
            }

            Vector3 point = _pendingPoint ?? (_player != null ? _player.transform.position + _player.transform.forward * 2f : Vector3.zero);
            PlayCast(_pendingTarget, point);
            _pendingTarget = null;
            _pendingPoint = null;
        }

        // ------------------------------------------------------------------
        // 플레이어 연결
        // ------------------------------------------------------------------

        private void Hook()
        {
            GameObject go = GameObject.FindWithTag("Player");

            if (go == null || !go.TryGetComponent(out _player))
            {
                _player = null;
                return;
            }

            _player.SkillUsed += OnSkillUsed;
            _player.Dodged += OnDodged;
            _dodge = go.GetComponent<DodgeAction>();
            _caster = go.GetComponent<SkillCaster>();

            if (_caster != null)
            {
                _caster.SkillCast += OnSkillCast;
            }

            _playerHealth = go.GetComponent<Health>();
        }

        private void Unhook()
        {
            if (_player != null)
            {
                _player.SkillUsed -= OnSkillUsed;
                _player.Dodged -= OnDodged;
            }

            if (_caster != null)
            {
                _caster.SkillCast -= OnSkillCast;
            }

            _player = null;
            _caster = null;
            _playerHealth = null;
            _pendingTarget = null;
        }

        private void OnSkillCast(SkillSlot slot, SkillDefinition skill)
        {
            if (skill == null)
            {
                return;
            }

            _lastSkill = skill;
            _lastSlot = slot;
            _lastSkillTime = Time.time;

            if (skill.CastEffect == null)
            {
                return;
            }

            if (skill.CastEffectAnchor == SkillEffectAnchor.Target)
            {
                _pendingTarget = skill;
                _pendingPoint = null;
                return;
            }

            PlayCast(skill, _player != null ? _player.transform.position : Vector3.zero);
        }

        private void OnSkillUsed(SkillSlot slot, Vector3 point)
        {
            if (_pendingTarget != null)
            {
                _pendingPoint = point;
            }
        }

        private void PlayCast(SkillDefinition skill, Vector3 point)
        {
            if (_player == null)
            {
                return;
            }

            Transform me = _player.transform;
            Quaternion facing = Quaternion.LookRotation(Flat(me.forward));

            if (skill.CastEffectAnchor == SkillEffectAnchor.Follow)
            {
                float lifetime = 0f;
                System.Func<bool> keep = null;

                if (skill.CastEffectLifetime == SkillEffectLifetime.SkillDuration)
                {
                    lifetime = skill.GetBaseParameter("duration", 3f);
                }
                else if (skill.CastEffectLifetime == SkillEffectLifetime.Channel && _caster != null)
                {
                    SkillSlot slot = _lastSlot;
                    SkillCaster caster = _caster;
                    keep = () => caster != null && caster.IsChannelActive(slot);
                }

                EffectPlayer.Follow(skill.CastEffect, me, skill.CastEffectScale, lifetime, keep);
                EffectPlayer.Play(skill.CastEffectExtra, me.position, skill.CastEffectScale, facing);
                return;
            }

            Vector3 at = skill.CastEffectAnchor == SkillEffectAnchor.Target ? point : me.position;
            EffectPlayer.Play(skill.CastEffect, at, skill.CastEffectScale, facing);
            EffectPlayer.Play(skill.CastEffectExtra, at, skill.CastEffectScale, facing);
        }

        // 회피(대시): 플레이어 뒤쪽으로 바람 가르는 이펙트 (회피 동안 따라다님)
        private void OnDodged(Vector3 direction)
        {
            EffectLibrary library = EffectLibrary.Instance;

            if (library == null || library.Dodge == null || _player == null)
            {
                return;
            }

            Transform me = _player.transform;
            Vector3 forward = Flat(direction.sqrMagnitude > 0.0001f ? direction : me.forward);
            DodgeAction dodge = _dodge;
            GameObject effect = EffectPlayer.Follow(library.Dodge, me, library.DodgeScale, 0f,
                () => dodge != null && dodge.IsDodging);

            if (effect != null)
            {
                // 진행 방향의 반대(뒤)를 향하게, 몸통 높이 · 등 뒤에
                effect.transform.SetPositionAndRotation(me.position - forward * 0.6f + Vector3.up * 1f, Quaternion.LookRotation(-forward));
            }
        }

        // ------------------------------------------------------------------
        // 전역 이벤트
        // ------------------------------------------------------------------

        private void OnAnyDamaged(Health health, DamageInfo info)
        {
            if (health == null || health == _playerHealth || _player == null || info.Attacker != _player.gameObject)
            {
                return;
            }

            Vector3 at = health.transform.position + Vector3.up * HIT_HEIGHT;
            Quaternion facing = Quaternion.LookRotation(Flat(health.transform.position - _player.transform.position));

            // 기술 타격 (기본 공격 피해는 제외)
            if (!info.IsBasicAttack && _lastSkill != null && Time.time - _lastSkillTime <= SKILL_HIT_WINDOW && _lastSkill.HitEffect != null)
            {
                EffectPlayer.Play(_lastSkill.HitEffect, at, _lastSkill.HitEffectScale, facing);
                EffectPlayer.Play(_lastSkill.HitEffectExtra, health.transform.position, _lastSkill.HitEffectScale, facing);
                return;
            }

            EffectLibrary library = EffectLibrary.Instance;

            if (library != null)
            {
                EffectPlayer.Play(info.IsCritical ? library.CritHit : library.BasicHit, at, library.HitScale, facing);
            }
        }

        private void OnAnyDied(Health health)
        {
            EffectLibrary library = EffectLibrary.Instance;

            if (library == null || health == null || health == _playerHealth || health.GetComponent<BossStatus>() != null)
            {
                return;
            }

            EffectPlayer.Play(library.EnemyDeath, health.transform.position + Vector3.up * 0.6f);
        }

        private void OnBossPhase(BossStatus boss, int phase, string title, string subtitle)
        {
            EffectLibrary library = EffectLibrary.Instance;

            if (library != null && boss != null)
            {
                EffectPlayer.Play(library.BossPhase, boss.transform.position, library.BossPhaseScale);
            }
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v.sqrMagnitude > 0.0001f ? v.normalized : Vector3.forward;
        }
    }
}
