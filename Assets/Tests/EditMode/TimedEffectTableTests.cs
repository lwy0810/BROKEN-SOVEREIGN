using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class TimedEffectTableTests
    {
        [Test]
        public void Apply_New_ReturnsTrue_ActiveUntilDuration()
        {
            var table = new TimedEffectTable();

            Assert.IsTrue(table.Apply("battle_cry", 10f, 0f));
            Assert.IsTrue(table.IsActive("battle_cry", 9.9f));
            Assert.IsFalse(table.IsActive("battle_cry", 10f));
        }

        [Test]
        public void Apply_Again_RefreshesWithoutStacking()
        {
            var table = new TimedEffectTable();
            table.Apply("battle_cry", 10f, 0f);

            Assert.IsFalse(table.Apply("battle_cry", 10f, 5f));
            Assert.AreEqual(1, table.Count);
            Assert.AreEqual(10f, table.Remaining("battle_cry", 5f), 0.0001f);
        }

        [Test]
        public void Apply_Shorter_KeepsLongerEnd()
        {
            var table = new TimedEffectTable();
            table.Apply("battle_cry", 10f, 0f);
            table.Apply("battle_cry", 2f, 1f);

            Assert.AreEqual(9f, table.Remaining("battle_cry", 1f), 0.0001f);
        }

        [Test]
        public void RemoveExpired_ReturnsOnlyEnded()
        {
            var table = new TimedEffectTable();
            table.Apply("a", 1f, 0f);
            table.Apply("b", 5f, 0f);

            var expired = table.RemoveExpired(2f);

            Assert.AreEqual(1, expired.Count);
            Assert.AreEqual("a", expired[0]);
            Assert.AreEqual(1, table.Count);
        }
    }
}
