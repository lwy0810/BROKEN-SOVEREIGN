using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class SkillRangeTests
    {
        [Test]
        public void Resolve_CastRange_TakesPriorityOverRadius()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.RADIUS, 3.5f);
            set.Set(SkillParameterKeys.CAST_RANGE, 4.5f);

            Assert.AreEqual(4.5f, SkillRange.Resolve(set), 0.0001f);
        }

        [Test]
        public void Resolve_NoCastRange_UsesRadiusRatio()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.RADIUS, 3.5f);

            Assert.AreEqual(3.5f * SkillRange.RADIUS_REACH_RATIO, SkillRange.Resolve(set), 0.0001f);
        }

        [Test]
        public void Resolve_NoRangeValues_ReturnsZero()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.DISTANCE, 5f);

            Assert.AreEqual(0f, SkillRange.Resolve(set));
            Assert.AreEqual(0f, SkillRange.Resolve(null));
        }

        [Test]
        public void Resolve_EmpoweredRadius_ScalesRange()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.RADIUS, 3.5f);
            set.Apply(new SkillParameterModifier(SkillParameterKeys.RADIUS, ModifierType.Percent, 0.5f));

            Assert.AreEqual(3.5f * 1.5f * SkillRange.RADIUS_REACH_RATIO, SkillRange.Resolve(set), 0.0001f);
        }

        [Test]
        public void Resolve_EmpoweredCastRange_Scales()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.CAST_RANGE, 4.5f);
            set.Apply(new SkillParameterModifier(SkillParameterKeys.CAST_RANGE, ModifierType.Percent, 0.3f));

            Assert.AreEqual(4.5f * 1.3f, SkillRange.Resolve(set), 0.0001f);
        }
    }
}
