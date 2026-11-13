using Global.AppArgs;
using Newtonsoft.Json;
using NUnit.Framework;

namespace DCL.FeatureFlags.Tests
{
    [TestFixture]
    public class FeaturesRegistrySceneItemPurchaseShould
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
        public void EnableSceneItemPurchaseWhenTheRemoteFlagIsOn()
        {
            // Arrange
            SeedRemoteFlag(true);
            var appArgs = new ApplicationParametersParser(false);

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: false);

            // Assert
            Assert.IsTrue(registry.IsEnabled(FeatureId.SceneItemPurchase));
        }

        [Test]
        public void DisableSceneItemPurchaseWhenTheArgOptsOutOverAnEnabledRemoteFlag()
        {
            // Arrange
            SeedRemoteFlag(true);
            var appArgs = new ApplicationParametersParser(false, "--scene-item-purchase", "false");

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: false);

            // Assert
            Assert.IsFalse(registry.IsEnabled(FeatureId.SceneItemPurchase));
        }

        [Test]
        public void EnableSceneItemPurchaseWhenTheArgOptsInWithDebug()
        {
            // Arrange
            SeedRemoteFlag(false);
            var appArgs = new ApplicationParametersParser(false, "--debug", "--scene-item-purchase");

            // Act
            var registry = new FeaturesRegistry(appArgs, localSceneDevelopment: false);

            // Assert
            Assert.IsTrue(registry.IsEnabled(FeatureId.SceneItemPurchase));
        }

        private static void SeedRemoteFlag(bool enabled)
        {
            string json = $"{{\"flags\":{{\"{FeatureFlagsStrings.SCENE_ITEM_PURCHASE}\":{(enabled ? "true" : "false")}}},\"variants\":{{}}}}";
            FeatureFlagsConfiguration.Initialize(new FeatureFlagsConfiguration(JsonConvert.DeserializeObject<FeatureFlagsResultDto>(json)));
        }
    }
}
