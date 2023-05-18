using Arch.Core;
using CrdtEcsBridge.Components;
using Instrumentation;
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
        public void TolerateWorldSyncBufferAllocation()
        {
            // Warm up
            // If we don't warm up static constructor will be called and screw up the test
            IWorldSyncCommandBuffer worldSyncCommandBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();
            worldSyncCommandBuffer.FinalizeAndDeserialize();
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncCommandBuffer);

            // Tolerate 128 bytes allocation
            MemoryStat.Debug.GC_ALLOCATED_IN_FRAME.Check(
                () => crdtWorldSynchronizer.GetSyncCommandBuffer(),
                value => Assert.Less(value, 128));
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
