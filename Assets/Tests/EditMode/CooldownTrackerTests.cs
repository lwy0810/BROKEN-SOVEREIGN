using BrokenSovereign.Skill;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class CooldownTrackerTests
    {
        [Test]
        public void NotStarted_IsReady()
        {
            var tracker = new CooldownTracker();

            Assert.IsTrue(tracker.IsReady("a"));
            Assert.AreEqual(0f, tracker.Remaining("a"));
        }

        [Test]
        public void Started_NotReadyUntilElapsed()
        {
            var tracker = new CooldownTracker();
            tracker.Start("a", 4f);
            tracker.Tick(3.9f);

            Assert.IsFalse(tracker.IsReady("a"));
            Assert.AreEqual(0.1f, tracker.Remaining("a"), 0.0001f);
            Assert.AreEqual(4f, tracker.Duration("a"), 0.0001f);

            tracker.Tick(0.1f);
            Assert.IsTrue(tracker.IsReady("a"));
        }

        [Test]
        public void Skills_TrackedIndependently()
        {
            var tracker = new CooldownTracker();
            tracker.Start("a", 4f);
            tracker.Start("b", 1f);
            tracker.Tick(1f);

            Assert.IsFalse(tracker.IsReady("a"));
            Assert.IsTrue(tracker.IsReady("b"));
        }

        [Test]
        public void StartZero_StaysReady()
        {
            var tracker = new CooldownTracker();
            tracker.Start("a", 0f);

            Assert.IsTrue(tracker.IsReady("a"));
        }
    }
}
