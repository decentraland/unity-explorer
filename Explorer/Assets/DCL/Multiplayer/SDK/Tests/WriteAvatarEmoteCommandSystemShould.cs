using Arch.Core;
using CommunicationData.URLHelpers;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using ECS.LifeCycle.Components;
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
        private readonly URN emoteUrn1 = new ("thunder-kiss-65");
        private readonly URN emoteUrn2 = new ("more-human-than-human");
        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private PlayerCRDTEntity playerCRDTEntity;
        private AvatarEmoteCommandComponent emoteCommand;
        private ISceneStateProvider sceneStateProvider;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            system = new WriteAvatarEmoteCommandSystem(world, ecsToCRDTWriter, sceneStateProvider);

            playerCRDTEntity = new PlayerCRDTEntity
            {
                IsDirty = true,
                CRDTEntity = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
            };

            entity = world.Create(playerCRDTEntity);

            emoteCommand = new AvatarEmoteCommandComponent
            {
                IsDirty = true,
                PlayingEmote = emoteUrn2,
                LoopingEmote = false,
            };
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void DispatchEmoteCommandUpdateCorrectly()
        {
            world.Add(entity, emoteCommand);

            var tickNumber = 563;
            sceneStateProvider.TickNumber.Returns((uint)tickNumber);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent, uint)>>(),
                                playerCRDTEntity.CRDTEntity,
                                tickNumber,
                                (emoteCommand, (uint)tickNumber));

            ecsToCRDTWriter.ClearReceivedCalls();

            emoteCommand.PlayingEmote = emoteUrn1;
            emoteCommand.LoopingEmote = true;
            emoteCommand.IsDirty = true;
            world.Set(entity, emoteCommand);

            tickNumber = 666;
            sceneStateProvider.TickNumber.Returns((uint)tickNumber);

            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .AppendMessage(
                                Arg.Any<Action<PBAvatarEmoteCommand, (AvatarEmoteCommandComponent, uint)>>(),
                                playerCRDTEntity.CRDTEntity,
                                tickNumber,
                                (emoteCommand, (uint)tickNumber));
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            world.Add(entity, emoteCommand);

            var tickNumber = 563;
            sceneStateProvider.TickNumber.Returns((uint)tickNumber);

            system.Update(0);

            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            Assert.IsFalse(world.Has<AvatarEmoteCommandComponent>(entity));
            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEmoteCommand>(playerCRDTEntity.CRDTEntity);
        }
    }
}
