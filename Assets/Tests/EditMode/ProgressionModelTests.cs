using BrokenSovereign.Ascension;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class ProgressionModelTests
    {
        [Test]
        public void RequiredFor_GrowsPerLevel()
        {
            var model = new ProgressionModel(100, 1.25f, 15);

            Assert.AreEqual(100, model.RequiredFor(1));
            Assert.AreEqual(125, model.RequiredFor(2));
            Assert.AreEqual(156, model.RequiredFor(3));
        }

        [Test]
        public void Add_BelowRequirement_NoLevelUp()
        {
            var model = new ProgressionModel(100, 1.25f, 15);

            Assert.AreEqual(0, model.Add(99));
            Assert.AreEqual(1, model.Level);
            Assert.AreEqual(99, model.Experience);
        }

        [Test]
        public void Add_CarriesOverflowAndMultipleLevels()
        {
            var model = new ProgressionModel(100, 1.25f, 15);

            Assert.AreEqual(2, model.Add(100 + 125 + 10));
            Assert.AreEqual(3, model.Level);
            Assert.AreEqual(10, model.Experience);
        }

        [Test]
        public void Add_StopsAtMaxLevel()
        {
            var model = new ProgressionModel(100, 1.25f, 3);

            model.Add(100000);

            Assert.AreEqual(3, model.Level);
            Assert.IsTrue(model.IsMaxLevel);
            Assert.AreEqual(0, model.Experience);
            Assert.AreEqual(0, model.Add(50));
            Assert.AreEqual(1f, model.Progress);
        }

        [Test]
        public void Add_ZeroOrNegative_Ignored()
        {
            var model = new ProgressionModel(100, 1.25f, 15);

            Assert.AreEqual(0, model.Add(0));
            Assert.AreEqual(0, model.Add(-10));
            Assert.AreEqual(0, model.Experience);
        }
    }
}
