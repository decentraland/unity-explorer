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
    }
}
