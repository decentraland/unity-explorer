using DCL.AvatarRendering.Wearables.Helpers;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableAssetShould
    {
        [TestCase(0)]
        [TestCase(5)]
        public void ProperlyCountReferenceWhenAddReferenceCalled(int refCount)
        {
            // Arrange
            var wearableAsset = new WearableRegularAsset(new GameObject(), new List<WearableRegularAsset.RendererInfo>(5), null);

            // Act
            for (var i = 0; i < refCount; i++)
                wearableAsset.AddReference();

            // Assert
            Assert.That(wearableAsset.ReferenceCount, Is.EqualTo(refCount));
        }

        [TestCase(13, 3, 10)]
        [TestCase(5, 5, 0)]
        [TestCase(0, 0, 0)]
        public void ProperlyRemoveReferenceWhenDereferenced(int initialRefs, int derefs, int remainedRefs)
        {
            // Arrange
            var wearableAsset = new WearableRegularAsset(new GameObject(), new List<WearableRegularAsset.RendererInfo>(5), null);

            for (var i = 0; i < initialRefs; i++)
                wearableAsset.AddReference();

            // Act
            for (var i = 0; i < derefs; i++)
                wearableAsset.Dereference();

            // Assert
            Assert.That(wearableAsset.ReferenceCount, Is.EqualTo(remainedRefs));
        }
    }
}
