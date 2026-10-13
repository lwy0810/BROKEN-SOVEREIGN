using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class DamageCalculatorTests
    {
        [Test]
        public void Calculate_BaseOnly_ReturnsPowerTimesCoefficient()
        {
            float result = DamageCalculator.Calculate(new DamageRequest(100f, 1.5f));
            Assert.AreEqual(150f, result, 0.001f);
        }

        [Test]
        public void Calculate_WithBonusAndReduction_AppliesBoth()
        {
            // 100 × 1 × 1.2 × 0.7 = 84
            float result = DamageCalculator.Calculate(new DamageRequest(100f, 1f, 0.2f, 0.3f));
            Assert.AreEqual(84f, result, 0.001f);
        }

        [Test]
        public void Calculate_ReductionOverOne_ReturnsZero()
        {
            float result = DamageCalculator.Calculate(new DamageRequest(100f, 1f, 0f, 1.5f));
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Calculate_NegativeBonusBelowMinusOne_ReturnsZero()
        {
            float result = DamageCalculator.Calculate(new DamageRequest(100f, 1f, -2f));
            Assert.AreEqual(0f, result, 0.001f);
        }

        [Test]
        public void Calculate_WithMainStatAndCrit_MultipliesBoth()
        {
            // 100 × 1 × 1.2 × 2(주 스탯) × 1.5(치명타) = 360
            float result = DamageCalculator.Calculate(new DamageRequest(100f, 1f, 0.2f, 0f, 2f, 1.5f));
            Assert.AreEqual(360f, result, 0.001f);
        }

        [Test]
        public void ApplyMitigation_HalvesDamage()
        {
            Assert.AreEqual(50f, DamageCalculator.ApplyMitigation(100f, 0.5f), 0.001f);
        }
    }
}
