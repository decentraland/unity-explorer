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
        private PlayerIdentityDataComponent playerIdentityData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBPlayerIdentityData> componentPoolRegistry = Substitute.For<IComponentPool<PBPlayerIdentityData>>();
            var instantiatedPbComponent = new PBPlayerIdentityData();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WritePlayerIdentityDataSystem(world, ecsToCRDTWriter, componentPoolRegistry);

            playerIdentityData = new PlayerIdentityDataComponent
            {
                Address = "Y065SoThoT",
                IsGuest = false,
                CRDTEntity = 3,
            };

            entity = world.Create(playerIdentityData);
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
                                Arg.Any<Action<PBPlayerIdentityData, PlayerIdentityDataComponent>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerIdentityData.CRDTEntity.Id),
                                Arg.Is<PlayerIdentityDataComponent>(comp =>
                                    comp.Address == playerIdentityData.Address
                                    && comp.IsGuest == playerIdentityData.IsGuest));

            Assert.IsTrue(world.TryGet(entity, out PBPlayerIdentityData pbPlayerIdentityData));
            Assert.AreEqual(pbPlayerIdentityData.Address, playerIdentityData.Address);
            Assert.AreEqual(pbPlayerIdentityData.IsGuest, playerIdentityData.IsGuest);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PBPlayerIdentityData>(entity));

            world.Remove<PlayerIdentityDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBPlayerIdentityData>(playerIdentityData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
            Assert.IsTrue(world.Has<DeleteEntityIntention>(entity));
        }
    }
}
