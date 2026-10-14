using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 시전자 주변 원형 범위의 적에게 시간제 능력치 디버프 (즉발). 기술 수치 키를 능력치로 연결합니다.
    /// 예: attackPowerReduction(0.2) → 공격력 −20%. 같은 기술을 다시 맞으면 중첩 없이 시간만 갱신 (StatusEffectHost).
    /// 수치 키: radius(m), duration(초) + 아래 연결 목록의 키
    /// 사용 기술: 전장의 함성
    /// </summary>
    [CreateAssetMenu(fileName = "AreaDebuffBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Area Debuff")]
    public class AreaDebuffBehaviour : SkillBehaviour
    {
        [Serializable]
        private struct DebuffStat
        {
            [Tooltip("기술 수치 키 (SkillParameterKeys). 값은 감소율 (0.2 = 20% 감소)")]
            public string Key;
            public StatType Stat;
        }

        [SerializeField] private List<DebuffStat> _stats = new List<DebuffStat>();

        [Header("디버그 표시 (임시)")]
        [SerializeField] private bool _showArea = true;
        [SerializeField] private Color _areaColor = new Color(0.6f, 0.3f, 1f, 0.3f);
        [SerializeField] private float _areaDuration = 0.35f;

        private readonly List<IDamageable> _targets = new List<IDamageable>();
        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        public override void Begin(SkillContext context)
        {
            SkillParameterSet parameters = context.Parameters;
            float radius = parameters.Get(SkillParameterKeys.RADIUS, 5f);
            float duration = parameters.Get(SkillParameterKeys.DURATION, 10f);
            Vector3 origin = context.Caster.position;

            _modifiers.Clear();

            foreach (DebuffStat debuff in _stats)
            {
                if (parameters.Has(debuff.Key))
                {
                    // 감소율을 음수 Percent로: (1 + 합) 배율에서 −20%
                    _modifiers.Add(new StatModifier(debuff.Stat, ModifierType.Percent, -parameters.Get(debuff.Key)));
                }
            }

            if (_showArea)
            {
                AttackAreaDebugDrawer.ShowCircle(origin, radius, context.CursorPoint.y, _areaColor, _areaDuration);
            }

            AttackArea.FindAllInCircle(origin, radius, context.TargetMask, _targets);
            string id = context.Definition.SkillId;
            int applied = 0;

            foreach (IDamageable target in _targets)
            {
                if (!(target is Component component))
                {
                    continue;
                }

                StatusEffectHost host = StatusEffectHost.GetOrAdd(component.gameObject);

                if (host == null)
                {
                    continue;
                }

                host.Apply(id, context.Definition.DisplayName, _modifiers, duration);
                applied++;
            }

            Debug.Log($"{context.Definition.DisplayName}: 적용 {applied} ({duration:0.#}초)");
            _targets.Clear();
        }
    }
}
