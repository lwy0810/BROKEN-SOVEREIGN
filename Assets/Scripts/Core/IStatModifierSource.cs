using System.Collections.Generic;

namespace BrokenSovereign.Core
{
    /// <summary>
    /// 수정치를 제공하는 출처 (장비, 신물, 업적, 버프).
    /// 내용이 바뀌면 소유자가 StatSheet.NotifySourcesChanged를 호출합니다.
    /// </summary>
    public interface IStatModifierSource
    {
        IEnumerable<StatModifier> GetModifiers();
    }
}
