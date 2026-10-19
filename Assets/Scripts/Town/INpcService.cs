using UnityEngine;

namespace BrokenSovereign.Town
{
    /// <summary>
    /// NPC 기능 (치료 등). TownNpc와 같은 오브젝트에 붙이면 대화 창에 기능 버튼이 표시됩니다.
    /// </summary>
    public interface INpcService
    {
        /// <summary>대화 창 버튼 이름</summary>
        string ActionLabel { get; }

        /// <summary>기능을 실행하고 결과 대사를 돌려 줍니다.</summary>
        string Use(GameObject user);
    }
}
