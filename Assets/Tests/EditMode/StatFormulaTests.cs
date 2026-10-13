using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class StatFormulaTests
    {
        private const float TOLERANCE = 0.0001f;

        [Test]
        public void Converge_Zero_ReturnsZero()
        {
            Assert.AreEqual(0f, StatFormula.Converge(0f, 0.5f, 1000f), TOLERANCE);
        }

        [Test]
        public void Converge_AtK_ReturnsHalfOfMax()
        {
            Assert.AreEqual(0.25f, StatFormula.Converge(1000f, 0.5f, 1000f), TOLERANCE);
        }

        [Test]
        public void Converge_VeryLarge_StaysBelowMax()
        {
            float value = StatFormula.Converge(1_000_000f, 0.5f, 1000f);
            Assert.Less(value, 0.5f);
            Assert.Greater(value, 0.49f);
        }

        [Test]
        public void ArmorReduction_VeryLarge_CappedAt85Percent()
        {
            Assert.AreEqual(0.85f, StatFormula.ArmorReduction(1_000_000f), TOLERANCE);
        }

        [Test]
        public void ArmorReduction_AtK_ReturnsHalf()
        {
            Assert.AreEqual(0.5f, StatFormula.ArmorReduction(1000f), TOLERANCE);
        }

        [Test]
        public void RollCrit_ZeroChance_NeverCrits()
        {
            Assert.IsFalse(StatFormula.RollCrit(0f, 0f));
        }

        [Test]
        public void RollCrit_FullChance_AlwaysCrits()
        {
            Assert.IsTrue(StatFormula.RollCrit(1f, 0.9999f));
        }
    }
}
