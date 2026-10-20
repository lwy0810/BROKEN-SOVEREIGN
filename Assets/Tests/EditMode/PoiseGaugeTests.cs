using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class PoiseGaugeTests
    {
        [Test]
        public void Reduce_ToZero_Staggers_AndRefills()
        {
            var gauge = new PoiseGauge(30f);

            Assert.IsFalse(gauge.Reduce(20f, 0f, 3f));
            Assert.AreEqual(10f, gauge.Current, 0.0001f);
            Assert.IsTrue(gauge.Reduce(10f, 1f, 3f));
            Assert.IsTrue(gauge.IsStaggered(3.9f));
            Assert.IsFalse(gauge.IsStaggered(4f));
            Assert.AreEqual(30f, gauge.Current, 0.0001f);
        }

        [Test]
        public void AfterStagger_ImmuneForDuration()
        {
            var gauge = new PoiseGauge(30f, 3f, 0.1f, 10f);
            gauge.Reduce(30f, 0f, 3f);

            // 무력화 3초 + 면역 10초 = 13초까지 감소 무시
            Assert.IsFalse(gauge.Reduce(30f, 12.9f, 3f));
            Assert.AreEqual(30f, gauge.Current, 0.0001f);
            Assert.IsTrue(gauge.Reduce(30f, 13f, 3f));
        }

        [Test]
        public void EndStagger_StopsNow_ImmunityFromNow()
        {
            var gauge = new PoiseGauge(30f, 3f, 0.1f, 10f);
            gauge.Reduce(30f, 0f, 3f);
            gauge.EndStagger(1f);

            Assert.IsFalse(gauge.IsStaggered(1f));
            Assert.IsTrue(gauge.IsImmune(10.9f));
            Assert.IsFalse(gauge.IsImmune(11f));
        }

        [Test]
        public void Recover_AfterDelay_TenPercentPerSecond()
        {
            var gauge = new PoiseGauge(100f, 3f, 0.1f, 10f);
            gauge.Reduce(50f, 0f, 3f);

            gauge.Tick(2.9f, 1f);
            Assert.AreEqual(50f, gauge.Current, 0.0001f);

            gauge.Tick(3f, 1f);
            Assert.AreEqual(60f, gauge.Current, 0.0001f);
        }

        [Test]
        public void ReduceRatio_HalfOfMax()
        {
            var gauge = new PoiseGauge(300f);

            Assert.IsFalse(gauge.ReduceRatio(0.5f, 0f, 2f));
            Assert.AreEqual(150f, gauge.Current, 0.0001f);
        }
    }
}
