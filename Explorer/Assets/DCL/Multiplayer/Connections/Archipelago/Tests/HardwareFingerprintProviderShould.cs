using DCL.Multiplayer.Connections.HardwareFingerprint;
using NUnit.Framework;
using UnityEngine;

namespace DCL.Multiplayer.Connections.HardwareFingerprintTests
{
    public class HardwareFingerprintProviderShould
    {
        private const string SAMPLE_ID = "abc123-device-identifier";

        [Test]
        public void ProduceLowercaseHexOfSha256Length()
        {
            string fingerprint = HardwareFingerprintProvider.ComputeFingerprint(SAMPLE_ID);

            Assert.That(fingerprint.Length, Is.EqualTo(64));
            Assert.That(fingerprint, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void BeStableForTheSameInput()
        {
            Assert.That(
                HardwareFingerprintProvider.ComputeFingerprint(SAMPLE_ID),
                Is.EqualTo(HardwareFingerprintProvider.ComputeFingerprint(SAMPLE_ID)));
        }

        [Test]
        public void DifferForDifferentInputs()
        {
            Assert.That(
                HardwareFingerprintProvider.ComputeFingerprint(SAMPLE_ID),
                Is.Not.EqualTo(HardwareFingerprintProvider.ComputeFingerprint("a-different-device")));
        }

        [Test]
        public void NormalizeCaseAndWhitespace()
        {
            string fingerprint = HardwareFingerprintProvider.ComputeFingerprint(SAMPLE_ID);

            Assert.That(HardwareFingerprintProvider.ComputeFingerprint($"  {SAMPLE_ID.ToUpperInvariant()}  "), Is.EqualTo(fingerprint));
        }

        [Test]
        public void ReturnEmptyForUnsupportedIdentifier()
        {
            Assert.That(HardwareFingerprintProvider.ComputeFingerprint(SystemInfo.unsupportedIdentifier), Is.Empty);
        }

        [Test]
        public void ReturnEmptyForMissingIdentifier()
        {
            Assert.That(HardwareFingerprintProvider.ComputeFingerprint(null), Is.Empty);
            Assert.That(HardwareFingerprintProvider.ComputeFingerprint(string.Empty), Is.Empty);
        }

        [Test]
        public void ExposeEmptyFingerprintFromNullObject()
        {
            Assert.That(IHardwareFingerprintProvider.EMPTY.Fingerprint, Is.Empty);
        }
    }
}
