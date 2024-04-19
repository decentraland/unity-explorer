using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Optimization.Pools;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.ColorComponent;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteSDKAvatarBaseSystemShould : UnitySystemTestBase<WriteSDKAvatarBaseSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerSDKDataComponent playerSDKData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBAvatarBase> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarBase>>();
            var instantiatedPbComponent = new PBAvatarBase();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WriteSDKAvatarBaseSystem(world, ecsToCRDTWriter, componentPoolRegistry);

            playerSDKData = new PlayerSDKDataComponent
            {
                CRDTEntity = 3,
                Name = "CthulhuFhtaghn",
                BodyShapeURN = "old:ones:01",
                SkinColor = Color.green,
                EyesColor = Color.red,
                HairColor = Color.gray,
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
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarBase, PlayerSDKDataComponent>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Is<PlayerSDKDataComponent>(comp =>
                                    comp.Name == playerSDKData.Name
                                    && comp.BodyShapeURN == playerSDKData.BodyShapeURN
                                    && comp.EyesColor == playerSDKData.EyesColor
                                    && comp.HairColor == playerSDKData.HairColor
                                    && comp.SkinColor == playerSDKData.SkinColor));

            Assert.IsTrue(world.TryGet(entity, out PBAvatarBase pbAvatarBase));
            Assert.AreEqual(pbAvatarBase.Name, playerSDKData.Name);
            Assert.IsTrue(playerSDKData.BodyShapeURN.Equals(pbAvatarBase.BodyShapeUrn));
            Assert.AreEqual(pbAvatarBase.HairColor, playerSDKData.HairColor.ToColor3());
            Assert.AreEqual(pbAvatarBase.EyesColor, playerSDKData.EyesColor.ToColor3());
            Assert.AreEqual(pbAvatarBase.SkinColor, playerSDKData.SkinColor.ToColor3());
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }

        // TODO: Component update

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PBAvatarBase>(entity));

            world.Remove<PlayerSDKDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarBase>(playerSDKData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
            Assert.IsTrue(world.Has<DeleteEntityIntention>(entity));
        }
    }
}
