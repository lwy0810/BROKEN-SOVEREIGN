using BrokenSovereign.Skill;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class ShockwavePlanTests
    {
        [Test]
        public void RadiusAt_GrowsByStep()
        {
            var plan = new ShockwavePlan(3, 0.3f, 3f, 1.5f);

            Assert.AreEqual(3f, plan.RadiusAt(0), 0.0001f);
            Assert.AreEqual(4.5f, plan.RadiusAt(1), 0.0001f);
            Assert.AreEqual(6f, plan.RadiusAt(2), 0.0001f);
        }

        [Test]
        public void WavesDueBy_FirstWaveImmediately_ThenByInterval()
        {
            var plan = new ShockwavePlan(3, 0.3f, 3f, 1.5f);

            Assert.AreEqual(1, plan.WavesDueBy(0f));
            Assert.AreEqual(1, plan.WavesDueBy(0.29f));
            Assert.AreEqual(2, plan.WavesDueBy(0.3f));
            Assert.AreEqual(3, plan.WavesDueBy(0.6f));
            Assert.AreEqual(3, plan.WavesDueBy(5f));
        }

        [Test]
        public void WavesDueBy_ZeroInterval_AllAtOnce()
        {
            var plan = new ShockwavePlan(5, 0f, 3f, 1f);

            Assert.AreEqual(5, plan.WavesDueBy(0f));
        }

        [Test]
        public void IsLast_OnlyFinalWave()
        {
            var plan = new ShockwavePlan(5, 0.3f, 3f, 1.5f);

            Assert.IsFalse(plan.IsLast(3));
            Assert.IsTrue(plan.IsLast(4));
            Assert.AreEqual(1.2f, plan.TotalDuration, 0.0001f);
        }

        [Test]
        public void Constructor_ClampsInvalidValues()
        {
            var plan = new ShockwavePlan(0, -1f, -2f, 1f);

            Assert.AreEqual(1, plan.WaveCount);
            Assert.AreEqual(0f, plan.Interval);
            Assert.AreEqual(0f, plan.RadiusAt(0));
        }
    }
}
