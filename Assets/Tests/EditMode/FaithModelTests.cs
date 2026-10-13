using BrokenSovereign.Skill;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class FaithModelTests
    {
        // 최대 100, 비전투 초당 5 감소, 버프 10초
        private static FaithModel Create()
        {
            return new FaithModel(100f, 5f, 10f);
        }

        [Test]
        public void Gain_BelowMax_NoBuff()
        {
            FaithModel faith = Create();

            Assert.IsFalse(faith.Gain(95f));
            Assert.AreEqual(95f, faith.Current, 0.0001f);
            Assert.IsFalse(faith.IsBuffActive);
        }

        [Test]
        public void Gain_ReachMax_StartsBuff()
        {
            FaithModel faith = Create();
            faith.Gain(98f);

            Assert.IsTrue(faith.Gain(5f));
            Assert.IsTrue(faith.IsBuffActive);
            Assert.AreEqual(100f, faith.Current, 0.0001f);
            Assert.AreEqual(10f, faith.BuffRemaining, 0.0001f);
        }

        [Test]
        public void Gain_DuringBuff_Ignored()
        {
            FaithModel faith = Create();
            faith.Gain(100f);
            faith.Tick(5f, true);

            Assert.IsFalse(faith.Gain(50f));
            Assert.AreEqual(50f, faith.Current, 0.0001f);
        }

        [Test]
        public void Tick_DuringBuff_DrainsByRemainingTime_ThenEnds()
        {
            FaithModel faith = Create();
            faith.Gain(100f);

            Assert.IsFalse(faith.Tick(2.5f, true));
            Assert.AreEqual(75f, faith.Current, 0.0001f);

            Assert.IsTrue(faith.Tick(7.5f, true));
            Assert.IsFalse(faith.IsBuffActive);
            Assert.AreEqual(0f, faith.Current, 0.0001f);
        }

        [Test]
        public void Tick_InCombat_NoDecay()
        {
            FaithModel faith = Create();
            faith.Gain(50f);
            faith.Tick(2f, true);

            Assert.AreEqual(50f, faith.Current, 0.0001f);
        }

        [Test]
        public void Tick_OutOfCombat_DecaysToZero()
        {
            FaithModel faith = Create();
            faith.Gain(8f);
            faith.Tick(1f, false);
            Assert.AreEqual(3f, faith.Current, 0.0001f);

            faith.Tick(1f, false);
            Assert.AreEqual(0f, faith.Current, 0.0001f);
        }

        [Test]
        public void Reset_ClearsGaugeAndBuff()
        {
            FaithModel faith = Create();
            faith.Gain(100f);
            faith.Reset();

            Assert.IsFalse(faith.IsBuffActive);
            Assert.AreEqual(0f, faith.Current, 0.0001f);
        }
    }
}
