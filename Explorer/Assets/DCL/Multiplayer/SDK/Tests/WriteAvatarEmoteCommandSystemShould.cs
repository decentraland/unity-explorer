using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems;
using DCL.Optimization.Pools;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System;
using WriteAvatarEmoteCommandSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEmoteCommandSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteAvatarEmoteCommandSystemShould : UnitySystemTestBase<WriteAvatarEmoteCommandSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerProfileDataComponent playerProfileData;
        private ISceneStateProvider sceneStateProvider;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBAvatarEmoteCommand> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarEmoteCommand>>();
            var instantiatedPbComponent = new PBAvatarEmoteCommand();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            system = new WriteAvatarEmoteCommandSystem(world, ecsToCRDTWriter, componentPoolRegistry, sceneStateProvider);

            playerProfileData = new PlayerProfileDataComponent
            {
                IsDirty = true,
                CRDTEntity = 3,
                Name = "CthulhuFhtaghn",
                PlayingEmote = "thunder-kiss-65",
                LoopingEmote = true,
            };

            // Not assigning component here so that the emote is not "triggered" on setup
            entity = world.Create();
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void PropagateComponentCreationCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, playerProfileData);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<PBAvatarEmoteCommand>(comp =>
                                    comp.EmoteUrn == playerProfileData.PlayingEmote
                                    && comp.Loop == playerProfileData.LoopingEmote));

            AssertPBComponentMatchesPlayerSDKData();
        }

        // TODO: Add timestamp test

        [Test]
        public void PropagateComponentUpdateCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, playerProfileData);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<PBAvatarEmoteCommand>(comp =>
                                    comp.EmoteUrn == playerProfileData.PlayingEmote
                                    && comp.Loop == playerProfileData.LoopingEmote));

            AssertPBComponentMatchesPlayerSDKData();

            Assert.IsTrue(world.TryGet(entity, out playerProfileData));

            playerProfileData.IsDirty = true;
            playerProfileData.PlayingEmote = "thunder-kiss-66";
            playerProfileData.LoopingEmote = false;

            world.Set(entity, playerProfileData);

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerProfileData));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void AvoidPropagationIfEmoteDoesntChange()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, playerProfileData);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerProfileData.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<PBAvatarEmoteCommand>(comp =>
                                    comp.EmoteUrn == playerProfileData.PlayingEmote
                                    && comp.Loop == playerProfileData.LoopingEmote));

            ecsToCRDTWriter.ClearReceivedCalls();

            AssertPBComponentMatchesPlayerSDKData();

            // Flag as dirty without actually updating the emotes or anything
            playerProfileData.IsDirty = true;
            world.Set(entity, playerProfileData);

            system.Update(0);

            ecsToCRDTWriter.DidNotReceive()
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<int>(),
                                Arg.Any<PBAvatarEmoteCommand>());
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));

            world.Add(entity, playerProfileData);

            system.Update(0);

            Assert.IsTrue(world.Has<PBAvatarEmoteCommand>(entity));

            world.Remove<PlayerProfileDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEmoteCommand>(playerProfileData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
        }

        private void AssertPBComponentMatchesPlayerSDKData()
        {
            Assert.IsTrue(world.TryGet(entity, out PBAvatarEmoteCommand pbAvatarEmoteCommand));
            Assert.IsTrue(playerProfileData.PlayingEmote.Equals(pbAvatarEmoteCommand.EmoteUrn));
            Assert.AreEqual(playerProfileData.LoopingEmote, pbAvatarEmoteCommand.Loop);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }
    }
}
