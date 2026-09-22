using NUnit.Framework;
using System;

namespace DCL.Prefs.Tests
{
    public class AnonymousInstallationIdShould
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
            prefs.Reset();
            string second = AnonymousInstallationId.Resolve();

            //Assert
            Assert.That(second, Is.Not.EqualTo(first));
        }
    }
}
