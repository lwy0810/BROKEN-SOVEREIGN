using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class CombatStateTrackerTests
    {
        [Test]
        public void Start_OutOfCombat()
        {
            Assert.IsFalse(new CombatStateTracker(5f).InCombat);
        }

        [Test]
        public void AfterAction_InCombatUntilDelay()
        {
            var tracker = new CombatStateTracker(5f);
            tracker.NotifyCombatAction();
            tracker.Tick(4.9f);
            Assert.IsTrue(tracker.InCombat);

            tracker.Tick(0.2f);
            Assert.IsFalse(tracker.InCombat);
        }

        [Test]
        public void NewAction_ResetsTimer()
        {
            var tracker = new CombatStateTracker(5f);
            tracker.NotifyCombatAction();
            tracker.Tick(4f);
            tracker.NotifyCombatAction();
            tracker.Tick(4f);

            Assert.IsTrue(tracker.InCombat);
        }
    }
}
