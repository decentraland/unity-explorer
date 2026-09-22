using DCL.Multiplayer.Connections.HardwareFingerprint;
using DCL.Prefs;
using NUnit.Framework;
using System;
using System.Reflection;

namespace DCL.Multiplayer.Connections.HardwareFingerprintTests
{
    public class HardwareFingerprintProviderShould
    {
        private static readonly FieldInfo PREFS_FIELD =
            typeof(DCLPlayerPrefs).GetField("dclPrefs", BindingFlags.NonPublic | BindingFlags.Static)!;

        private object? originalPrefs;

        [SetUp]
        public void SetUp()
        {
            originalPrefs = PREFS_FIELD.GetValue(null);
            GivenAFreshInstallation();
        }

        [TearDown]
        public void TearDown()
        {
            PREFS_FIELD.SetValue(null, originalPrefs);
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
        public void PersistAGeneratedIdentifier()
        {
            //Act
            _ = new HardwareFingerprintProvider();

            //Assert
            Assert.That(Guid.TryParse(DCLPlayerPrefs.GetString(DCLPrefKeys.HARDWARE_FINGERPRINT_ID), out _), Is.True);
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
        public void ReuseThePersistedIdentifier()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.HARDWARE_FINGERPRINT_ID, "already-persisted");

            //Act
            _ = new HardwareFingerprintProvider();

            //Assert
            Assert.That(DCLPlayerPrefs.GetString(DCLPrefKeys.HARDWARE_FINGERPRINT_ID), Is.EqualTo("already-persisted"));
        }

        [Test]
        public void DifferBetweenInstallations()
        {
            //Arrange
            string first = new HardwareFingerprintProvider().Fingerprint;

            //Act
            GivenAFreshInstallation();
            string second = new HardwareFingerprintProvider().Fingerprint;

            //Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void KeepThePersistedIdentifierOffTheWire()
        {
            //Act
            string fingerprint = new HardwareFingerprintProvider().Fingerprint;

            //Assert
            Assert.That(fingerprint, Is.Not.EqualTo(DCLPlayerPrefs.GetString(DCLPrefKeys.HARDWARE_FINGERPRINT_ID)));
        }

        private static void GivenAFreshInstallation() =>
            PREFS_FIELD.SetValue(null, new InMemoryDCLPlayerPrefs());
    }
}
