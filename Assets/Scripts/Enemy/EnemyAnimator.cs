using UnityEngine;
using UnityEngine.AI;

namespace BrokenSovereign.Enemy
{
    /// <summary>
    /// 적 모델 Animator 연결 (PlayerAnimator와 같은 파라미터: Speed, Moving, Attack, AttackSpeed).
    /// 이동 속도 → 대기·달리기 블렌드, 공격 상태 진입 → 공격 모션 (예비 동작 + 후딜레이 시간에 맞춰 재생 속도 조절).
    /// 기절·띄우기 중에는 모션을 멈춥니다. 피격·사망 모션은 아직 없습니다.
    /// </summary>
    [RequireComponent(typeof(EnemyController))]
    public class EnemyAnimator : MonoBehaviour
    {
        private static readonly int SPEED = Animator.StringToHash("Speed");
        private static readonly int MOVING = Animator.StringToHash("Moving");
        private static readonly int ATTACK = Animator.StringToHash("Attack");
        private static readonly int ATTACK_SPEED = Animator.StringToHash("AttackSpeed");

        [Tooltip("비우면 자식에서 찾음")]
        [SerializeField] private Animator _animator;

        [Tooltip("공격 모션 길이(초). 0이면 Animator의 'Attack' 이름 클립에서 읽음")]
        [SerializeField, Min(0f)] private float _attackClipLength;

        [Tooltip("이 속도 비율 이상이면 이동 중으로 판단")]
        [SerializeField, Range(0f, 1f)] private float _movingThreshold = 0.1f;

        private EnemyController _controller;
        private NavMeshAgent _agent;
        private EnemyState _lastState;

        private void Awake()
        {
            _controller = GetComponent<EnemyController>();
            _agent = GetComponent<NavMeshAgent>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }

            if (_attackClipLength <= 0f)
            {
                _attackClipLength = FindAttackClipLength();
            }
        }

        private void Update()
        {
            if (_animator == null)
            {
                return;
            }

            bool frozen = _controller.IsStunned || _controller.IsAirborne || _controller.State == EnemyState.Dead;
            _animator.speed = frozen ? 0f : 1f;

            float maxSpeed = _agent != null && _agent.speed > 0f ? _agent.speed : 1f;
            float speed = _agent != null && _agent.enabled ? Mathf.Clamp01(_agent.velocity.magnitude / maxSpeed) : 0f;
            _animator.SetFloat(SPEED, speed);
            _animator.SetBool(MOVING, speed > _movingThreshold);

            EnemyState state = _controller.State;

            if (state == EnemyState.Attack && _lastState != EnemyState.Attack)
            {
                PlayAttack();
            }

            _lastState = state;
        }

        // 타격 시점이 대략 예비 동작 끝과 맞도록 공격 모션 전체를 예비 동작 + 후딜레이 시간에 맞춤
        private void PlayAttack()
        {
            EnemyDefinition definition = _controller.Definition;
            float duration = definition != null ? definition.AttackWindup + definition.AttackRecovery : 1f;
            float playSpeed = _attackClipLength > 0f && duration > 0f ? _attackClipLength / duration : 1f;
            _animator.SetFloat(ATTACK_SPEED, Mathf.Clamp(playSpeed, 0.25f, 4f));
            _animator.SetTrigger(ATTACK);
        }

        private float FindAttackClipLength()
        {
            if (_animator == null || _animator.runtimeAnimatorController == null)
            {
                return 0f;
            }

            foreach (AnimationClip clip in _animator.runtimeAnimatorController.animationClips)
            {
                if (clip != null && clip.name.Contains("Attack"))
                {
                    return clip.length;
                }
            }

            return 0f;
        }

#if UNITY_EDITOR
        public void EditorSetup(Animator animator, float attackClipLength)
        {
            _animator = animator;
            _attackClipLength = attackClipLength;
        }
#endif
    }
}
