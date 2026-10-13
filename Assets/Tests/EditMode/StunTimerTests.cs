using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class StunTimerTests
    {
        [Test]
        public void Apply_StunnedUntilDuration()
        {
            var timer = new StunTimer();

            Assert.IsTrue(timer.Apply(0.5f, 10f));
            Assert.IsTrue(timer.IsStunned(10.49f));
            Assert.IsFalse(timer.IsStunned(10.5f));
        }

        [Test]
        public void Apply_ShorterWhileStunned_KeepsLongerEnd()
        {
            var timer = new StunTimer();
            timer.Apply(2f, 10f);

            Assert.IsFalse(timer.Apply(0.5f, 10.5f));
            Assert.AreEqual(12f, timer.EndTime, 0.0001f);
        }

        [Test]
        public void Apply_LongerWhileStunned_Extends()
        {
            var timer = new StunTimer();
            timer.Apply(0.5f, 10f);

            Assert.IsTrue(timer.Apply(1f, 10.2f));
            Assert.AreEqual(11.2f, timer.EndTime, 0.0001f);
        }

        [Test]
        public void ApplyZero_Ignored()
        {
            var timer = new StunTimer();

            Assert.IsFalse(timer.Apply(0f, 10f));
            Assert.IsFalse(timer.IsStunned(10f));
        }
    }
}
