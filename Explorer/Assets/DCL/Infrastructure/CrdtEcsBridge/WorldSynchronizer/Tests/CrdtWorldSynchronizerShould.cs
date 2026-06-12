using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace CrdtEcsBridge.WorldSynchronizer.Tests
{
    public class CrdtWorldSynchronizerShould
    {
        private CRDTWorldSynchronizer crdtWorldSynchronizer;

        [SetUp]
        public void SetUp()
        {
            crdtWorldSynchronizer = new CRDTWorldSynchronizer(World.Create(), Substitute.For<ISDKComponentsRegistry>(), Substitute.For<ISceneEntityFactory>(), new Dictionary<CRDTEntity, Entity>());
        }

        [Test]
        public void ThrowIfSyncBufferIsAlreadyRented()
        {
            IWorldSyncCommandBuffer cb = crdtWorldSynchronizer.GetSyncCommandBuffer();
            Assert.Throws<TimeoutException>(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }

        [Test]
        public void ReleaseSyncBuffer()
        {
            IWorldSyncCommandBuffer worldSyncCommandBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();
            worldSyncCommandBuffer.FinalizeAndDeserialize();
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncCommandBuffer);

            Assert.DoesNotThrow(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }

        [Test]
        public void ReuseSyncBufferInstance()
        {
            //Arrange
            IWorldSyncCommandBuffer first = crdtWorldSynchronizer.GetSyncCommandBuffer();
            first.FinalizeAndDeserialize();
            crdtWorldSynchronizer.ApplySyncCommandBuffer(first);

            //Act
            IWorldSyncCommandBuffer second = crdtWorldSynchronizer.GetSyncCommandBuffer();

            //Assert
            Assert.AreSame(first, second);
        }

        [Test]
        public void AllowRentAfterReleaseWithoutApplying()
        {
            //Arrange
            IWorldSyncCommandBuffer worldSyncCommandBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();
            worldSyncCommandBuffer.FinalizeAndDeserialize();

            //Act
            crdtWorldSynchronizer.ReleaseSyncCommandBuffer(worldSyncCommandBuffer);

            //Assert
            Assert.DoesNotThrow(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }
    }
}
