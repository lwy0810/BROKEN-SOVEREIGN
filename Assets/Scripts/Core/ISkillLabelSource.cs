namespace BrokenSovereign.Core
{
    /// <summary>
    /// HUD 기술 슬롯 이름 덧붙임 출처 (플레이어 컴포넌트). 예: 석천 모드변환 → "모드변환 · 단봉"
    /// </summary>
    public interface ISkillLabelSource
    {
        bool TryGetSkillLabel(string skillId, out string label);
    }
}
