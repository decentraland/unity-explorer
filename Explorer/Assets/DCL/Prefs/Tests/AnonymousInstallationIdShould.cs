using NUnit.Framework;
using System;

namespace DCL.Prefs.Tests
{
    public class AnonymousInstallationIdShould
    {
        private IDCLPrefs originalPrefs = null!;

        [SetUp]
        public void SetUp()
        {
            originalPrefs = DCLPlayerPrefs.SwapForTests(new InMemoryDCLPlayerPrefs());
        }

        [TearDown]
        public void TearDown()
        {
            DCLPlayerPrefs.SwapForTests(originalPrefs);
        }

        [Test]
        public void GenerateAndPersistAnIdentifier()
        {
            //Act
            string resolved = AnonymousInstallationId.Resolve();

            //Assert
            Assert.That(Guid.TryParse(resolved, out _), Is.True);
            Assert.That(DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID), Is.EqualTo(resolved));
        }

        [Test]
        public void ReuseThePersistedIdentifier()
        {
            //Arrange
            string first = AnonymousInstallationId.Resolve();

            //Act
            string second = AnonymousInstallationId.Resolve();

            //Assert
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void AdoptTheLegacyFeatureFlagsIdentifier()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.LEGACY_FEATURE_FLAGS_USER_ID, "already-bucketed");

            //Act
            string resolved = AnonymousInstallationId.Resolve();

            //Assert
            Assert.That(resolved, Is.EqualTo("already-bucketed"));
            Assert.That(DCLPlayerPrefs.GetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID), Is.EqualTo("already-bucketed"));
        }

        [Test]
        public void PreferThePersistedIdentifierOverTheLegacyOne()
        {
            //Arrange
            DCLPlayerPrefs.SetString(DCLPrefKeys.ANONYMOUS_INSTALLATION_ID, "current");
            DCLPlayerPrefs.SetString(DCLPrefKeys.LEGACY_FEATURE_FLAGS_USER_ID, "already-bucketed");

            //Act
            string resolved = AnonymousInstallationId.Resolve();

            //Assert
            Assert.That(resolved, Is.EqualTo("current"));
        }

        [Test]
        public void DifferBetweenInstallations()
        {
            //Arrange
            string first = AnonymousInstallationId.Resolve();

            //Act
            DCLPlayerPrefs.SwapForTests(new InMemoryDCLPlayerPrefs());
            string second = AnonymousInstallationId.Resolve();

            //Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void ProduceFingerprintAsLowercaseHexOfSha256Length()
        {
            //Act
            string fingerprint = AnonymousInstallationId.ResolveFingerprint();

            //Assert
            Assert.That(fingerprint, Does.Match("^[0-9a-f]{64}$"));
        }

        [Test]
        public void KeepTheFingerprintStable()
        {
            //Arrange
            string first = AnonymousInstallationId.ResolveFingerprint();

            //Act
            string second = AnonymousInstallationId.ResolveFingerprint();

            //Assert
            Assert.That(second, Is.EqualTo(first));
        }

        [Test]
        public void DifferFingerprintBetweenInstallations()
        {
            //Arrange
            string first = AnonymousInstallationId.ResolveFingerprint();

            //Act
            DCLPlayerPrefs.SwapForTests(new InMemoryDCLPlayerPrefs());
            string second = AnonymousInstallationId.ResolveFingerprint();

            //Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }

        [Test]
        public void KeepTheIdentifierOutOfTheFingerprint()
        {
            //Act
            string fingerprint = AnonymousInstallationId.ResolveFingerprint();

            //Assert
            Assert.That(fingerprint, Is.Not.EqualTo(AnonymousInstallationId.Resolve()));
        }
    }
}
