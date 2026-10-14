using BrokenSovereign.Ascension;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class AscensionModelTests
    {
        [Test]
        public void Mortal_WithoutJob_CannotAscend()
        {
            Assert.IsNull(AscensionModel.GetNext(AscensionTier.Mortal, 10, false, 5, 10));
        }

        [Test]
        public void Mortal_WithJobAndLevel_BecomesSuperhuman()
        {
            Assert.IsNull(AscensionModel.GetNext(AscensionTier.Mortal, 4, true, 5, 10));
            Assert.AreEqual(AscensionTier.Superhuman, AscensionModel.GetNext(AscensionTier.Mortal, 5, true, 5, 10));
        }

        [Test]
        public void Superhuman_AtDemigodLevel_BecomesDemigod()
        {
            Assert.IsNull(AscensionModel.GetNext(AscensionTier.Superhuman, 9, true, 5, 10));
            Assert.AreEqual(AscensionTier.Demigod, AscensionModel.GetNext(AscensionTier.Superhuman, 10, true, 5, 10));
        }

        [Test]
        public void Demigod_IsPrototypeMax()
        {
            Assert.IsNull(AscensionModel.GetNext(AscensionTier.Demigod, 15, true, 5, 10));
        }
    }
}
