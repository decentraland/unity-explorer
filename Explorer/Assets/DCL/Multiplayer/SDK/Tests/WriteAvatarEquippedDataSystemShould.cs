using Arch.Core;
using CrdtEcsBridge.Components;
using CrdtEcsBridge.ECSToCRDTWriter;
using DCL.AvatarRendering.Wearables;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using WriteAvatarEquippedDataSystem = DCL.Multiplayer.SDK.Systems.SceneWorld.WriteAvatarEquippedDataSystem;

namespace DCL.Multiplayer.SDK.Tests
{
    public class WriteAvatarEquippedDataSystemShould : UnitySystemTestBase<WriteAvatarEquippedDataSystem>
    {
        private const string FAKE_USER_ID = "Ia4Ia5Cth0ulhu2Ftaghn2";

        private Entity entity;
        private IECSToCRDTWriter ecsToCRDTWriter;
        private Profile profile;
        private PlayerCRDTEntity playerCRDTEntity;

        private Avatar CreateTestAvatar() =>
            new (BodyShape.MALE,
                WearablesConstants.DefaultWearables.GetDefaultWearablesForBodyShape(BodyShape.MALE),
                WearablesConstants.DefaultColors.GetRandomEyesColor(),
                WearablesConstants.DefaultColors.GetRandomHairColor(),
                WearablesConstants.DefaultColors.GetRandomSkinColor());

        [SetUp]
        public void Setup()
        {
            ecsToCRDTWriter = Substitute.For<IECSToCRDTWriter>();

            system = new WriteAvatarEquippedDataSystem(world, ecsToCRDTWriter);

            profile = new Profile(FAKE_USER_ID, "fake user", CreateTestAvatar());
            profile.IsDirty = true;

            playerCRDTEntity = new PlayerCRDTEntity
            {
                IsDirty = true,
                CRDTEntity = SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
            };

            entity = world.Create(playerCRDTEntity);
        }

        [TearDown]
        public void TearDown()
        {
            world.Dispose();
        }

        [Test]
        public void DispatchAvatarEquippedDataUpdateCorrectly()
        {
            world.Add(entity, profile);
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarEquippedData, Profile>>(),
                                playerCRDTEntity.CRDTEntity,
                                profile);

            ecsToCRDTWriter.ClearReceivedCalls();
            profile.Name = "newName";
            profile.IsDirty = true;

            world.Set(entity, profile);
            system.Update(0);

            ecsToCRDTWriter.Received(1)
                           .PutMessage(
                                Arg.Any<Action<PBAvatarEquippedData, Profile>>(),
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

            ecsToCRDTWriter.Received(1).DeleteMessage<PBAvatarEquippedData>(playerCRDTEntity.CRDTEntity);
        }
    }
}
