using BrokenSovereign.Combat;
using NUnit.Framework;

namespace BrokenSovereign.Tests
{
    public class AirborneArcTests
    {
        [Test]
        public void HeightAt_PeaksAtMiddle()
        {
            Assert.AreEqual(1.5f, AirborneArc.HeightAt(0.4f, 0.8f, 1.5f), 0.0001f);
        }

        [Test]
        public void HeightAt_ZeroAtStartAndEnd()
        {
            Assert.AreEqual(0f, AirborneArc.HeightAt(0f, 0.8f, 1.5f));
            Assert.AreEqual(0f, AirborneArc.HeightAt(0.8f, 0.8f, 1.5f));
            Assert.AreEqual(0f, AirborneArc.HeightAt(1f, 0.8f, 1.5f));
        }

        [Test]
        public void HeightAt_SymmetricAroundMiddle()
        {
            Assert.AreEqual(AirborneArc.HeightAt(0.2f, 0.8f, 1.5f), AirborneArc.HeightAt(0.6f, 0.8f, 1.5f), 0.0001f);
        }

        [Test]
        public void HeightAt_ZeroDuration_Zero()
        {
            Assert.AreEqual(0f, AirborneArc.HeightAt(0.1f, 0f, 1.5f));
        }
    }
}
