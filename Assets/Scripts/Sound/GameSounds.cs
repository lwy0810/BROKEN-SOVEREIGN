using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using BrokenSovereign.Enemy;
using BrokenSovereign.Player;
using BrokenSovereign.Skill;
using BrokenSovereign.Town;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BrokenSovereign.Sound
{
    /// <summary>
    /// 게임 사건 → 소리 연결 (10/26). 게임 시작 시 자동 생성되어 씬이 바뀌어도 유지됩니다.
    /// - 전역 이벤트: 사망 (10/26 몬스터·플레이어 피격 효과음 제거), 보스 페이즈·격파, 웨이포인트 발견
    /// - 플레이어 이벤트: 기본 공격, 기술 시전(기술별 파일 없으면 공용), 회피, 물약
    /// - 음악: 필드 = bgm_field, 보스 교전 중 = bgm_boss (타이틀 씬 bgm_title은 TitleScreen이 재생)
    /// 보스 패턴·번개·포털·타이틀 UI 소리는 각 코드에서 SoundPlayer를 직접 호출합니다.
    /// </summary>
    public class GameSounds : MonoBehaviour
    {
        private const string TITLE_SCENE = "Title";

        private PlayerController _player;
        private SkillCaster _caster;
        private Health _playerHealth;
        private float _nextPlayerSearch;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (FindAnyObjectByType<GameSounds>() != null)
            {
                return;
            }

            var go = new GameObject("GameSounds");
            DontDestroyOnLoad(go);
            go.AddComponent<GameSounds>();
        }

        private void OnEnable()
        {
            Health.AnyDamaged += OnAnyDamaged;
            Health.AnyDied += OnAnyDied;
            BossStatus.AnyPhaseChanged += OnBossPhase;
            BossStatus.AnyDefeated += OnBossDefeated;
            Waypoint.AnyDiscovered += OnWaypointDiscovered;
            SkillBehaviour.ImpactLanded += OnSkillImpact;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            Health.AnyDamaged -= OnAnyDamaged;
            Health.AnyDied -= OnAnyDied;
            BossStatus.AnyPhaseChanged -= OnBossPhase;
            BossStatus.AnyDefeated -= OnBossDefeated;
            Waypoint.AnyDiscovered -= OnWaypointDiscovered;
            SkillBehaviour.ImpactLanded -= OnSkillImpact;
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

            UpdateMusic();
        }

        private void UpdateMusic()
        {
            // 타이틀 씬 음악은 TitleScreen이 직접 (세계관 소개 중에는 끔)
            if (SceneManager.GetActiveScene().name == TITLE_SCENE)
            {
                return;
            }

            bool bossFight = false;

            foreach (BossStatus boss in BossStatus.Active)
            {
                if (boss != null && boss.IsEngaged)
                {
                    bossFight = true;
                    break;
                }
            }

            SoundPlayer.PlayMusic(bossFight ? SoundId.BGM_BOSS : SoundId.BGM_FIELD);
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

            _player.BasicAttacked += OnBasicAttacked;
            _player.Dodged += OnDodged;
            _player.PotionUsed += OnPotionUsed;
            _caster = go.GetComponent<SkillCaster>();

            if (_caster != null)
            {
                _caster.SkillCast += OnSkillCast;
            }

            _playerHealth = go.GetComponent<Health>();
            SoundPlayer.SetListener(go.transform);
        }

        private void Unhook()
        {
            if (_player != null)
            {
                _player.BasicAttacked -= OnBasicAttacked;
                _player.Dodged -= OnDodged;
                _player.PotionUsed -= OnPotionUsed;
            }

            if (_caster != null)
            {
                _caster.SkillCast -= OnSkillCast;
            }

            _player = null;
            _caster = null;
            _playerHealth = null;
            SoundPlayer.SetListener(null);
        }

        private void OnBasicAttacked(Vector3 point)
        {
            SoundPlayer.Play(SoundId.ATTACK_SWING, 0.8f, 0.08f);
        }

        private void OnSkillCast(SkillSlot slot, SkillDefinition skill)
        {
            // 강타 등 타격 순간에 피해를 주는 기술은 그 순간에 소리 (OnSkillImpact)
            if (skill != null && skill.Behaviour != null && skill.Behaviour.DelaysImpact)
            {
                return;
            }

            PlaySkillSound(skill);
        }

        private void OnSkillImpact(SkillDefinition skill, Vector3 point)
        {
            PlaySkillSound(skill);
        }

        private static void PlaySkillSound(SkillDefinition skill)
        {
            string id = skill != null ? SoundId.Skill(skill.SkillId) : null;
            SoundPlayer.Play(id != null && SoundPlayer.Has(id) ? id : SoundId.SKILL_CAST);
        }

        private void OnDodged(Vector3 direction)
        {
            // 10/26: 회피 효과음 제거 (이펙트로 대체: GameEffects)
        }

        private void OnPotionUsed(int remaining)
        {
            SoundPlayer.Play(SoundId.POTION);
        }

        // ------------------------------------------------------------------
        // 전역 이벤트
        // ------------------------------------------------------------------

        private void OnAnyDamaged(Health health, DamageInfo info)
        {
            if (health == null)
            {
                return;
            }

            // 10/26: 몬스터 피격(일반·치명타)·플레이어 피격 효과음 모두 제거. 다시 쓰려면 여기서 재생
        }

        private void OnAnyDied(Health health)
        {
            if (health == null)
            {
                return;
            }

            if (health == _playerHealth)
            {
                SoundPlayer.Play(SoundId.PLAYER_DEATH);
            }
            else if (health.GetComponent<BossStatus>() != null)
            {
                // 10/26: 적 사망음은 보스만 (일반 적은 이펙트만, GameEffects)
                SoundPlayer.PlayAt(SoundId.ENEMY_DEATH, health.transform.position, 1f, 0.05f);
            }
        }

        private void OnBossPhase(BossStatus boss, int phase, string title, string subtitle)
        {
            SoundPlayer.Play(SoundId.BOSS_ROAR);
        }

        private void OnBossDefeated(BossStatus boss)
        {
            SoundPlayer.Play(SoundId.VICTORY);
        }

        private void OnWaypointDiscovered(Waypoint waypoint)
        {
            SoundPlayer.Play(SoundId.WAYPOINT);
        }
    }
}
