using NUnit.Framework;

namespace ECS.StreamableLoading.AssetBundles.Tests
{
    public class AssetBundleDataShould
    {


        public void ProperlyCountReferenceWhenAddReferenceCalled(int refCount)
        {
            // Arrange
            var assetBundleData = new AssetBundleData(null, null, null, null);

            // Act
            for (var i = 0; i < refCount; i++)
                assetBundleData.AddReference();

            // Assert
            Assert.That(assetBundleData.referencesCount, Is.EqualTo(refCount));
        }




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
