using System;
using System.Collections.Generic;
using BrokenSovereign.Core;

namespace BrokenSovereign.Skill
{
    /// <summary>장착 시도 결과</summary>
    public enum EquipResult
    {
        Success,
        /// <summary>이미 그 슬롯에 장착된 기술</summary>
        AlreadyEquipped,
        /// <summary>그 슬롯의 후보가 아님 (다른 슬롯 후보 포함)</summary>
        NotCandidate,
        /// <summary>해금하지 않은 기술</summary>
        Locked,
        /// <summary>전투 중에는 교체 불가</summary>
        InCombat,
        /// <summary>순환 장착할 다른 해금 후보가 없음</summary>
        NoOtherCandidate
    }

    /// <summary>
    /// 슬롯별 기술 구성 (순수 클래스, EditMode 테스트 대상).
    /// 슬롯마다 후보 목록을 두고, 해금한 후보 중 하나를 장착합니다.
    /// 해금은 직업을 바꿔도 유지되고, 후보·장착은 직업 전환 시 다시 구성합니다.
    /// 슬롯 레벨·강화 분기 선택은 저장 자리만 두고, 효과 적용은 이후 단계에서 연결합니다.
    /// </summary>
    public class SkillLoadout
    {
        private static readonly IReadOnlyList<SkillDefinition> EMPTY = Array.Empty<SkillDefinition>();

        private readonly Dictionary<SkillSlot, List<SkillDefinition>> _candidates =
            new Dictionary<SkillSlot, List<SkillDefinition>>();
        private readonly Dictionary<SkillSlot, SkillDefinition> _equipped = new Dictionary<SkillSlot, SkillDefinition>();
        private readonly HashSet<SkillDefinition> _unlocked = new HashSet<SkillDefinition>();
        private readonly Dictionary<SkillSlot, int> _slotLevels = new Dictionary<SkillSlot, int>();
        private readonly Dictionary<SkillDefinition, int> _branchChoices = new Dictionary<SkillDefinition, int>();

        /// <summary>슬롯 장착 기술이 바뀌었을 때 (슬롯, 새 기술 또는 null)</summary>
        public event Action<SkillSlot, SkillDefinition> Changed;

        /// <summary>후보·장착 초기화 (직업 전환 시). 해금·슬롯 레벨·분기 선택은 유지</summary>
        public void ClearSlots()
        {
            var slots = new List<SkillSlot>(_equipped.Keys);
            _candidates.Clear();
            _equipped.Clear();

            foreach (SkillSlot slot in slots)
            {
                Changed?.Invoke(slot, null);
            }
        }

        /// <summary>슬롯 후보 지정. null·중복은 제외. 현재 장착 기술이 후보에서 빠지면 해제</summary>
        public void SetCandidates(SkillSlot slot, IEnumerable<SkillDefinition> candidates)
        {
            var list = new List<SkillDefinition>();

            if (candidates != null)
            {
                foreach (SkillDefinition skill in candidates)
                {
                    if (skill != null && !list.Contains(skill))
                    {
                        list.Add(skill);
                    }
                }
            }

            _candidates[slot] = list;

            if (_equipped.TryGetValue(slot, out SkillDefinition equipped) && !list.Contains(equipped))
            {
                Unequip(slot);
            }
        }

        public IReadOnlyList<SkillDefinition> GetCandidates(SkillSlot slot)
        {
            return _candidates.TryGetValue(slot, out List<SkillDefinition> list) ? list : EMPTY;
        }

        public bool IsCandidate(SkillSlot slot, SkillDefinition skill)
        {
            return skill != null && _candidates.TryGetValue(slot, out List<SkillDefinition> list) && list.Contains(skill);
        }

        public bool IsUnlocked(SkillDefinition skill)
        {
            return skill != null && _unlocked.Contains(skill);
        }

        /// <summary>해금. 새로 해금했으면 true</summary>
        public bool Unlock(SkillDefinition skill)
        {
            return skill != null && _unlocked.Add(skill);
        }

        public SkillDefinition GetEquipped(SkillSlot slot)
        {
            return _equipped.TryGetValue(slot, out SkillDefinition skill) ? skill : null;
        }

        /// <summary>장착. 실패 이유를 돌려줍니다 (해금 안 됨·다른 슬롯 후보·전투 중)</summary>
        public EquipResult Equip(SkillSlot slot, SkillDefinition skill, bool inCombat)
        {
            if (!IsCandidate(slot, skill))
            {
                return EquipResult.NotCandidate;
            }

            if (!IsUnlocked(skill))
            {
                return EquipResult.Locked;
            }

            if (GetEquipped(slot) == skill)
            {
                return EquipResult.AlreadyEquipped;
            }

            if (inCombat)
            {
                return EquipResult.InCombat;
            }

            // 같은 기술이 여러 슬롯 후보면 한 슬롯에만 장착
            foreach (KeyValuePair<SkillSlot, SkillDefinition> pair in _equipped)
            {
                if (pair.Value == skill)
                {
                    Unequip(pair.Key);
                    break;
                }
            }

            _equipped[slot] = skill;
            Changed?.Invoke(slot, skill);
            return EquipResult.Success;
        }

        /// <summary>해금된 다음 후보로 교체 (후보 목록 순서대로 순환)</summary>
        public EquipResult EquipNext(SkillSlot slot, bool inCombat)
        {
            IReadOnlyList<SkillDefinition> candidates = GetCandidates(slot);
            SkillDefinition current = GetEquipped(slot);
            int start = IndexOf(candidates, current);

            for (int offset = 1; offset <= candidates.Count; offset++)
            {
                SkillDefinition next = candidates[(start + offset + candidates.Count) % candidates.Count];

                if (next != current && IsUnlocked(next))
                {
                    return Equip(slot, next, inCombat);
                }
            }

            return EquipResult.NoOtherCandidate;
        }

        public void Unequip(SkillSlot slot)
        {
            if (_equipped.Remove(slot))
            {
                Changed?.Invoke(slot, null);
            }
        }

        /// <summary>슬롯 레벨 (기본 1). 효과 적용은 이후 단계</summary>
        public int GetSlotLevel(SkillSlot slot)
        {
            return _slotLevels.TryGetValue(slot, out int level) ? level : 1;
        }

        public void SetSlotLevel(SkillSlot slot, int level)
        {
            _slotLevels[slot] = Math.Max(1, level);
        }

        /// <summary>기술 강화 분기 선택 (-1 = 미선택). 효과 적용은 이후 단계</summary>
        public int GetBranch(SkillDefinition skill)
        {
            return skill != null && _branchChoices.TryGetValue(skill, out int branch) ? branch : -1;
        }

        public void SetBranch(SkillDefinition skill, int branch)
        {
            if (skill == null)
            {
                return;
            }

            if (branch < 0)
            {
                _branchChoices.Remove(skill);
                return;
            }

            _branchChoices[skill] = branch;
        }

        private static int IndexOf(IReadOnlyList<SkillDefinition> list, SkillDefinition skill)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == skill)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
