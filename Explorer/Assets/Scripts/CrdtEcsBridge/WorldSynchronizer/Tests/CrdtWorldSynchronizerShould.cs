using Arch.Core;
using CrdtEcsBridge.Components;
using NSubstitute;
using NUnit.Framework;
using System;

namespace CrdtEcsBridge.WorldSynchronizer.Tests
{
    public class CrdtWorldSynchronizerShould
    {
        private CrdtWorldSynchronizer crdtWorldSynchronizer;

        [SetUp]
        public void SetUp()
        {
            crdtWorldSynchronizer = new CrdtWorldSynchronizer(World.Create(), Substitute.For<ISDKComponentsRegistry>());
        }

        [Test]
        public void ThrowIfSyncBufferIsAlreadyRented()
        {
            var cb = crdtWorldSynchronizer.GetSyncCommandBuffer();
            Assert.Throws<InvalidOperationException>(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }

        [Test]
        public void ReleaseSyncBuffer()
        {
            var worldSyncCommandBuffer = crdtWorldSynchronizer.GetSyncCommandBuffer();
            crdtWorldSynchronizer.ApplySyncCommandBuffer(worldSyncCommandBuffer);

            Assert.DoesNotThrow(() => crdtWorldSynchronizer.GetSyncCommandBuffer());
        }
    }
}
