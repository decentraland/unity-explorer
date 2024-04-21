using Arch.Core;
using CommunicationData.URLHelpers;
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
using System.Collections.Generic;
using System.Linq;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteAvatarEquippedDataSystemShould : UnitySystemTestBase<WriteAvatarEquippedDataSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerSDKDataComponent playerSDKData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBAvatarEquippedData> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarEquippedData>>();
            var instantiatedPbComponent = new PBAvatarEquippedData();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WriteAvatarEquippedDataSystem(world, ecsToCRDTWriter, componentPoolRegistry);

            var wearableURNs = new List<URN>();
            wearableURNs.Add("wearable-urn-1");
            wearableURNs.Add("wearable-urn-2");
            wearableURNs.Add("wearable-urn-3");
            var emoteURNs = new List<URN>();
            emoteURNs.Add("emote-urn-1");
            emoteURNs.Add("emote-urn-2");
            emoteURNs.Add("emote-urn-3");

            playerSDKData = new PlayerSDKDataComponent
            {
                CRDTEntity = 3,
                Name = "CthulhuFhtaghn",
                WearableUrns = wearableURNs,
                EmoteUrns = emoteURNs,
            };

            entity = world.Create(playerSDKData);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void PropagateComponentCreationCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarEquippedData, PlayerSDKDataComponent>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Is<PlayerSDKDataComponent>(comp =>
                                    comp.Name == playerSDKData.Name
                                    && comp.WearableUrns == playerSDKData.WearableUrns
                                    && comp.EmoteUrns == playerSDKData.EmoteUrns));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void PropagateComponentUpdateCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarEquippedData, PlayerSDKDataComponent>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Is<PlayerSDKDataComponent>(comp =>
                                    comp.Name == playerSDKData.Name
                                    && comp.WearableUrns == playerSDKData.WearableUrns
                                    && comp.EmoteUrns == playerSDKData.EmoteUrns));

            AssertPBComponentMatchesPlayerSDKData();

            Assert.IsTrue(world.TryGet(entity, out playerSDKData));

            playerSDKData.IsDirty = true;
            playerSDKData.Name = "D460N";
            playerSDKData.BodyShapeURN = "old:ones:02";
            var newWearableURNs = new List<URN>();
            newWearableURNs.Add("wearable-urn-4");
            newWearableURNs.Add("wearable-urn-5");
            newWearableURNs.Add("wearable-urn-6");
            playerSDKData.WearableUrns = newWearableURNs;
            var newEmoteURNs = new List<URN>();
            newEmoteURNs.Add("emote-urn-4");
            newEmoteURNs.Add("emote-urn-5");
            newEmoteURNs.Add("emote-urn-6");
            playerSDKData.EmoteUrns = newEmoteURNs;

            world.Set(entity, playerSDKData);

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerSDKData));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));

            system.Update(0);

            Assert.IsTrue(world.Has<PBAvatarEquippedData>(entity));

            world.Remove<PlayerSDKDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEquippedData>(playerSDKData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBAvatarEquippedData>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
            Assert.IsTrue(world.Has<DeleteEntityIntention>(entity));
        }

        private void AssertPBComponentMatchesPlayerSDKData()
        {
            Assert.IsTrue(world.TryGet(entity, out PBAvatarEquippedData pbComponent));

            Assert.AreEqual(playerSDKData.WearableUrns.Count, pbComponent.WearableUrns.Count);

            foreach (string urn in pbComponent.WearableUrns) { Assert.IsTrue(playerSDKData.WearableUrns.Contains(urn)); }

            Assert.AreEqual(playerSDKData.EmoteUrns.Count, pbComponent.EmoteUrns.Count);

            foreach (string urn in pbComponent.EmoteUrns) { Assert.IsTrue(playerSDKData.EmoteUrns.Contains(urn)); }

            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }
    }
}
