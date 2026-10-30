using Global.AppArgs;
using NUnit.Framework;

namespace DCL.FeatureFlags.Tests
{
    /// <summary>
    ///     Local scene development resolves no remote feature flags, so Pulse cannot be driven by one there.
    ///     It defaults on instead, and <c>--pulse false</c> is the way back to LiveKit-only.
    /// </summary>
    [TestFixture]
    public class FeaturesRegistryPulseShould
    {
        [SetUp]
        public void SetUp()
        {
            // The configuration is a process-wide singleton: reset before seeding so the fixture is order-independent.
            FeatureFlagsConfiguration.Reset();
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
        }

        [TearDown]
        public void TearDown()
        {
            FeatureFlagsConfiguration.Reset();
        }

        [Test]
        public void EnablePulseByDefaultInLocalSceneDevelopment()
        {
            // Arrange
            var appArgs = new ApplicationParametersParser(false);

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: true);

            // Assert
            Assert.IsTrue(registry.IsEnabled(FeatureId.Pulse));
        }

        [Test]
        public void DisablePulseInLocalSceneDevelopmentWhenTheArgOptsOut()
        {
            // Arrange
            var appArgs = new ApplicationParametersParser(false, "--pulse", "false");

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: true);

            // Assert
            Assert.IsFalse(registry.IsEnabled(FeatureId.Pulse));
        }

        [Test]
        public void KeepPulseDrivenByTheRemoteFlagOutsideLocalSceneDevelopment()
        {
            // Arrange
            var appArgs = new ApplicationParametersParser(false);

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: false);

            // Assert — the flag is absent from the empty configuration
            Assert.IsFalse(registry.IsEnabled(FeatureId.Pulse));
        }

        [Test]
        public void EnablePulseOutsideLocalSceneDevelopmentWhenTheArgOptsIn()
        {
            // Arrange
            var appArgs = new ApplicationParametersParser(false, "--pulse", "true");

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: false);

            // Assert
            Assert.IsTrue(registry.IsEnabled(FeatureId.Pulse));
        }
    }
}
