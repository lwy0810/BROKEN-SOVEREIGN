using System;
using System.Collections.Generic;
using BrokenSovereign.Combat;
using BrokenSovereign.Core;
using UnityEngine;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 일정 시간 시전자에게 능력치 버프 (지속 기술). 기술 수치 키를 능력치로 연결합니다.
    /// 예: damageReduction(0.3) → 받는 피해 감소 +30%. 충만 수정치로 수치를 바꾸면 버프도 강해집니다.
    /// 수치 키: duration(초) + 아래 연결 목록의 키
    /// 사용 기술: 불굴의 갑주 (보복의 맹세 예정)
    /// </summary>
    [CreateAssetMenu(fileName = "TimedBuffBehaviour", menuName = "BrokenSovereign/Skill Behaviour/Timed Buff")]
    public class TimedBuffBehaviour : SkillBehaviour
    {
        [Serializable]
        private struct BuffStat
        {
            [Tooltip("기술 수치 키 (SkillParameterKeys)")]
            public string Key;
            public StatType Stat;
            public ModifierType Type;
        }

        [SerializeField] private List<BuffStat> _stats = new List<BuffStat>();

        private class BuffSource : IStatModifierSource
        {
            public readonly List<StatModifier> Modifiers = new List<StatModifier>();
            public CharacterStats Owner;

            public IEnumerable<StatModifier> GetModifiers() => Modifiers;
        }

        public override void Begin(SkillContext context)
        {
            context.Duration = context.Parameters.Get(SkillParameterKeys.DURATION, 8f);

            var source = new BuffSource { Owner = context.Caster.GetComponent<CharacterStats>() };

            foreach (BuffStat buffStat in _stats)
            {
                if (context.Parameters.Has(buffStat.Key))
                {
                    source.Modifiers.Add(new StatModifier(buffStat.Stat, buffStat.Type,
                        context.Parameters.Get(buffStat.Key)));
                }
            }

            if (source.Owner == null)
            {
                Debug.LogWarning($"{context.Definition.DisplayName}: 시전자에 CharacterStats가 없어 버프를 적용할 수 없습니다.",
                    context.Caster);
            }
            else
            {
                source.Owner.Sheet.AddSource(source);
            }

            context.State = source;
            Debug.Log($"{context.Definition.DisplayName}: {context.Duration:0.#}초 버프{(context.IsEmpowered ? " (충만 강화)" : string.Empty)}");
        }

        public override bool Tick(SkillContext context, float deltaTime)
        {
            return context.Elapsed < context.Duration;
        }

        public override void End(SkillContext context)
        {
            if (context.State is BuffSource source && source.Owner != null)
            {
                source.Owner.Sheet.RemoveSource(source);
            }

            Debug.Log($"{context.Definition.DisplayName}: 종료");
        }
    }
}
