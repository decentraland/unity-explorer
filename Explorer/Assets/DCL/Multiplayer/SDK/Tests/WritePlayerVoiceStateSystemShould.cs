using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Multiplayer.SDK.Systems.SceneWorld;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WritePlayerVoiceStateSystemShould : UnitySystemTestBase<WritePlayerVoiceStateSystem>
    {
        private IECSToCRDTWriter ecsToCRDTWriter = null!;
        private PlayerSceneCRDTEntity playerCRDTEntity;
        private Entity entity;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();
            system = new WritePlayerVoiceStateSystem(world, ecsToCRDTWriter);

            playerCRDTEntity = new PlayerSceneCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);
            entity = world.Create(playerCRDTEntity);
        }

        [Test]
        public void PutTheVoiceStateWhenDirty()
        {
            world.Add(entity, new PlayerVoiceStateComponent { IsSpeaking = true, IsDirty = true });

            system.Update(0);

            ecsToCRDTWriter.Received(1).PutMessage(Arg.Any<Action<PBPlayerVoiceState, bool>>(), playerCRDTEntity.CRDTEntity, true);
        }

        [Test]
        public void NotPutTheVoiceStateWhenClean()
        {
            world.Add(entity, new PlayerVoiceStateComponent { IsSpeaking = true, IsDirty = false });

            system.Update(0);

            ecsToCRDTWriter.DidNotReceive().PutMessage(Arg.Any<Action<PBPlayerVoiceState, bool>>(), Arg.Any<CRDTEntity>(), Arg.Any<bool>());
        }

        [Test]
        public void DeleteTheVoiceStateWhenThePlayerLeaves()
        {
            world.Add(entity, new PlayerVoiceStateComponent { IsSpeaking = true, IsDirty = false });
            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBPlayerVoiceState>(playerCRDTEntity.CRDTEntity);
        }
    }
}
