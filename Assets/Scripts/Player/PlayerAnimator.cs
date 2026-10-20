using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Player
{
    /// <summary>
    /// 플레이어 상태를 모델 Animator 파라미터로 전달합니다 (표현 전용, 게임 로직 영향 없음).
    /// PlayerController 이벤트와 NavMeshAgent 속도만 읽습니다 (GDD 13장 적용 순서).
    ///
    /// Animator 파라미터
    /// - Speed (float, 0~1): 이동 속도 / 최대 속도 → 대기·달리기 블렌드
    /// - Moving (bool): 행동 중이 아니면서 실제로 움직이는 중 → 공격 모션을 끊고 이동으로 전환
    /// - Attack (trigger): 기본 공격·기술 사용
    /// - AttackSpeed (float): 공격 모션 재생 배율. 공격 간격이 모션보다 짧으면 빠르게 재생
    /// - SkillSpeed (float): 기술 모션 재생 배율 (시전 시간에 맞춤)
    ///
    /// 기술 모션: 기술의 AnimationState(예: SkillRadiantSmite) 상태가 있으면 그 상태로, 없으면 공격 모션.
    /// 지속 시전 기술(난천무)은 반복 상태를 시전이 끝날 때까지 유지한 뒤 Locomotion으로 돌아갑니다.
    /// </summary>
    [RequireComponent(typeof(PlayerController))]
    public class PlayerAnimator : MonoBehaviour
    {
        private static readonly int SPEED_ID = Animator.StringToHash("Speed");
        private static readonly int ATTACK_ID = Animator.StringToHash("Attack");
        private static readonly int ATTACK_SPEED_ID = Animator.StringToHash("AttackSpeed");
        private static readonly int MOVING_ID = Animator.StringToHash("Moving");
        private static readonly int SKILL_SPEED_ID = Animator.StringToHash("SkillSpeed");
        private static readonly int LOCOMOTION_ID = Animator.StringToHash("Locomotion");

        [Header("연결")]
        [Tooltip("비워 두면 자식에서 찾음")]
        [SerializeField] private Animator _animator;

        [Header("공격 모션")]
        [Tooltip("공격 클립 길이(초). 에디터 메뉴가 클립에서 자동 입력")]
        [SerializeField] private float _attackClipLength = 1f;

        [Tooltip("공격 간격 안에 모션이 끝나도록 이 비율만큼만 사용 (나머지는 복귀 여유)")]
        [Range(0.5f, 1f)]
        [SerializeField] private float _attackFitRatio = 0.9f;

        [Tooltip("공격 모션 재생 배율 상한")]
        [SerializeField] private float _maxAttackSpeed = 3f;

        [Header("이동")]
        [Tooltip("Speed 값 변화 완화 시간(초)")]
        [SerializeField] private float _speedDampTime = 0.08f;

        [Tooltip("이 비율(최대 속도 대비)보다 빠르면 Moving = true")]
        [SerializeField] private float _movingThreshold = 0.2f;

        private PlayerController _controller;
        private NavMeshAgent _agent;
        private SkillCaster _caster;
        private SkillSlot? _channelSlot;
        private readonly System.Collections.Generic.Dictionary<string, float> _clipLengths =
            new System.Collections.Generic.Dictionary<string, float>();

        public Animator Animator => _animator;

        private void Awake()
        {
            _controller = GetComponent<PlayerController>();
            _agent = GetComponent<NavMeshAgent>();
            _caster = GetComponent<SkillCaster>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnEnable()
        {
            _controller.BasicAttacked += OnBasicAttacked;
            _controller.SkillUsed += OnSkillUsed;
        }

        private void OnDisable()
        {
            _controller.BasicAttacked -= OnBasicAttacked;
            _controller.SkillUsed -= OnSkillUsed;
        }

        private void Update()
        {
            if (_animator == null || _agent == null)
            {
                return;
            }

            float maxSpeed = Mathf.Max(_agent.speed, 0.01f);
            float speed = _agent.isOnNavMesh && !_agent.isStopped
                ? Mathf.Clamp01(_agent.velocity.magnitude / maxSpeed)
                : 0f;

            _animator.SetFloat(SPEED_ID, speed, _speedDampTime, Time.deltaTime);
            _animator.SetBool(MOVING_ID, !_controller.IsActing && speed > _movingThreshold);

            // 지속 시전이 끝나면 반복 모션 종료
            if (_channelSlot.HasValue && (_caster == null || !_caster.IsChannelActive(_channelSlot.Value)))
            {
                _channelSlot = null;
                _animator.CrossFadeInFixedTime(LOCOMOTION_ID, 0.15f, 0);
            }
        }

        /// <summary>기술 모션 재생. 상태가 없으면 false (공격 모션으로 대체)</summary>
        private bool PlaySkill(SkillSlot slot, SkillDefinition skill)
        {
            string state = skill.AnimationState;
            int hash = Animator.StringToHash(state);

            if (_animator == null || !_animator.HasState(0, hash))
            {
                return false;
            }

            float speed = 1f;
            float factor = _caster != null ? _caster.AttackSpeedFactor : 1f;

            if (skill.IsChanneled)
            {
                _channelSlot = slot;
                speed = factor;
            }
            else
            {
                // 시전 시간(없으면 공격 간격) 안에 모션이 끝나도록 빠르게만 재생
                float time = skill.CastTime > 0f ? skill.CastTime / factor : _controller.BasicAttackInterval;
                float length = GetClipLength(state);

                if (time > 0f && length > 0f)
                {
                    speed = Mathf.Clamp(length / (time * _attackFitRatio), 1f, _maxAttackSpeed);
                }
            }

            _animator.SetFloat(SKILL_SPEED_ID, speed);
            _animator.CrossFadeInFixedTime(hash, 0.05f, 0, 0f);
            return true;
        }

        // 상태 이름(SkillX)으로 끝나는 클립 길이 (클립 이름 = 파일 이름 PaladinSkillX)
        private float GetClipLength(string state)
        {
            if (_clipLengths.TryGetValue(state, out float length))
            {
                return length;
            }

            length = 0f;
            RuntimeAnimatorController controller = _animator.runtimeAnimatorController;

            if (controller != null)
            {
                foreach (AnimationClip clip in controller.animationClips)
                {
                    if (clip != null && clip.name.EndsWith(state))
                    {
                        length = clip.length;
                        break;
                    }
                }
            }

            _clipLengths[state] = length;
            return length;
        }

        /// <summary>공격 간격(초)에 맞춰 공격 모션을 재생합니다.</summary>
        public void PlayAttack(float interval)
        {
            if (_animator == null)
            {
                return;
            }

            _animator.SetFloat(ATTACK_SPEED_ID, GetAttackPlaybackSpeed(interval));
            _animator.ResetTrigger(ATTACK_ID);
            _animator.SetTrigger(ATTACK_ID);
        }

        /// <summary>
        /// 모션 길이가 (간격 × 사용 비율)보다 길면 그만큼 빠르게, 짧으면 원래 속도(1)로 재생합니다.
        /// </summary>
        public float GetAttackPlaybackSpeed(float interval)
        {
            if (interval <= 0f || _attackClipLength <= 0f)
            {
                return 1f;
            }

            float available = interval * _attackFitRatio;
            return Mathf.Clamp(_attackClipLength / available, 1f, _maxAttackSpeed);
        }

        private void OnBasicAttacked(Vector3 point)
        {
            PlayAttack(_controller.BasicAttackInterval);
        }

        private void OnSkillUsed(SkillSlot slot, Vector3 point)
        {
            SkillDefinition skill = _caster != null ? _caster.GetSkill(slot) : null;

            if (skill != null && PlaySkill(slot, skill))
            {
                return;
            }

            PlayAttack(_controller.BasicAttackInterval);
        }

#if UNITY_EDITOR
        /// <summary>에디터 메뉴에서 연결할 때 사용합니다.</summary>
        public void EditorSetup(Animator animator, float attackClipLength)
        {
            _animator = animator;
            _attackClipLength = attackClipLength;
        }
#endif
    }
}
