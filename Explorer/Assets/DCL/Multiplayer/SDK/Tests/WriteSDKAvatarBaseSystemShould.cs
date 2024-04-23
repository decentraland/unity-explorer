using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using ECS.Unity.ColorComponent;
using NSubstitute;
using NUnit.Framework;
using System;
using UnityEngine;
using WriteSDKAvatarBaseSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteSDKAvatarBaseSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteSDKAvatarBaseSystemShould : UnitySystemTestBase<WriteSDKAvatarBaseSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerProfileDataComponent playerProfileData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBAvatarBase> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarBase>>();
            var instantiatedPbComponent = new PBAvatarBase();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WriteSDKAvatarBaseSystem(world, ecsToCRDTWriter, componentPoolRegistry);

            playerProfileData = new PlayerProfileDataComponent
            {
                CRDTEntity = 3,
                Name = "CthulhuFhtaghn",
                BodyShapeURN = "old:ones:01",
                SkinColor = Color.green,
                EyesColor = Color.red,
                HairColor = Color.gray,
            };

            entity = world.Create(playerProfileData);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void PropagateComponentCreationCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarBase, PBAvatarBase>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
                                Arg.Is<PBAvatarBase>(comp =>
                                    comp.Name == playerProfileData.Name
                                    && comp.BodyShapeUrn == playerProfileData.BodyShapeURN
                                    && comp.EyesColor.ToUnityColor() == playerProfileData.EyesColor
                                    && comp.HairColor.ToUnityColor() == playerProfileData.HairColor
                                    && comp.SkinColor.ToUnityColor() == playerProfileData.SkinColor
                                ));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void PropagateComponentUpdateCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarBase, PBAvatarBase>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
                                Arg.Is<PBAvatarBase>(comp =>
                                    comp.Name == playerProfileData.Name
                                    && comp.BodyShapeUrn == playerProfileData.BodyShapeURN
                                    && comp.EyesColor.ToUnityColor() == playerProfileData.EyesColor
                                    && comp.HairColor.ToUnityColor() == playerProfileData.HairColor
                                    && comp.SkinColor.ToUnityColor() == playerProfileData.SkinColor
                                ));

            AssertPBComponentMatchesPlayerSDKData();

            Assert.IsTrue(world.TryGet(entity, out playerProfileData));

            playerProfileData.IsDirty = true;
            playerProfileData.Name = "D460N";
            playerProfileData.BodyShapeURN = "old:ones:02";
            playerProfileData.SkinColor = Color.gray;
            playerProfileData.EyesColor = Color.blue;
            playerProfileData.HairColor = Color.white;

            world.Set(entity, playerProfileData);

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerProfileData));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PBAvatarBase>(entity));

            world.Remove<PlayerProfileDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarBase>(playerProfileData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBAvatarBase>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
        }

        private void AssertPBComponentMatchesPlayerSDKData()
        {
            Assert.IsTrue(world.TryGet(entity, out PBAvatarBase pbAvatarBase));
            Assert.AreEqual(playerProfileData.Name, pbAvatarBase.Name);
            Assert.IsTrue(playerProfileData.BodyShapeURN.Equals(pbAvatarBase.BodyShapeUrn));
            Assert.AreEqual(playerProfileData.HairColor.ToColor3(), pbAvatarBase.HairColor);
            Assert.AreEqual(playerProfileData.EyesColor.ToColor3(), pbAvatarBase.EyesColor);
            Assert.AreEqual(playerProfileData.SkinColor.ToColor3(), pbAvatarBase.SkinColor);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }
    }
}
