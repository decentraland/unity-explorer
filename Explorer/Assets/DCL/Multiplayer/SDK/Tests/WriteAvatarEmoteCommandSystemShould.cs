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

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteAvatarEmoteCommandSystemShould : UnitySystemTestBase<WriteAvatarEmoteCommandSystem>
    {
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerSDKDataComponent playerSDKData;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBAvatarEmoteCommand> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarEmoteCommand>>();
            var instantiatedPbComponent = new PBAvatarEmoteCommand();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            system = new WriteAvatarEmoteCommandSystem(world, ecsToCRDTWriter, componentPoolRegistry, Substitute.For<ISceneStateProvider>());

            playerSDKData = new PlayerSDKDataComponent
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

            world.Add(entity, playerSDKData);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<PBAvatarEmoteCommand>(comp =>
                                    comp.EmoteUrn == playerSDKData.PlayingEmote
                                    && comp.Loop == playerSDKData.LoopingEmote));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void PropagateComponentUpdateCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, playerSDKData);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<PBAvatarEmoteCommand>(comp =>
                                    comp.EmoteUrn == playerSDKData.PlayingEmote
                                    && comp.Loop == playerSDKData.LoopingEmote));

            AssertPBComponentMatchesPlayerSDKData();

            Assert.IsTrue(world.TryGet(entity, out playerSDKData));

            playerSDKData.IsDirty = true;
            playerSDKData.PlayingEmote = "thunder-kiss-66";
            playerSDKData.LoopingEmote = false;

            world.Set(entity, playerSDKData);

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out playerSDKData));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void AvoidPropagationIfEmoteDoesntChange()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, playerSDKData);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, PBAvatarEmoteCommand>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerSDKData.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<PBAvatarEmoteCommand>(comp =>
                                    comp.EmoteUrn == playerSDKData.PlayingEmote
                                    && comp.Loop == playerSDKData.LoopingEmote));

            ecsToCRDTWriter.ClearReceivedCalls();

            AssertPBComponentMatchesPlayerSDKData();

            // Flag as dirty without actually updating the emotes or anything
            playerSDKData.IsDirty = true;
            world.Set(entity, playerSDKData);

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

            world.Add(entity, playerSDKData);

            system.Update(0);

            Assert.IsTrue(world.Has<PBAvatarEmoteCommand>(entity));

            world.Remove<PlayerSDKDataComponent>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEmoteCommand>(playerSDKData.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
        }

        private void AssertPBComponentMatchesPlayerSDKData()
        {
            Assert.IsTrue(world.TryGet(entity, out PBAvatarEmoteCommand pbAvatarEmoteCommand));
            Assert.IsTrue(playerSDKData.PlayingEmote.Equals(pbAvatarEmoteCommand.EmoteUrn));
            Assert.AreEqual(playerSDKData.LoopingEmote, pbAvatarEmoteCommand.Loop);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }
    }
}
