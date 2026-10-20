using BrokenSovereign.Combat;
using BrokenSovereign.Hero;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class ReachShiftTests
    {
        [Test]
        public void Long_RangeAndLine()
        {
            BasicAttackProfile profile = BasicAttackProfile.Default;
            ReachShift.Apply(StaffMode.Long, ReachShiftSettings.Default, ref profile);

            Assert.AreEqual(1.5f, profile.RangeBonus, 0.0001f);
            Assert.AreEqual(0.75f, profile.CoefficientMultiplier, 0.0001f);
            Assert.IsTrue(profile.IsLine);
            Assert.AreEqual(3, profile.MaxTargets);
            Assert.AreEqual(0f, profile.ArmorPenetration, 0.0001f);
        }

        [Test]
        public void Short_DamageAndPenetration()
        {
            BasicAttackProfile profile = BasicAttackProfile.Default;
            ReachShift.Apply(StaffMode.Short, ReachShiftSettings.Default, ref profile);

            Assert.AreEqual(0f, profile.RangeBonus, 0.0001f);
            Assert.AreEqual(1.3f, profile.CoefficientMultiplier, 0.0001f);
            Assert.IsFalse(profile.IsLine);
            Assert.AreEqual(0.3f, profile.ArmorPenetration, 0.0001f);
        }

        [Test]
        public void Toggle_Alternates()
        {
            Assert.AreEqual(StaffMode.Long, ReachShift.Toggle(StaffMode.Short));
            Assert.AreEqual(StaffMode.Short, ReachShift.Toggle(StaffMode.Long));
        }
    }
}
