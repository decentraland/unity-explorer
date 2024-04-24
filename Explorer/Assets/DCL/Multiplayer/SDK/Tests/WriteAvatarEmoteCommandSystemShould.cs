using Arch.Core;
using CRDT;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
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
        private PlayerCRDTEntity playerCRDTEntity;
        private AvatarEmoteCommandComponent emoteCommand;
        private ISceneStateProvider sceneStateProvider;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            IComponentPool<PBAvatarEmoteCommand> componentPoolRegistry = Substitute.For<IComponentPool<PBAvatarEmoteCommand>>();
            var instantiatedPbComponent = new PBAvatarEmoteCommand();
            componentPoolRegistry.Get().Returns(instantiatedPbComponent);
            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            system = new WriteAvatarEmoteCommandSystem(world, ecsToCRDTWriter, sceneStateProvider);

            playerCRDTEntity = new PlayerCRDTEntity
            {
                IsDirty = true,
                CRDTEntity = new CRDTEntity(666),
            };

            entity = world.Create(playerCRDTEntity);

            emoteCommand = new AvatarEmoteCommandComponent
            {
                IsDirty = true,
                PlayingEmote = "thunder-kiss-65",
                LoopingEmote = true,
            };
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

            world.Add(entity, emoteCommand);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerCRDTEntity.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<(AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(data =>
                                    data.emoteCommand.PlayingEmote == emoteCommand.PlayingEmote
                                    && data.emoteCommand.LoopingEmote == emoteCommand.LoopingEmote));

            AssertPBComponentMatchesPlayerSDKData();
        }

        // TODO: Add timestamp test

        [Test]
        public void PropagateComponentUpdateCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, emoteCommand);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerCRDTEntity.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<(AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(data =>
                                    data.emoteCommand.PlayingEmote == emoteCommand.PlayingEmote
                                    && data.emoteCommand.LoopingEmote == emoteCommand.LoopingEmote));

            AssertPBComponentMatchesPlayerSDKData();

            Assert.IsTrue(world.TryGet(entity, out emoteCommand));

            emoteCommand.IsDirty = true;
            emoteCommand.PlayingEmote = "thunder-kiss-66";
            emoteCommand.LoopingEmote = false;

            world.Set(entity, emoteCommand);

            system.Update(0);

            Assert.IsTrue(world.TryGet(entity, out emoteCommand));

            AssertPBComponentMatchesPlayerSDKData();
        }

        [Test]
        public void AvoidPropagationIfEmoteDoesntChange()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));

            world.Add(entity, emoteCommand);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>>(),
                                Arg.Is<CRDTEntity>(crdtEntity => crdtEntity.Id == playerCRDTEntity.CRDTEntity.Id),
                                Arg.Any<int>(),
                                Arg.Is<(AvatarEmoteCommandComponent emoteCommand, uint timestamp)>(data =>
                                    data.emoteCommand.PlayingEmote == emoteCommand.PlayingEmote
                                    && data.emoteCommand.LoopingEmote == emoteCommand.LoopingEmote));

            ecsToCRDTWriter.ClearReceivedCalls();

            AssertPBComponentMatchesPlayerSDKData();

            // Flag as dirty without actually updating the emotes or anything
            emoteCommand.IsDirty = true;
            world.Set(entity, emoteCommand);

            system.Update(0);

            ecsToCRDTWriter.DidNotReceive()
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent emoteCommand, uint timestamp)>>(),
                                Arg.Any<CRDTEntity>(),
                                Arg.Any<int>(),
                                Arg.Any<(AvatarEmoteCommandComponent emoteCommand, uint timestamp)>());
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));

            world.Add(entity, emoteCommand);

            system.Update(0);

            Assert.IsTrue(world.Has<PBAvatarEmoteCommand>(entity));

            world.Remove<PlayerCRDTEntity>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEmoteCommand>(playerCRDTEntity.CRDTEntity.Id);
            Assert.IsFalse(world.Has<PBAvatarEmoteCommand>(entity));
            Assert.IsFalse(world.Has<CRDTEntity>(entity));
        }

        private void AssertPBComponentMatchesPlayerSDKData()
        {
            Assert.IsTrue(world.TryGet(entity, out PBAvatarEmoteCommand pbAvatarEmoteCommand));
            Assert.IsTrue(emoteCommand.PlayingEmote.Equals(pbAvatarEmoteCommand.EmoteUrn));
            Assert.AreEqual(emoteCommand.LoopingEmote, pbAvatarEmoteCommand.Loop);
            Assert.IsTrue(world.Has<CRDTEntity>(entity));
        }
    }
}
