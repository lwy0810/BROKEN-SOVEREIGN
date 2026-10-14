using System.Collections.Generic;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using NUnit.Framework;
using UnityEngine;

namespace BrokenSovereign.Tests
{
    public class SkillLoadoutTests
    {
        private readonly List<SkillDefinition> _created = new List<SkillDefinition>();
        private SkillLoadout _loadout;
        private SkillDefinition _a;
        private SkillDefinition _b;
        private SkillDefinition _c;

        [SetUp]
        public void SetUp()
        {
            _loadout = new SkillLoadout();
            _a = Create("A");
            _b = Create("B");
            _c = Create("C");
        }

        [TearDown]
        public void TearDown()
        {
            foreach (SkillDefinition skill in _created)
            {
                Object.DestroyImmediate(skill);
            }

            _created.Clear();
        }

        [Test]
        public void Equip_UnlockedCandidate_Succeeds()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a, _b });
            _loadout.Unlock(_a);

            Assert.AreEqual(EquipResult.Success, _loadout.Equip(SkillSlot.Key1, _a, false));
            Assert.AreSame(_a, _loadout.GetEquipped(SkillSlot.Key1));
        }

        [Test]
        public void Equip_Locked_Rejected()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a });

            Assert.AreEqual(EquipResult.Locked, _loadout.Equip(SkillSlot.Key1, _a, false));
            Assert.IsNull(_loadout.GetEquipped(SkillSlot.Key1));
        }

        [Test]
        public void Equip_OtherSlotCandidate_Rejected()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a });
            _loadout.SetCandidates(SkillSlot.Key2, new[] { _b });
            _loadout.Unlock(_b);

            Assert.AreEqual(EquipResult.NotCandidate, _loadout.Equip(SkillSlot.Key1, _b, false));
        }

        [Test]
        public void Equip_InCombat_RejectedAndKeepsCurrent()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a, _b });
            _loadout.Unlock(_a);
            _loadout.Unlock(_b);
            _loadout.Equip(SkillSlot.Key1, _a, false);

            Assert.AreEqual(EquipResult.InCombat, _loadout.Equip(SkillSlot.Key1, _b, true));
            Assert.AreSame(_a, _loadout.GetEquipped(SkillSlot.Key1));
        }

        [Test]
        public void Equip_SameSkill_AlreadyEquipped()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a });
            _loadout.Unlock(_a);
            _loadout.Equip(SkillSlot.Key1, _a, false);

            Assert.AreEqual(EquipResult.AlreadyEquipped, _loadout.Equip(SkillSlot.Key1, _a, true));
        }

        [Test]
        public void Equip_SkillInTwoSlots_MovesToNewSlot()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a });
            _loadout.SetCandidates(SkillSlot.Key2, new[] { _a });
            _loadout.Unlock(_a);
            _loadout.Equip(SkillSlot.Key1, _a, false);

            Assert.AreEqual(EquipResult.Success, _loadout.Equip(SkillSlot.Key2, _a, false));
            Assert.IsNull(_loadout.GetEquipped(SkillSlot.Key1));
            Assert.AreSame(_a, _loadout.GetEquipped(SkillSlot.Key2));
        }

        [Test]
        public void EquipNext_SkipsLockedAndWraps()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a, _b, _c });
            _loadout.Unlock(_a);
            _loadout.Unlock(_c);
            _loadout.Equip(SkillSlot.Key1, _a, false);

            Assert.AreEqual(EquipResult.Success, _loadout.EquipNext(SkillSlot.Key1, false));
            Assert.AreSame(_c, _loadout.GetEquipped(SkillSlot.Key1));

            Assert.AreEqual(EquipResult.Success, _loadout.EquipNext(SkillSlot.Key1, false));
            Assert.AreSame(_a, _loadout.GetEquipped(SkillSlot.Key1));
        }

        [Test]
        public void EquipNext_NoOtherUnlocked_ReturnsNoOtherCandidate()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a, _b });
            _loadout.Unlock(_a);
            _loadout.Equip(SkillSlot.Key1, _a, false);

            Assert.AreEqual(EquipResult.NoOtherCandidate, _loadout.EquipNext(SkillSlot.Key1, false));
        }

        [Test]
        public void SetCandidates_RemovingEquipped_Unequips()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a, _b });
            _loadout.Unlock(_a);
            _loadout.Equip(SkillSlot.Key1, _a, false);

            _loadout.SetCandidates(SkillSlot.Key1, new[] { _b });

            Assert.IsNull(_loadout.GetEquipped(SkillSlot.Key1));
        }

        [Test]
        public void ClearSlots_KeepsUnlocksAndProgress()
        {
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a });
            _loadout.Unlock(_a);
            _loadout.Equip(SkillSlot.Key1, _a, false);
            _loadout.SetSlotLevel(SkillSlot.Key1, 3);
            _loadout.SetBranch(_a, 1);

            _loadout.ClearSlots();

            Assert.IsNull(_loadout.GetEquipped(SkillSlot.Key1));
            Assert.AreEqual(0, _loadout.GetCandidates(SkillSlot.Key1).Count);
            Assert.IsTrue(_loadout.IsUnlocked(_a));
            Assert.AreEqual(3, _loadout.GetSlotLevel(SkillSlot.Key1));
            Assert.AreEqual(1, _loadout.GetBranch(_a));
        }

        [Test]
        public void Changed_RaisedOnEquipAndUnequip()
        {
            var events = new List<SkillDefinition>();
            _loadout.Changed += (slot, skill) => events.Add(skill);
            _loadout.SetCandidates(SkillSlot.Key1, new[] { _a });
            _loadout.Unlock(_a);

            _loadout.Equip(SkillSlot.Key1, _a, false);
            _loadout.Unequip(SkillSlot.Key1);

            CollectionAssert.AreEqual(new SkillDefinition[] { _a, null }, events);
        }

        private SkillDefinition Create(string skillName)
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            skill.name = skillName;
            _created.Add(skill);
            return skill;
        }
    }
}
