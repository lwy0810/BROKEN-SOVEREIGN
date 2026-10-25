using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Hero
{
    /// <summary>
    /// 플레이어의 성물 (10/21). 영웅의 길을 고르면(반신) HeroPathController가 붙이고 1단계 각성 상태로 시작합니다.
    /// 1단계 효과: 야로미르 = 버팀 피해·무력화 시간 증가(CrushRule이 읽음), 석천 = 모드변환 후 잠시 피해 증가(능력치 출처).
    /// </summary>
    public class HeroRelic : MonoBehaviour, IStatModifierSource, INamedStatSource
    {
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();
        private CharacterStats _stats;
        private StaffReachMode _staff;
        private float _buffEndTime;
        private bool _buffActive;

        public RelicInfo Info { get; private set; }
        public int Stage { get; private set; }
        public string SourceName => "성물";

        public bool IsBuffActive => _buffActive;
        public float BuffRemaining => Mathf.Max(0f, _buffEndTime - Time.time);

        /// <summary>새로 각성해 아직 [성물] 탭을 보지 않음 (탭 알림 점)</summary>
        public bool IsUnseen { get; set; }

        public event Action Changed;

        public float PoiseMultiplier => Info != null && Info.HeroId == "yaromir" && Stage >= 1 ? 1f + RelicCatalog.YAROMIR_POISE_BONUS : 1f;
        public float StaggerBonus => Info != null && Info.HeroId == "yaromir" && Stage >= 1 ? RelicCatalog.YAROMIR_STAGGER_BONUS : 0f;

        public void Awaken(HeroPathDefinition path)
        {
            Info = path != null ? RelicCatalog.Get(path.HeroId) : null;
            Stage = Info != null ? 1 : 0;
            IsUnseen = Info != null;
            _stats = GetComponent<CharacterStats>();

            if (_stats != null)
            {
                _stats.Sheet.AddSource(this);
            }

            Changed?.Invoke();
        }

        private void Start()
        {
            _staff = GetComponent<StaffReachMode>();

            if (_staff != null)
            {
                _staff.ModeChanged += OnModeChanged;
            }
        }

        private void OnDestroy()
        {
            if (_staff != null)
            {
                _staff.ModeChanged -= OnModeChanged;
            }

            if (_stats != null)
            {
                _stats.Sheet.RemoveSource(this);
            }
        }

        public IEnumerable<StatModifier> GetModifiers()
        {
            return _modifiers;
        }

        // 석천: 모드변환할 때마다 3초간 피해 증가 (다시 바꾸면 시간 갱신)
        private void OnModeChanged(StaffMode mode)
        {
            if (Info == null || Info.HeroId != "seokcheon" || Stage < 1)
            {
                return;
            }

            _buffEndTime = Time.time + RelicCatalog.SEOKCHEON_DURATION;

            if (!_buffActive)
            {
                _buffActive = true;
                _modifiers.Clear();
                _modifiers.Add(new StatModifier(StatType.DamageBonus, ModifierType.Flat, RelicCatalog.SEOKCHEON_DAMAGE_BONUS));
                _stats?.Sheet.NotifySourcesChanged();
            }
        }

        private void Update()
        {
            if (_buffActive && Time.time >= _buffEndTime)
            {
                _buffActive = false;
                _modifiers.Clear();
                _stats?.Sheet.NotifySourcesChanged();
            }
        }
    }
}
