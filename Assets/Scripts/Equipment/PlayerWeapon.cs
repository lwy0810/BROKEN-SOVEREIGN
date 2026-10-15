using System;
using BrokenSovereign.Ascension;
using BrokenSovereign.Combat;
using UnityEngine;

namespace BrokenSovereign.Equipment
{
    /// <summary>
    /// 플레이어가 든 무기. 기본 공격 간격(IAttackIntervalSource)과 손의 무기 외형을 담당합니다.
    /// 프로토타입은 장비창 없이 직업이 무기를 정합니다 (GDD 11장: 성투사 전직 시 양손 둔기로 교체).
    /// 직업에 무기가 없으면(무직) 기본 무기를 씁니다.
    /// </summary>
    public class PlayerWeapon : MonoBehaviour, IAttackIntervalSource
    {
        [Tooltip("직업 무기가 없을 때 쓰는 무기 (무직)")]
        [SerializeField] private WeaponDefinition _defaultWeapon;

        [Tooltip("무기를 붙일 Animator. 비우면 자식에서 찾음 (Humanoid 오른손 본 사용)")]
        [SerializeField] private Animator _animator;

        private PlayerJob _job;
        private GameObject _visual;

        public WeaponDefinition Current { get; private set; }

        /// <summary>무기가 바뀔 때 (애니메이터 공격 모션 교체 등)</summary>
        public event Action<WeaponDefinition> WeaponChanged;

        public float BaseAttackInterval => Current != null ? Current.BaseAttackInterval : 0f;

        private void Awake()
        {
            _job = GetComponent<PlayerJob>();

            if (_animator == null)
            {
                _animator = GetComponentInChildren<Animator>();
            }
        }

        private void OnEnable()
        {
            if (_job != null)
            {
                _job.JobChanged += OnJobChanged;
            }
        }

        private void OnDisable()
        {
            if (_job != null)
            {
                _job.JobChanged -= OnJobChanged;
            }
        }

        private void Start()
        {
            if (Current != null)
            {
                return;
            }

            if (_defaultWeapon == null)
            {
                Debug.LogWarning($"PlayerWeapon({name}): 기본 무기 미지정. 메뉴 Weapons > 무기 장착을 실행하세요.", this);
                return;
            }

            Equip(_defaultWeapon);
        }

        public void Equip(WeaponDefinition weapon)
        {
            weapon = weapon != null ? weapon : _defaultWeapon;

            if (weapon == Current)
            {
                return;
            }

            Current = weapon;
            RefreshVisual();
            WeaponChanged?.Invoke(Current);
        }

        private void OnJobChanged(JobDefinition job)
        {
            Equip(job != null ? job.ResolveDefaultWeapon() : null);
        }

        private void RefreshVisual()
        {
            if (_visual != null)
            {
                Destroy(_visual);
                _visual = null;
            }

            if (Current == null)
            {
                Debug.LogWarning($"PlayerWeapon({name}): 장착할 무기 없음 (기본 무기 미지정)", this);
                return;
            }

            if (Current.VisualPrefab == null)
            {
                return;
            }

            if (_animator == null || !_animator.isHuman)
            {
                Debug.LogWarning($"PlayerWeapon({name}): Humanoid Animator를 찾지 못해 무기 외형을 붙이지 못함", this);
                return;
            }

            Transform hand = _animator.GetBoneTransform(HumanBodyBones.RightHand);

            if (hand == null)
            {
                Debug.LogWarning($"PlayerWeapon({name}): 오른손 본 없음", this);
                return;
            }

            _visual = Instantiate(Current.VisualPrefab, hand);
            _visual.name = Current.VisualPrefab.name;
            _visual.transform.localPosition = Current.GripPosition;
            _visual.transform.localRotation = Current.GripRotation;

            // 손 본에 FBX 단위 배율이 남아 있어도 무기는 캐릭터 루트 기준 실제 크기로 표시
            float handScale = hand.lossyScale.x / _animator.transform.lossyScale.x;
            _visual.transform.localScale = Vector3.one / Mathf.Max(handScale, 0.0001f);
        }

#if UNITY_EDITOR
        public void EditorSetDefaultWeapon(WeaponDefinition weapon)
        {
            _defaultWeapon = weapon;
        }
#endif
    }
}
