using DCL.AvatarRendering.Loading.Assets;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableAssetShould
    {
        private readonly List<Object> createdObjects = new ();

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in createdObjects)
                Object.DestroyImmediate(createdObject);

            createdObjects.Clear();
        }

        [TestCase(0)]
        [TestCase(5)]
        public void ProperlyCountReferenceWhenAddReferenceCalled(int refCount)
        {
            // Arrange
            var wearableAsset = new AttachmentRegularAsset(new GameObject(), new List<AttachmentRegularAsset.RendererInfo>(5), null);

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
            var wearableAsset = new AttachmentRegularAsset(new GameObject(), new List<AttachmentRegularAsset.RendererInfo>(5), null);

            for (var i = 0; i < initialRefs; i++)
                wearableAsset.AddReference();

            // Act
            for (var i = 0; i < derefs; i++)
                wearableAsset.Dereference();

            // Assert
            Assert.That(wearableAsset.ReferenceCount, Is.EqualTo(remainedRefs));
        }

        [Test]
        public void MarkTangentsOfTheSameMeshOnlyOnce()
        {
            // Arrange
            var wearableAsset = new AttachmentRegularAsset(new GameObject(), new List<AttachmentRegularAsset.RendererInfo>(5), IStreamableRefCountData.Null.INSTANCE);
            var mesh = new Mesh();
            var anotherMesh = new Mesh();
            createdObjects.Add(mesh);
            createdObjects.Add(anotherMesh);

            // Act
            bool firstMark = wearableAsset.TryMarkTangentsRecalculated(mesh);
            bool repeatedMark = wearableAsset.TryMarkTangentsRecalculated(mesh);
            bool anotherMeshMark = wearableAsset.TryMarkTangentsRecalculated(anotherMesh);

            // Assert
            Assert.That(firstMark, Is.True);
            Assert.That(repeatedMark, Is.False);
            Assert.That(anotherMeshMark, Is.True);
        }

        [Test]
        public void ForgetMarkedTangentsWhenDisposed()
        {
            // Arrange
            var wearableAsset = new AttachmentRegularAsset(new GameObject(), new List<AttachmentRegularAsset.RendererInfo>(5), IStreamableRefCountData.Null.INSTANCE);
            var mesh = new Mesh();
            createdObjects.Add(mesh);
            wearableAsset.TryMarkTangentsRecalculated(mesh);

            // Act
            wearableAsset.Dispose();

            // Assert
            Assert.That(wearableAsset.TryMarkTangentsRecalculated(mesh), Is.True);
        }
    }
}
