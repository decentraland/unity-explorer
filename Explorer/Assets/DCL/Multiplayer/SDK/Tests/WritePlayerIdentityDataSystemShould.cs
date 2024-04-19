using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WritePlayerIdentityDataSystemShould : UnitySystemTestBase<WritePlayerIdentityDataSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerSDKDataComponent playerSDKData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBPlayerIdentityData> componentPoolRegistry = Substitute.For<IComponentPool<PBPlayerIdentityData>>();
            var instantiatedPbComponent = new PBPlayerIdentityData();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WritePlayerIdentityDataSystem(world, ecsToCRDTWriter, componentPoolRegistry);

            playerSDKData = new PlayerSDKDataComponent
            {
                Address = "Y065SoThoT",
                IsGuest = false,
                CRDTEntity = 3,
            };

            entity = world.Create(playerSDKData);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void PropagatePlayerIdentityDataCorrectly()
        {
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBPlayerIdentityData, PlayerSDKDataComponent>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Is<PlayerSDKDataComponent>(comp =>
                                    comp.Address == playerSDKData.Address
                                    && comp.IsGuest == playerSDKData.IsGuest));

            Assert.IsTrue(world.TryGet(entity, out PBPlayerIdentityData pbPlayerIdentityData));
            Assert.AreEqual(pbPlayerIdentityData.Address, playerSDKData.Address);
            Assert.AreEqual(pbPlayerIdentityData.IsGuest, playerSDKData.IsGuest);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PBPlayerIdentityData>(entity));

            world.Remove<PlayerSDKDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBPlayerIdentityData>(playerSDKData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
            Assert.IsTrue(world.Has<DeleteEntityIntention>(entity));
        }
    }
}
