using NUnit.Framework;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class AbgenConversionMetricsShould
    {
        [Test]
        public void KeepFirstRawGltfFallbackReasonAndRequestPanelOnce()
        {
            // Arrange
            var metrics = new AbgenConversionMetrics();
            metrics.OnRawGltfFallback("first");
            Assert.IsTrue(metrics.TryConsumePanelOpenRequest());

            // Act
            metrics.OnRawGltfFallback("second");

            // Assert
            Assert.AreEqual("first", metrics.RawGltfFallbackReason);
            Assert.IsFalse(metrics.TryConsumePanelOpenRequest());
        }

        [Test]
        public void NotCountUnknownFailedFileAsInFlight()
        {
            // Arrange
            var metrics = new AbgenConversionMetrics();

            // Act
            metrics.OnFailed("models/tree.glb", "bundle missing");

            // Assert
            Assert.AreEqual(1, metrics.Failed);
            Assert.AreEqual(0, metrics.InFlight);
        }

        [Test]
        public void CountRepeatedFailureOfSameFileOnce()
        {
            // Arrange
            var metrics = new AbgenConversionMetrics();
            metrics.OnStarted("models/tree.glb");

            // Act
            metrics.OnFailed("models/tree.glb", "not converted");
            metrics.OnFailed("models/tree.glb", "loaded as raw GLTF");

            // Assert
            Assert.AreEqual(1, metrics.Failed);
            Assert.AreEqual(0, metrics.InFlight);
        }
    }
}
