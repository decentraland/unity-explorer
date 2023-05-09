using Arch.Core;
using CrdtEcsBridge.Components;
using NSubstitute;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.WorldSynchronizer.Tests
{
    public class CrdtWorldSynchronizerShould
    {
        private CRDTWorldSynchronizer crdtWorldSynchronizer;

        [SetUp]
        public void SetUp()
        {
            crdtWorldSynchronizer = new CRDTWorldSynchronizer(World.Create(), Substitute.For<ISDKComponentsRegistry>(), Substitute.For<IEntityFactory>());
        }

        [Test]
        public void ThrowIfSyncBufferIsAlreadyRented()
        {
            var cb = crdtWorldSynchronizer.GetSyncCommandBuffer();
            Assert.Throws<TimeoutException>(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }

        [Test]
        public void ReleaseSyncBuffer()
        {
            var worldSyncCommandBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();
            worldSyncCommandBuffer.FinalizeAndDeserialize();
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncCommandBuffer);

            Assert.DoesNotThrow(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }
    }
}
