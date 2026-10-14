using System;
using System.Collections.Generic;
using BrokenSovereign.Core;
using UnityEngine;
using UnityEngine.Serialization;

namespace BrokenSovereign.Skill
{
    /// <summary>
    /// 직업 정의의 슬롯 구성 한 줄: 기본 장착 기술 + 교체 후보.
    /// 후보 목록 = 기본 기술 + 교체 후보 (순서대로, 중복·빈칸 제외).
    /// </summary>
    [Serializable]
    public struct SkillSlotCandidates
    {
        public SkillSlot Slot;

        [Tooltip("직업 전환 시 장착되는 기본 기술 (항상 해금)")]
        [FormerlySerializedAs("Skill")]
        public SkillDefinition Default;

        [Tooltip("이 슬롯에 바꿔 낄 수 있는 다른 후보 기술")]
        public List<SkillDefinition> Alternatives;

        /// <summary>기본 기술을 맨 앞에 둔 후보 목록</summary>
        public IEnumerable<SkillDefinition> AllCandidates()
        {
            if (Default != null)
            {
                yield return Default;
            }

            if (Alternatives == null)
            {
                yield break;
            }

            foreach (SkillDefinition skill in Alternatives)
            {
                if (skill != null && skill != Default)
                {
                    yield return skill;
                }
            }
        }
    }
}
