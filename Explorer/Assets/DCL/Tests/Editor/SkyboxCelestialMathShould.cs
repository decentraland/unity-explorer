using DCL.SkyBox;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Tests
{
    [TestFixture]
    public class SkyboxCelestialMathShould
    {
        private const float TOLERANCE = 1e-4f;

        // Sun up from 06:00 to 18:00.
        private const float RISE = 0.25f;
        private const float SET = 0.75f;

        // Moon up from 21:36 to 04:48, one-hour crossovers (window [20:36, 21:36) and [04:48, 05:48)).
        private const float MOONRISE = 0.9f;
        private const float MOONSET = 0.2f;
        private const float SWAP = 0.1f;

        [TestCase(0.25f, 0f)]
        [TestCase(0.5f, 0.5f)]
        [TestCase(0.75f, 1f)]
        [TestCase(0.875f, 1.25f)]
        [TestCase(0f, 1.5f)]
        [TestCase(0.125f, 1.75f)]
        public void ProgressRunsRiseToSetThenBelowTheHorizon(float timeOfDay, float expected) =>
            Assert.That(SkyboxCelestialMath.CelestialProgress(timeOfDay, RISE, SET), Is.EqualTo(expected).Within(TOLERANCE));

        [TestCase(0.9f, 0f)]
        [TestCase(0f, 0.5f)]
        [TestCase(0.1f, 1f)]
        [TestCase(0.5f, 1.5f)]
        public void ProgressWrapsAcrossMidnightWhenSetIsBeforeRise(float timeOfDay, float expected) =>
            Assert.That(SkyboxCelestialMath.CelestialProgress(timeOfDay, 0.9f, 0.1f), Is.EqualTo(expected).Within(TOLERANCE));

        [TestCase(0.8f, 0f, 0f)]
        [TestCase(0.85f, 0.5f, 1f)]
        [TestCase(0.9f, 1f, 0f)]
        [TestCase(0.05f, 1f, 0f)]
        [TestCase(0.25f, 0.5f, 1f)]
        [TestCase(0.3f, 0f, 0f)]
        [TestCase(0.5f, 0f, 0f)]
        public void SwapSwingsInsideTheWindowsAndHoldsOutside(float timeOfDay, float expectedWeight, float expectedDip)
        {
            SkyboxCelestialMath.EvaluateSwap(timeOfDay, MOONRISE, MOONSET, SWAP, out float moonWeight, out float dip);

            Assert.That(moonWeight, Is.EqualTo(expectedWeight).Within(TOLERANCE));
            Assert.That(dip, Is.EqualTo(expectedDip).Within(TOLERANCE));
        }

        [TestCase(0f, 0f)]
        [TestCase(0.125f, 0.5f)]
        [TestCase(0.25f, 1f)]
        [TestCase(0.5f, 1f)]
        [TestCase(0.875f, 0.5f)]
        [TestCase(1f, 0f)]
        public void DipRampsInAndOutAtTheWindowEdges(float progress, float expected) =>
            Assert.That(SkyboxCelestialMath.SwapDip(progress), Is.EqualTo(expected).Within(TOLERANCE));

        [Test]
        public void ArcStartsAtTheRisePointPeaksOverheadAndSetsOpposite()
        {
            AssertDirection(SkyboxCelestialMath.ArcDirection(0f, 0f, 0f), Vector3.forward);
            AssertDirection(SkyboxCelestialMath.ArcDirection(0.5f, 0f, 0f), Vector3.up);
            AssertDirection(SkyboxCelestialMath.ArcDirection(1f, 0f, 0f), Vector3.back);
            AssertDirection(SkyboxCelestialMath.ArcDirection(1.5f, 0f, 0f), Vector3.down);
            AssertDirection(SkyboxCelestialMath.ArcDirection(0f, 90f, 0f), Vector3.right);
        }

        [Test]
        public void ArcTiltLeansThePeakSideways()
        {
            Vector3 peak = SkyboxCelestialMath.ArcDirection(0.5f, 0f, 40f);

            Assert.That(peak.y, Is.EqualTo(Mathf.Cos(40f * Mathf.Deg2Rad)).Within(TOLERANCE));
            Assert.That(peak.x, Is.EqualTo(Mathf.Sin(40f * Mathf.Deg2Rad)).Within(TOLERANCE));
            Assert.That(peak.magnitude, Is.EqualTo(1f).Within(TOLERANCE));
        }

        private static void AssertDirection(Vector3 actual, Vector3 expected)
        {
            Assert.That(actual.x, Is.EqualTo(expected.x).Within(TOLERANCE));
            Assert.That(actual.y, Is.EqualTo(expected.y).Within(TOLERANCE));
            Assert.That(actual.z, Is.EqualTo(expected.z).Within(TOLERANCE));
        }
    }
}
