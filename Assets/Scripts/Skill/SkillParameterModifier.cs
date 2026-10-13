using BrokenSovereign.Core;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 기술 수치 하나를 바꾸는 수정치 (강화 분기·충만·영웅 규칙용).
    /// Flat: 더하기 / Percent: (1 + 값) 곱하기 / Multiplicative: 값 곱하기
    /// </summary>
    public readonly struct SkillParameterModifier
    {
        public readonly string Key;
        public readonly ModifierType Type;
        public readonly float Value;

        public SkillParameterModifier(string key, ModifierType type, float value)
        {
            Key = key;
            Type = type;
            Value = value;
        }
    }
}
