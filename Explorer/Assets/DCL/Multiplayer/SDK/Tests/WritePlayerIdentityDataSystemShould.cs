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
using WritePlayerIdentityDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WritePlayerIdentityDataSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WritePlayerIdentityDataSystemShould : UnitySystemTestBase<WritePlayerIdentityDataSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerProfileDataComponent playerProfileData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBPlayerIdentityData> componentPoolRegistry = Substitute.For<IComponentPool<PBPlayerIdentityData>>();
            var instantiatedPbComponent = new PBPlayerIdentityData();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WritePlayerIdentityDataSystem(world, ecsToCRDTWriter, componentPoolRegistry);

            playerProfileData = new PlayerProfileDataComponent
            {
                Address = "Y065SoThoT",
                IsGuest = false,
                CRDTEntity = 3,
            };

            entity = world.Create(playerProfileData);
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
                                Arg.Any<Action<PBPlayerIdentityData, PBPlayerIdentityData>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
                                Arg.Is<PBPlayerIdentityData>(comp =>
                                    comp.Address == playerProfileData.Address
                                    && comp.IsGuest == playerProfileData.IsGuest));

            Assert.IsTrue(world.TryGet(entity, out PBPlayerIdentityData pbPlayerIdentityData));
            Assert.AreEqual(pbPlayerIdentityData.Address, playerProfileData.Address);
            Assert.AreEqual(pbPlayerIdentityData.IsGuest, playerProfileData.IsGuest);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PBPlayerIdentityData>(entity));

            world.Remove<PlayerProfileDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBPlayerIdentityData>(playerProfileData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBPlayerIdentityData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
            Assert.IsTrue(world.Has<DeleteEntityIntention>(entity));
        }
    }
}
