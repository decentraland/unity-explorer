using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Nametags;
using DCL.Profiles;
using DCL.SDKComponents.AvatarNametag.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.AvatarNametag.Tests
{
    public class PropagateSceneAvatarTagSystemShould : UnitySystemTestBase<PropagateSceneAvatarTagSystem>
    {
        private const string REMOTE_WALLET = "0xdeadbeef";

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
                IsDirty = true,
            };

            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, pbNametag);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.TextColor, Is.EqualTo(new Color(1f, 0.5f, 0f)));
            Assert.That(plate.BackgroundColor, Is.EqualTo(new Color(0f, 0f, 0.25f)));
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
        public void FlagThePlateRemovingOnAnEmptyLabel()
        {
            // Arrange
            var pbNametag = new PBAvatarNametag { Label = "Club Owner", IsDirty = true };
            CreateSceneEntity(SpecialEntitiesID.PLAYER_ENTITY, pbNametag);
            system.Update(0);

            // Act
            pbNametag.Label = string.Empty;
            pbNametag.IsDirty = true;
            system.Update(0);

            // Assert — the plate is flagged, never removed: NametagPlacementSystem hides it first.
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.True);
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).IsRemoving, Is.True);
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
