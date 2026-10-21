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
    /// 슬롯 레벨·강화 분기·기술 포인트를 저장하고, 효과는 SkillGrowth가 시전 수치에 적용합니다 (10/21).
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

        // 키 순서 바꾸기 (10/21): 직업·영웅이 정한 슬롯(지정) → 실제 키. 직업이 바뀌어도 유지
        private readonly Dictionary<SkillSlot, SkillSlot> _remap = new Dictionary<SkillSlot, SkillSlot>();

        /// <summary>슬롯 장착 기술이 바뀌었을 때 (슬롯, 새 기술 또는 null)</summary>
        public event Action<SkillSlot, SkillDefinition> Changed;

        /// <summary>기술 포인트·슬롯 레벨·분기가 바뀌었을 때</summary>
        public event Action GrowthChanged;

        /// <summary>남은 기술 포인트 (레벨업당 1)</summary>
        public int SkillPoints { get; private set; }

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

        /// <summary>지정 슬롯이 지금 놓인 실제 키 (키 순서를 바꾸지 않았으면 그대로)</summary>
        public SkillSlot MapSlot(SkillSlot designated)
        {
            return _remap.TryGetValue(designated, out SkillSlot actual) ? actual : designated;
        }

        /// <summary>실제 키에 놓인 지정 슬롯 (MapSlot의 반대)</summary>
        public SkillSlot DesignatedOf(SkillSlot actual)
        {
            foreach (KeyValuePair<SkillSlot, SkillSlot> pair in _remap)
            {
                if (pair.Value == actual)
                {
                    return pair.Key;
                }
            }

            return actual;
        }

        /// <summary>
        /// 두 키의 기술 맞바꾸기 (기술 창 장착 슬롯 끌어 놓기). 후보·장착·슬롯 레벨이 함께 옮겨지고,
        /// 이후 직업 전환·영웅 기술 장착도 바뀐 키에 들어갑니다. 좌클릭(기본 공격)은 제외, 전투 중 불가.
        /// </summary>
        public bool SwapSlots(SkillSlot a, SkillSlot b, bool inCombat)
        {
            if (a == b || inCombat || a == SkillSlot.LeftClick || b == SkillSlot.LeftClick)
            {
                return false;
            }

            Swap(_candidates, a, b);
            Swap(_equipped, a, b);
            Swap(_slotLevels, a, b);

            SkillSlot da = DesignatedOf(a), db = DesignatedOf(b);
            _remap[da] = b;
            _remap[db] = a;

            Changed?.Invoke(a, GetEquipped(a));
            Changed?.Invoke(b, GetEquipped(b));
            GrowthChanged?.Invoke();
            return true;
        }

        private static void Swap<T>(Dictionary<SkillSlot, T> map, SkillSlot a, SkillSlot b)
        {
            bool hasA = map.TryGetValue(a, out T va);
            bool hasB = map.TryGetValue(b, out T vb);
            map.Remove(a);
            map.Remove(b);

            if (hasA)
            {
                map[b] = va;
            }

            if (hasB)
            {
                map[a] = vb;
            }
        }

        /// <summary>슬롯 후보 지정 (직업·영웅 설정용, 지정 슬롯 기준 → 실제 키로 옮겨 저장). null·중복은 제외. 현재 장착 기술이 후보에서 빠지면 해제</summary>
        public void SetCandidates(SkillSlot slot, IEnumerable<SkillDefinition> candidates)
        {
            slot = MapSlot(slot);
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
            // 직업·영웅 설정은 지정 슬롯으로 부름: 그 키의 후보가 아니면 옮겨진 실제 키로
            if (!IsCandidate(slot, skill) && IsCandidate(MapSlot(slot), skill))
            {
                slot = MapSlot(slot);
            }

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

        public void AddPoints(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            SkillPoints += amount;
            GrowthChanged?.Invoke();
        }

        /// <summary>포인트 1을 써서 슬롯 레벨 +1 (최대 SkillGrowth.MAX_LEVEL)</summary>
        public bool TryLevelUp(SkillSlot slot)
        {
            int level = GetSlotLevel(slot);

            if (SkillPoints <= 0 || level >= SkillGrowth.MAX_LEVEL)
            {
                return false;
            }

            SkillPoints--;
            _slotLevels[slot] = level + 1;
            GrowthChanged?.Invoke();
            return true;
        }

        /// <summary>모든 슬롯 레벨을 1로 되돌리고 포인트 환급, 분기 선택 초기화</summary>
        public void ResetGrowth()
        {
            foreach (int level in _slotLevels.Values)
            {
                SkillPoints += Math.Max(0, level - 1);
            }

            _slotLevels.Clear();
            _branchChoices.Clear();
            GrowthChanged?.Invoke();
        }

        /// <summary>슬롯 레벨 (기본 1). 효과는 SkillGrowth.Apply (SkillCaster 시전 시)</summary>
        public int GetSlotLevel(SkillSlot slot)
        {
            return _slotLevels.TryGetValue(slot, out int level) ? level : 1;
        }

        public void SetSlotLevel(SkillSlot slot, int level)
        {
            _slotLevels[slot] = Math.Max(1, level);
            GrowthChanged?.Invoke();
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
            }
            else
            {
                _branchChoices[skill] = branch;
            }

            GrowthChanged?.Invoke();
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
