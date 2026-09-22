using DCL.Multiplayer.Connections.HardwareFingerprint;
using DCL.Prefs;
using DCL.Prefs.Tests;
using NUnit.Framework;

namespace DCL.Multiplayer.Connections.HardwareFingerprintTests
{
    public class HardwareFingerprintProviderShould
    {
        private InMemoryPrefsScope prefs = null!;

        [SetUp]
        public void SetUp()
        {
            prefs = new InMemoryPrefsScope();
        }

        [TearDown]
        public void TearDown()
        {
            prefs.Dispose();
        }

        [Test]
        public void ProduceLowercaseHexOfSha256Length()
        {
            //Act
            string fingerprint = new HardwareFingerprintProvider().Fingerprint;

            //Assert
            Assert.That(fingerprint, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void BeStableAcrossInstances()
        {
            //Arrange
            string first = new HardwareFingerprintProvider().Fingerprint;

            //Act
            string second = new HardwareFingerprintProvider().Fingerprint;

            //Assert
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void DifferBetweenInstallations()
        {
            //Arrange
            string first = new HardwareFingerprintProvider().Fingerprint;

            //Act
            prefs.Reset();
            string second = new HardwareFingerprintProvider().Fingerprint;

            //Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void KeepTheInstallationIdOffTheWire()
        {
            //Act
            string fingerprint = new HardwareFingerprintProvider().Fingerprint;

            //Assert
            Assert.That(fingerprint, Is.Not.EqualTo(DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID)));
        }
    }
}
