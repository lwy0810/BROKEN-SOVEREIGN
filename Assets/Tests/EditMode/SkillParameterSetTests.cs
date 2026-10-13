using BrokenSovereign.Core;
using BrokenSovereign.Skill;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class SkillParameterSetTests
    {
        [Test]
        public void Get_MissingKey_ReturnsFallback()
        {
            var set = new SkillParameterSet();

            Assert.AreEqual(3f, set.Get(SkillParameterKeys.RADIUS, 3f));
            Assert.IsFalse(set.Has(SkillParameterKeys.RADIUS));
        }

        [Test]
        public void Set_ThenGet_ReturnsValue()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.COEFFICIENT, 1.5f);

            Assert.AreEqual(1.5f, set.Get(SkillParameterKeys.COEFFICIENT), 0.0001f);
        }

        [Test]
        public void Apply_Flat_Adds()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.RADIUS, 3f);
            set.Apply(new SkillParameterModifier(SkillParameterKeys.RADIUS, ModifierType.Flat, 1f));

            Assert.AreEqual(4f, set.Get(SkillParameterKeys.RADIUS), 0.0001f);
        }

        [Test]
        public void Apply_Percent_Multiplies()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.COEFFICIENT, 2f);
            set.Apply(new SkillParameterModifier(SkillParameterKeys.COEFFICIENT, ModifierType.Percent, 0.5f));

            Assert.AreEqual(3f, set.Get(SkillParameterKeys.COEFFICIENT), 0.0001f);
        }

        [Test]
        public void Apply_Multiplicative_Multiplies()
        {
            var set = new SkillParameterSet();
            set.Set(SkillParameterKeys.ANGLE, 120f);
            set.Apply(new SkillParameterModifier(SkillParameterKeys.ANGLE, ModifierType.Multiplicative, 1.5f));

            Assert.AreEqual(180f, set.Get(SkillParameterKeys.ANGLE), 0.0001f);
        }

        [Test]
        public void Apply_PercentOnMissingKey_Ignored()
        {
            var set = new SkillParameterSet();
            set.Apply(new SkillParameterModifier(SkillParameterKeys.RADIUS, ModifierType.Percent, 0.5f));

            Assert.IsFalse(set.Has(SkillParameterKeys.RADIUS));
        }
    }
}
