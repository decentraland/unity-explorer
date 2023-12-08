using NUnit.Framework;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class AssetBundleDataShould
    {
        private const int REF_COUNT = 5;

        [Test]
        public void ProperlyCountReferenceWhenAddReferenceCalled()
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, null);

            // Act
            for (var i = 0; i < REF_COUNT; i++)
                assetBundleData.AddReference();

            // Assert
            Assert.That(assetBundleData.referencesCount, Is.EqualTo(REF_COUNT));
        }

        [TestCase(13, 3, 10)]
        [TestCase(5, 5, 0)]
        [TestCase(0, 0, 0)]
        public void ProperlyRemoveReferenceWhenDereferenced(int initialRefs, int derefs, int remainedRefs)
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, null);

            for (var i = 0; i < initialRefs; i++)
                assetBundleData.AddReference();

            // Act
            for (var i = 0; i < derefs; i++)
                assetBundleData.Dereference();

            // Assert
            Assert.That(assetBundleData.referencesCount, Is.EqualTo(remainedRefs));
        }

        [TestCase(5, false)]
        [TestCase(0, true)]
        public void CannotBeDisposedWhenStillReferenced(int refCount, bool canBeDisposed)
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, null);

            for (var i = 0; i < refCount; i++)
                assetBundleData.AddReference();

            // Act-Assert
            Assert.That(assetBundleData.CanBeDisposed(), Is.EqualTo(canBeDisposed));
        }
    }
}
