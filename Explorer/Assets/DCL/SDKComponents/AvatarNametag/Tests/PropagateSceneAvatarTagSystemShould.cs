using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Nametags;
using DCL.Profiles;
using DCL.SDKComponents.AvatarNametag.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.AvatarNametag.Tests
{
    public class PropagateSceneAvatarTagSystemShould : UnitySystemTestBase<PropagateSceneAvatarTagSystem>
    {
        private const string REMOTE_WALLET = "0xdeadbeef";

        // An ordinary scene-owned entity id, past the reserved range player entities live in.
        private const int SCENE_LOCAL_ENTITY = 512;

        // Assigned in SetUp before any test runs; null! keeps the fixture free of nullable-dereference noise.
        private World globalWorld = null!;
        private Entity globalPlayerEntity;
        private Entity globalRemoteEntity;
        private EntityParticipantTable entityParticipantTable = null!;
        private ISceneStateProvider sceneStateProvider = null!;

        [SetUp]
        public void Setup()
        {
            // Profile.NewRandomProfile validates the generated name against the feature registry.
            EcsTestsUtils.SetUpFeaturesRegistry();

            globalWorld = World.Create();
            globalPlayerEntity = globalWorld.Create();
            globalRemoteEntity = globalWorld.Create();

            entityParticipantTable = new EntityParticipantTable();
            entityParticipantTable.Register(REMOTE_WALLET, globalRemoteEntity, RoomSource.Island);

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            system = new PropagateSceneAvatarTagSystem(world, sceneStateProvider, entityParticipantTable, globalWorld, globalPlayerEntity);
        }

        [Test]
        public void AddPlateToLocalPlayerFromPlayerEntity()
        {
            // Arrange
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.True);
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.Text, Is.EqualTo("Club Owner"));
            Assert.That(plate.IsRemoving, Is.False);
            Assert.That(plate.IsDirty, Is.True);
        }

        [Test]
        public void FallBackToNativeColorsWhenTheSceneSendsNone()
        {
            // Arrange
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, new PBAvatarNametag { Label = "Janitor", IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.TextColor, Is.EqualTo(SceneAvatarTagComponent.NATIVE_TEXT_COLOR));
            Assert.That(plate.BackgroundColor, Is.EqualTo(SceneAvatarTagComponent.NATIVE_BACKGROUND_COLOR));
            Assert.That(plate.BorderColor, Is.EqualTo(SceneAvatarTagComponent.NATIVE_BACKGROUND_COLOR));
        }

        [Test]
        public void FallBackToTheBackgroundColorWhenTheSceneSendsNoBorder()
        {
            // Arrange
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, new PBAvatarNametag
            {
                Label = "Blue",
                BackgroundColor = new Decentraland.Common.Color3 { R = 0.47f, G = 0.56f, B = 0.96f },
                IsDirty = true,
            });

            // Act
            system.Update(0);

            // Assert
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.BorderColor, Is.EqualTo(plate.BackgroundColor));
        }

        [Test]
        public void UseTheColorsTheSceneSends()
        {
            // Arrange
            var pbNametag = new PBAvatarNametag
            {
                Label = "Gold",
                LabelColor = new Decentraland.Common.Color3 { R = 1f, G = 0.5f, B = 0f },
                BackgroundColor = new Decentraland.Common.Color3 { R = 0f, G = 0f, B = 0.25f },
                BorderColor = new Decentraland.Common.Color3 { R = 1f, G = 0.9f, B = 0.4f },
                IsDirty = true,
            };

            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, pbNametag);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.TextColor, Is.EqualTo(new Color(1f, 0.5f, 0f)));
            Assert.That(plate.BackgroundColor, Is.EqualTo(new Color(0f, 0f, 0.25f)));
            Assert.That(plate.BorderColor, Is.EqualTo(new Color(1f, 0.9f, 0.4f)));
        }

        [Test]
        public void ResolveRemotePlayerThroughTheParticipantTable()
        {
            // Arrange
            Entity sceneEntity = CreateSceneEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
                new PBAvatarNametag { Label = "Bronze", IsDirty = true });

            world.Add(sceneEntity, NewSdkProfile(REMOTE_WALLET));

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalRemoteEntity), Is.True);
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).Text, Is.EqualTo("Bronze"));
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
        }

        [Test]
        public void ResolveARemotePlayerWhoseProfileLivesOnTheBridgeEntity()
        {
            // Arrange — in a real scene world the CRDT bridge materializes the scene's write on its own
            // entity, while the multiplayer bridge keeps SDKProfile on a separate entity that carries
            // no CRDTEntity; the two representations of one player share nothing but the CRDT id.
            CreateSceneEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
                new PBAvatarNametag { Label = "Bronze", IsDirty = true });

            world.Create(
                new PlayerSceneCRDTEntity(new CRDTEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM)),
                NewSdkProfile(REMOTE_WALLET));

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalRemoteEntity), Is.True);
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).Text, Is.EqualTo("Bronze"));
        }

        [Test]
        public void ResolveASceneAvatarThroughItsGlobalTwin()
        {
            // Arrange
            Entity globalNpcEntity = globalWorld.Create();
            Entity sceneEntity = CreateSceneEntity(SCENE_LOCAL_ENTITY, new PBAvatarNametag { Label = "Boss", IsDirty = true });
            world.Add(sceneEntity, new SDKAvatarShapeComponent(globalNpcEntity));

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalNpcEntity), Is.True);
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalNpcEntity).Text, Is.EqualTo("Boss"));
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
        }

        [Test]
        public void RetryUntilTheSceneAvatarGetsItsGlobalTwin()
        {
            // Arrange — the nametag arrives before AvatarShapeHandlerSystem has instantiated the avatar.
            var pbNametag = new PBAvatarNametag { Label = "Boss", IsDirty = true };
            Entity sceneEntity = CreateSceneEntity(SCENE_LOCAL_ENTITY, pbNametag);
            system.Update(0);

            Assert.That(pbNametag.IsDirty, Is.True, "an unresolved write must stay dirty to be retried");

            // Act — the avatar appears; the pending write lands on the next update.
            Entity globalNpcEntity = globalWorld.Create();
            world.Add(sceneEntity, new SDKAvatarShapeComponent(globalNpcEntity));
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalNpcEntity), Is.True);
            Assert.That(pbNametag.IsDirty, Is.False);
        }

        [Test]
        public void IgnoreAnEntityThatResolvesToNoAvatar()
        {
            // Arrange
            CreateSceneEntity(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM,
                new PBAvatarNametag { Label = "Nobody", IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalRemoteEntity), Is.False);
        }

        [Test]
        public void KeepThePlateOnAnEmptyLabel()
        {
            // Arrange
            var pbNametag = new PBAvatarNametag
            {
                Label = "Club Owner",
                BackgroundColor = new Decentraland.Common.Color3 { R = 1f, G = 0f, B = 0f },
                IsDirty = true,
            };

            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, pbNametag);
            system.Update(0);

            // Act
            pbNametag.Label = string.Empty;
            pbNametag.IsDirty = true;
            system.Update(0);

            // Assert - a label-less plate is the color-coding case, so it stays and keeps its colors.
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.IsRemoving, Is.False);
            Assert.That(plate.Text, Is.Empty);
            Assert.That(plate.BackgroundColor, Is.EqualTo(new Color(1f, 0f, 0f)));
        }

        [Test]
        public void FlagThePlateRemovingWhenTheComponentIsRemoved()
        {
            // Arrange
            Entity sceneEntity = CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY,
                new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

            system.Update(0);

            // Act
            world.Remove<PBAvatarNametag>(sceneEntity);
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).IsRemoving, Is.True);
        }

        [Test]
        public void FlagThePlateRemovingWhenTheSceneEntityIsDestroyed()
        {
            // Arrange
            Entity sceneEntity = CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY,
                new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

            system.Update(0);

            // Act
            world.Add(sceneEntity, new DeleteEntityIntention());
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).IsRemoving, Is.True);
        }

        [Test]
        public void DropThePlateWhenTheSceneStopsBeingCurrentAndReapplyOnReturn()
        {
            // Arrange
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, new PBAvatarNametag { Label = "Club Owner", IsDirty = true });
            system.Update(0);

            // Act — leaving the scene drops the plate.
            system.OnSceneIsCurrentChanged(false);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).IsRemoving, Is.True);

            // Act — coming back replays the write. Emulate the placement system having consumed the removal.
            globalWorld.Remove<SceneAvatarTagComponent>(globalPlayerEntity);
            system.OnSceneIsCurrentChanged(true);
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.True);
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).IsRemoving, Is.False);
        }

        [Test]
        public void DropThePlateOnWorldFinalization()
        {
            // Arrange
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, new PBAvatarNametag { Label = "Club Owner", IsDirty = true });
            system.Update(0);

            // Act
            system.FinalizeComponents(world.Query(new QueryDescription().WithAll<CRDTEntity>()));

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).IsRemoving, Is.True);
        }

        [Test]
        public void DoNothingWhileTheSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        private Entity CreateSceneEntity(int crdtId, PBAvatarNametag pbNametag) =>
            world.Create(new CRDTEntity(crdtId), pbNametag);

        private static SDKProfile NewSdkProfile(string userId)
        {
            var sdkProfile = new SDKProfile();
            sdkProfile.OverrideWith(Profile.NewRandomProfile(userId));
            return sdkProfile;
        }
    }
}
