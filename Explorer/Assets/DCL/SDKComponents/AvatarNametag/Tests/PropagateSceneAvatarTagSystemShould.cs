using Arch.Core;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.ECSComponents;
using DCL.Multiplayer.SDK.Components;
using DCL.Nametags;
using DCL.SDKComponents.AvatarNametag.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using ECS.Unity.AvatarShape.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.AvatarNametag.Tests
{
    public class PropagateSceneAvatarTagSystemShould : UnitySystemTestBase<PropagateSceneAvatarTagSystem>
    {
        // An ordinary scene-owned entity id, past the reserved range player entities live in.
        private const int SCENE_LOCAL_ENTITY = 512;

        private static readonly QueryDescription PLAYERS_QUERY = new QueryDescription().WithAll<PlayerSceneCRDTEntity>();
        private static readonly CRDTEntity REMOTE_PLAYER_CRDT_ENTITY = CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 1);

        private World globalWorld = null!;
        private Entity globalPlayerEntity;
        private Entity globalRemoteEntity;
        private Dictionary<CRDTEntity, Entity> entitiesMap = null!;
        private ISceneStateProvider sceneStateProvider = null!;

        [SetUp]
        public void Setup()
        {
            globalWorld = World.Create();
            globalPlayerEntity = globalWorld.Create();
            globalRemoteEntity = globalWorld.Create();

            entitiesMap = new Dictionary<CRDTEntity, Entity>();

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            system = new PropagateSceneAvatarTagSystem(world, sceneStateProvider, entitiesMap, globalWorld);
        }

        [Test]
        public void AddPlateToLocalPlayerFromPlayerEntity()
        {
            // Arrange
            CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

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
            CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Janitor", IsDirty = true });

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
            CreateLocalPlayerNametag(new PBAvatarNametag
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

            CreateLocalPlayerNametag(pbNametag);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity);
            Assert.That(plate.TextColor, Is.EqualTo(new Color(1f, 0.5f, 0f)));
            Assert.That(plate.BackgroundColor, Is.EqualTo(new Color(0f, 0f, 0.25f)));
            Assert.That(plate.BorderColor, Is.EqualTo(new Color(1f, 0.9f, 0.4f)));
        }

        [Test]
        public void ApplyAnAlreadyPresentNametagWhenTheRemotePlayerArrives()
        {
            // Arrange
            var pbNametag = new PBAvatarNametag { Label = "Bronze", IsDirty = true };
            CreateRemoteNametag(pbNametag);
            system.Update(0);

            Assert.That(pbNametag.IsDirty, Is.False, "a nametag with no player inside the scene waits for the arrival, not for a retry");

            // Act
            CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).Text, Is.EqualTo("Bronze"));
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
        }

        [Test]
        public void ApplyANametagTheSceneWritesAfterTheRemotePlayerArrived()
        {
            // Arrange
            CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);
            EndFrame();

            // Act
            var pbNametag = new PBAvatarNametag { Label = "Bronze", IsDirty = true };
            CreateRemoteNametag(pbNametag);
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).Text, Is.EqualTo("Bronze"));
            Assert.That(pbNametag.IsDirty, Is.False);
        }

        [Test]
        public void ApplyARemotePlayerNametagThatSharesTheEntityWithAnAvatarShape()
        {
            // Arrange
            Entity sceneEntity = CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            world.Add(sceneEntity, new PBAvatarShape());
            CreateRemoteBridgeEntity(globalRemoteEntity);

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).Text, Is.EqualTo("Bronze"));
        }

        [Test]
        public void ApplyEveryPlayerNametagInOneUpdate()
        {
            // Arrange
            Entity secondGlobalEntity = globalWorld.Create();
            CRDTEntity secondCrdtEntity = CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM + 1, 1);

            CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            entitiesMap[secondCrdtEntity] = world.Create(secondCrdtEntity, new PBAvatarNametag { Label = "Silver", IsDirty = true });

            CreateRemoteBridgeEntity(globalRemoteEntity);
            world.Create(new PlayerSceneCRDTEntity(secondCrdtEntity, secondGlobalEntity));

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).Text, Is.EqualTo("Bronze"));
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(secondGlobalEntity).Text, Is.EqualTo("Silver"));
        }

        [Test]
        public void FlagThePlateRemovingButKeepTheNametagWhenTheRemotePlayerLeavesTheScene()
        {
            // Arrange
            Entity sceneEntity = CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            Entity bridgeEntity = CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);
            EndFrame();

            // Act
            world.Add(bridgeEntity, new DeleteEntityIntention());
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).IsRemoving, Is.True);
            Assert.That(world.Has<PBAvatarNametag>(sceneEntity), Is.True);
        }

        [Test]
        public void ReapplyThePlateWhenTheRemotePlayerReturnsToTheScene()
        {
            // Arrange
            CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            Entity bridgeEntity = CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);
            EndFrame();

            world.Add(bridgeEntity, new DeleteEntityIntention());
            system.Update(0);
            world.Destroy(bridgeEntity);
            EndFrame();

            // Emulate the placement system having consumed the removal.
            globalWorld.Remove<SceneAvatarTagComponent>(globalRemoteEntity);

            // Act
            CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);

            // Assert
            SceneAvatarTagComponent plate = globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity);
            Assert.That(plate.Text, Is.EqualTo("Bronze"));
            Assert.That(plate.IsRemoving, Is.False);
        }

        [Test]
        public void KeepTheReappliedPlateWhileTheLeftBridgeEntityAwaitsDeletion()
        {
            // Arrange
            CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            Entity leftBridgeEntity = CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);
            EndFrame();

            world.Add(leftBridgeEntity, new DeleteEntityIntention());
            system.Update(0);
            EndFrame();

            CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);
            EndFrame();

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).IsRemoving, Is.False);
        }

        [Test]
        public void MoveThePlateWhenTheIdLandsOnAnotherPlayer()
        {
            // Arrange
            Entity secondGlobalEntity = globalWorld.Create();

            CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            Entity bridgeEntity = CreateRemoteBridgeEntity(globalRemoteEntity);
            system.Update(0);
            EndFrame();

            world.Destroy(bridgeEntity);

            // Act
            CreateRemoteBridgeEntity(secondGlobalEntity);
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalRemoteEntity).IsRemoving, Is.True);
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(secondGlobalEntity).Text, Is.EqualTo("Bronze"));
        }

        [Test]
        public void SkipAPlayerWhoseAvatarIsGone()
        {
            // Arrange
            CreateRemoteNametag(new PBAvatarNametag { Label = "Bronze", IsDirty = true });
            CreateRemoteBridgeEntity(globalRemoteEntity);
            globalWorld.Destroy(globalRemoteEntity);

            // Act, Assert
            Assert.DoesNotThrow(() => system.Update(0));
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
            world.Add(sceneEntity, new PBAvatarShape());
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
            var pbNametag = new PBAvatarNametag { Label = "Nobody", IsDirty = true };
            CreateSceneEntity(SCENE_LOCAL_ENTITY, pbNametag);

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalRemoteEntity), Is.False);
            Assert.That(pbNametag.IsDirty, Is.False, "no avatar can ever back this entity, so the retry must end");
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

            CreateLocalPlayerNametag(pbNametag);
            system.Update(0);
            EndFrame();

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
        public void PassASpacesOnlyLabelThroughVerbatim()
        {
            // Arrange - a spaces-only label is how a scene widens a text-less plate, so no
            // trimming or normalization may happen on the way to the component.
            CreateLocalPlayerNametag(new PBAvatarNametag { Label = "    ", IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Get<SceneAvatarTagComponent>(globalPlayerEntity).Text,
                Is.EqualTo("    "));
        }

        [Test]
        public void FlagThePlateRemovingWhenTheComponentIsRemoved()
        {
            // Arrange
            Entity sceneEntity = CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

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
            Entity sceneEntity = CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

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
            CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Club Owner", IsDirty = true });
            system.Update(0);
            EndFrame();

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
            CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Club Owner", IsDirty = true });
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
            CreateLocalPlayerNametag(new PBAvatarNametag { Label = "Club Owner", IsDirty = true });

            // Act
            system.Update(0);

            // Assert
            Assert.That(globalWorld.Has<SceneAvatarTagComponent>(globalPlayerEntity), Is.False);
        }

        protected override void OnTearDown()
        {
            globalWorld.Dispose();
        }

        private Entity CreateSceneEntity(int crdtId, PBAvatarNametag pbNametag) =>
            world.Create(new CRDTEntity(crdtId), pbNametag);

        /// <summary>
        ///     The local player has a single scene entity: the CRDT bridge maps its id to the persistent player entity,
        ///     which also carries <see cref="PlayerSceneCRDTEntity" />.
        /// </summary>
        private Entity CreateLocalPlayerNametag(PBAvatarNametag pbNametag)
        {
            Entity entity = world.Create(new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY), pbNametag,
                new PlayerSceneCRDTEntity(SpecialEntitiesID.PLAYER_ENTITY, globalPlayerEntity));

            entitiesMap[SpecialEntitiesID.PLAYER_ENTITY] = entity;
            return entity;
        }

        /// <summary>
        ///     The scene's entity for a remote player's CRDT id, as the CRDT bridge creates and maps it.
        /// </summary>
        private Entity CreateRemoteNametag(PBAvatarNametag pbNametag)
        {
            Entity entity = world.Create(REMOTE_PLAYER_CRDT_ENTITY, pbNametag);
            entitiesMap[REMOTE_PLAYER_CRDT_ENTITY] = entity;
            return entity;
        }

        /// <summary>
        ///     The multiplayer bridge's separate entity for a remote player inside the scene, not mapped by the CRDT bridge.
        /// </summary>
        private Entity CreateRemoteBridgeEntity(Entity globalEntity) =>
            world.Create(new PlayerSceneCRDTEntity(REMOTE_PLAYER_CRDT_ENTITY, globalEntity));

        /// <summary>
        ///     Resets the player dirty flags the way the end-of-frame clean-up system does.
        /// </summary>
        private void EndFrame() =>
            world.Query(in PLAYERS_QUERY, (ref PlayerSceneCRDTEntity player) => player.IsDirty = false);
    }
}
