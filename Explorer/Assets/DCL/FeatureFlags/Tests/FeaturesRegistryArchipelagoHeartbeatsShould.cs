using Global.AppArgs;
using NUnit.Framework;
using System.Collections.Generic;

namespace DCL.FeatureFlags.Tests
{
    /// <summary>
    ///     The archipelago heartbeat is a kill switch: the client keeps sending it until the backend serves
    ///     <c>archipelago-heartbeats</c> as false, so a rollout stays server-controlled and a client that
    ///     resolved no flags behaves exactly as it does today.
    /// </summary>
    [TestFixture]
    public class FeaturesRegistryArchipelagoHeartbeatsShould
    {
        [SetUp]
        public void SetUp()
        {
            // The configuration is a process-wide singleton: reset before seeding so the fixture is order-independent.
            FeatureFlagsConfiguration.Reset();
        }

        [TearDown]
        public void TearDown()
        {
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void EnableHeartbeatsWhenTheFlagIsAbsent()
        {
            // Arrange
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));

            // Act
            var registry = new FeaturesRegistry(new ApplicationParametersParser(false), localSceneDevelopment: false);

            // Assert
            Assert.IsTrue(registry.IsEnabled(FeatureId.ArchipelagoHeartbeats));
        }

        [Test]
        public void EnableHeartbeatsWhenTheFlagIsServedAsTrue()
        {
            // Arrange
            InitializeFeatureFlags(archipelagoHeartbeats: true);

            // Act
            var registry = new FeaturesRegistry(new ApplicationParametersParser(false), localSceneDevelopment: false);

            // Assert
            Assert.IsTrue(registry.IsEnabled(FeatureId.ArchipelagoHeartbeats));
        }

        [Test]
        public void DisableHeartbeatsOnlyWhenTheFlagIsServedAsFalse()
        {
            // Arrange
            InitializeFeatureFlags(archipelagoHeartbeats: false);

            // Act
            var registry = new FeaturesRegistry(new ApplicationParametersParser(false), localSceneDevelopment: false);

            // Assert
            Assert.IsFalse(registry.IsEnabled(FeatureId.ArchipelagoHeartbeats));
        }

        [Test]
        public void ReadAnAbsentKillSwitchAsEnabled()
        {
            // Arrange
            var configuration = new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty);

            // Assert - the default-on read is what keeps today's behaviour; the plain overload still reads off
            Assert.IsTrue(configuration.IsEnabled(FeatureFlagsStrings.ARCHIPELAGO_HEARTBEATS, defaultWhenAbsent: true));
            Assert.IsFalse(configuration.IsEnabled(FeatureFlagsStrings.ARCHIPELAGO_HEARTBEATS));
        }

        private static void InitializeFeatureFlags(bool archipelagoHeartbeats)
        {
            var dto = new FeatureFlagsResultDto
            {
                flags = new Dictionary<string, bool> { [FeatureFlagsStrings.ARCHIPELAGO_HEARTBEATS] = archipelagoHeartbeats },
                variants = new Dictionary<string, FeatureFlagVariantDto>(),
            };

            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(dto));
        }
    }
}
