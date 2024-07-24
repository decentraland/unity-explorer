using Arch.Core;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using WriteSDKAvatarBaseSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteSDKAvatarBaseSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteSDKAvatarBaseSystemShould : UnitySystemTestBase<WriteSDKAvatarBaseSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private SDKProfile profile;
        private PlayerSceneCRDTEntity playerCRDTEntity;

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            system = new WriteSDKAvatarBaseSystem(world, ecsToCRDTWriter);

            profile = new SDKProfile();
            profile.OverrideWith(Profile.NewRandomProfile(FAKE_USER_ID));
            profile.IsDirty = true;

            playerCRDTEntity = new PlayerSceneCRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM);

            entity = world.Create(playerCRDTEntity);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void DispatchAvatarBaseUpdateCorrectly()
        {
            world.Add(entity, profile);
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarBase, SDKProfile>>(),
                                playerCRDTEntity.CRDTEntity,
                                profile);

            ecsToCRDTWriter.ClearReceivedCalls();
            profile.Name = "newName";
            profile.IsDirty = true;

            world.Set(entity, profile);
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarBase, SDKProfile>>(),
                                playerCRDTEntity.CRDTEntity,
                                profile);
        }

        [Test]
        public void HandleComponentRemovalCorrectly()
        {
            world.Add(entity, profile);
            system.Update(0);

            world.Add<DeleteEntityIntention>(entity);

            system.Update(0);

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarBase>(playerCRDTEntity.CRDTEntity);
        }
    }
}
