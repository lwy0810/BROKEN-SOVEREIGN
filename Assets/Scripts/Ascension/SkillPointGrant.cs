using BrokenSovereign.Skill;
using UnityEngine;

namespace BrokenSovereign.Ascension
{
    /// <summary>
    /// 레벨업마다 기술 포인트 1 (GDD 5.4, 10/21). 시작 시 이미 오른 레벨만큼(레벨 - 1) 지급합니다.
    /// HudView가 플레이어에 없으면 붙입니다.
    /// </summary>
    public class SkillPointGrant : MonoBehaviour
    {
        private PlayerProgression _progression;
        private SkillCaster _caster;
        private int _grantedLevel = 1;

        private void Awake()
        {
            _progression = GetComponent<PlayerProgression>();
            _caster = GetComponent<SkillCaster>();
        }

        private void OnEnable()
        {
            if (_progression != null)
            {
                _progression.LeveledUp += OnLeveledUp;
            }
        }

        private void OnDisable()
        {
            if (_progression != null)
            {
                _progression.LeveledUp -= OnLeveledUp;
            }
        }

        private void Start()
        {
            if (_progression != null)
            {
                OnLeveledUp(_progression.Level);
            }
        }

        private void OnLeveledUp(int level)
        {
            if (_caster == null || level <= _grantedLevel)
            {
                return;
            }

            _caster.Loadout.AddPoints(level - _grantedLevel);
            _grantedLevel = level;
        }
    }
}
