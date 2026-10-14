using System.Collections.Generic;
using BrokenSovereign.Ascension;
using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using NUnit.Framework;
using UnityEngine;

namespace BrokenSovereign.Tests
{
    public class JobSlotMergeTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        private SkillDefinition Skill()
        {
            var skill = ScriptableObject.CreateInstance<SkillDefinition>();
            _created.Add(skill);
            return skill;
        }

        private static SkillSlotCandidates Slot(SkillSlot slot, SkillDefinition skill)
        {
            return new SkillSlotCandidates { Slot = slot, Default = skill, Alternatives = new List<SkillDefinition>() };
        }

        [Test]
        public void MergeSlots_ChildAddsNewSlot_KeepsParentSlots()
        {
            SkillDefinition smite = Skill();
            SkillDefinition quake = Skill();

            List<SkillSlotCandidates> merged = JobDefinition.MergeSlots(new[]
            {
                new[] { Slot(SkillSlot.RightClick, smite) },
                new[] { Slot(SkillSlot.Key3, quake) }
            });

            Assert.AreEqual(2, merged.Count);
            Assert.AreEqual(smite, merged[0].Default);
            Assert.AreEqual(quake, merged[1].Default);
        }

        [Test]
        public void MergeSlots_ChildOverridesSameSlot()
        {
            SkillDefinition parentSkill = Skill();
            SkillDefinition childSkill = Skill();

            List<SkillSlotCandidates> merged = JobDefinition.MergeSlots(new[]
            {
                new[] { Slot(SkillSlot.Key1, parentSkill) },
                new[] { Slot(SkillSlot.Key1, childSkill) }
            });

            Assert.AreEqual(1, merged.Count);
            Assert.AreEqual(childSkill, merged[0].Default);
        }

        [Test]
        public void MergeSlots_SortedBySlot()
        {
            List<SkillSlotCandidates> merged = JobDefinition.MergeSlots(new[]
            {
                new[] { Slot(SkillSlot.Key2, Skill()), Slot(SkillSlot.RightClick, Skill()) },
                new[] { Slot(SkillSlot.Key1, Skill()) }
            });

            Assert.AreEqual(SkillSlot.RightClick, merged[0].Slot);
            Assert.AreEqual(SkillSlot.Key1, merged[1].Slot);
            Assert.AreEqual(SkillSlot.Key2, merged[2].Slot);
        }
    }
}
