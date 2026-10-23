using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Multiplayer.Connections.Rooms;
using DCL.Multiplayer.Profiles.Tables;
using DCL.Multiplayer.SDK.Components;
using DCL.Profiles;
using DCL.SDKComponents.SceneWearables.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneWearables.Tests
{
    public class PropagateSceneWearablesToAvatarsSystemShould : UnitySystemTestBase<PropagateSceneWearablesToAvatarsSystem>
    {
        private const string SCENE_ID = "bafkreiscene";
        private const string OTHER_SCENE_ID = "bafkreiotherscene";
        private const string CATALYST_WEARABLE = "urn:decentraland:off-chain:base-avatars:eyes_00";
        private const string SCENE_WEARABLE = "urn:decentraland:off-chain:scene-wearable:" + SCENE_ID + "-hathash";
        private const string SCENE_WEARABLE_2 = "urn:decentraland:off-chain:scene-wearable:" + SCENE_ID + "-kilthash";
        private const string OTHER_SCENE_WEARABLE = "urn:decentraland:off-chain:scene-wearable:" + OTHER_SCENE_ID + "-kilthash";
        private const string REMOTE_WALLET = "0xdeadbeef";
        private const string SECOND_WALLET = "0xcafebabe";

        private static readonly QueryDescription FINALIZE_QUERY = new QueryDescription().WithAll<CRDTEntity>();
        private static readonly CRDTEntity REMOTE_PLAYER_CRDT_ENTITY = CRDTEntity.Create(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM, 1);

        private World globalWorld = null!;
        private World? otherSceneWorld;
        private Entity globalPlayerEntity;
        private Entity globalRemoteEntity;
        private Entity scenePlayerEntity;
        private EntityParticipantTable entityParticipantTable = null!;
        private ISceneStateProvider sceneStateProvider = null!;

        [SetUp]
        public void SetUp()
        {
            // Profile.NewRandomProfile validates the generated name against the feature registry.
            EcsTestsUtils.SetUpFeaturesRegistry();

            globalWorld = World.Create();
            globalPlayerEntity = globalWorld.Create(new SceneWearablesOverride(new List<URN>()));
            globalRemoteEntity = globalWorld.Create();

            entityParticipantTable = new EntityParticipantTable();
            entityParticipantTable.Register(REMOTE_WALLET, globalRemoteEntity, RoomSource.Island);

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            system = new PropagateSceneWearablesToAvatarsSystem(world, SceneDataSub(SCENE_ID), sceneStateProvider, entityParticipantTable, globalWorld, globalPlayerEntity);
        }

        protected override void OnTearDown()
        {
            otherSceneWorld?.Dispose();
            globalWorld.Dispose();
            EcsTestsUtils.TearDownFeaturesRegistry();
        }

        [Test]
        public void ApplyOnlyOwnSceneWearablesOfPlayerEntity()
        {
            // Arrange
            CreateScenePlayerAvatarShape(CATALYST_WEARABLE, OTHER_SCENE_WEARABLE, SCENE_WEARABLE, "assets/wearables/unresolved_hat.glb");

            // Act
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }

        [Test]
        public void IgnoreAvatarShapeOfOtherEntities()
        {
            // Arrange
            world.Create(new PBAvatarShape { Wearables = { SCENE_WEARABLE }, IsDirty = true }, new CRDTEntity(512));

            // Act
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            Assert.IsEmpty(sceneWearables.Urns);
            Assert.IsFalse(sceneWearables.IsDirty);
        }

        [TestCase(SpecialEntitiesID.CAMERA_ENTITY)]
        [TestCase(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_FROM - 1)]
        [TestCase(SpecialEntitiesID.OTHER_PLAYER_ENTITIES_TO)]
        public void IgnoreNonPlayerIdsEvenWithAProfileBehindThem(int crdtId)
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { SCENE_WEARABLE }, IsDirty = true };
            world.Create(pbAvatarShape, new CRDTEntity(crdtId), NewSdkProfile(REMOTE_WALLET));

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<SceneWearablesOverride>(globalRemoteEntity));
            Assert.IsEmpty(GetOverride().Urns);
        }

        [Test]
        public void NotApplyWhenSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);

            // Act
            system.Update(0);

            // Assert
            Assert.IsEmpty(GetOverride().Urns);
        }

        [Test]
        public void NotMarkDirtyWhenSceneWearablesAreUnchanged()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(CATALYST_WEARABLE, SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            pbAvatarShape.Wearables.Remove(CATALYST_WEARABLE);
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsFalse(sceneWearables.IsDirty);
        }

        [Test]
        public void ReplaceSceneWearablesWhenTheyChange()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            pbAvatarShape.Wearables.Add(SCENE_WEARABLE_2);
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE), new URN(SCENE_WEARABLE_2) }, sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }

        [Test]
        public void ClearOverrideWhenComponentIsRemoved()
        {
            // Arrange
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            world.Remove<PBAvatarShape>(scenePlayerEntity);

            // Act
            system.Update(0);

            // Assert
            AssertOverrideCleared();
        }

        [Test]
        public void ClearOverrideWhenEntityIsDeleted()
        {
            // Arrange
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            world.Add(scenePlayerEntity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            AssertOverrideCleared();
        }

        [Test]
        public void ClearOverrideWhenSceneStopsBeingCurrent()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();
            pbAvatarShape.IsDirty = false;

            // Act
            sceneStateProvider.IsCurrent.Returns(false);
            system.OnSceneIsCurrentChanged(false);

            // Assert
            AssertOverrideCleared();
        }

        [Test]
        public void ReapplyOverrideWhenSceneBecomesCurrentAgain()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            pbAvatarShape.IsDirty = false;

            sceneStateProvider.IsCurrent.Returns(false);
            system.OnSceneIsCurrentChanged(false);
            ClearOverrideDirtyFlag();

            // Act
            sceneStateProvider.IsCurrent.Returns(true);
            system.OnSceneIsCurrentChanged(true);
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }

        [Test]
        public void ApplyAvatarShapeWrittenWhileSceneWasNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            pbAvatarShape.IsDirty = false;

            // Act
            sceneStateProvider.IsCurrent.Returns(true);
            system.OnSceneIsCurrentChanged(true);
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride().Urns);
        }

        [Test]
        public void KeepOverrideOfAnotherSceneWhenNotApplied()
        {
            // Arrange
            globalWorld.Get<SceneWearablesOverride>(globalPlayerEntity).Urns.Add(OTHER_SCENE_WEARABLE);

            // Act
            system.OnSceneIsCurrentChanged(false);
            system.FinalizeComponents(world.Query(in FINALIZE_QUERY));

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(OTHER_SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsFalse(sceneWearables.IsDirty);
        }

        [Test]
        public void KeepOverrideOfNextSceneWhenPreviousSceneIsFinalized()
        {
            // Arrange
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);

            sceneStateProvider.IsCurrent.Returns(false);
            system.OnSceneIsCurrentChanged(false);

            otherSceneWorld = World.Create();
            ISceneStateProvider otherSceneStateProvider = Substitute.For<ISceneStateProvider>();
            otherSceneStateProvider.IsCurrent.Returns(true);
            otherSceneWorld.Create(new PBAvatarShape { Wearables = { OTHER_SCENE_WEARABLE }, IsDirty = true }, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));

            using var otherSceneSystem = new PropagateSceneWearablesToAvatarsSystem(otherSceneWorld, SceneDataSub(OTHER_SCENE_ID), otherSceneStateProvider,
                entityParticipantTable, globalWorld, globalPlayerEntity);

            otherSceneSystem.Update(0);
            ClearOverrideDirtyFlag();

            // Act
            system.FinalizeComponents(world.Query(in FINALIZE_QUERY));

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(OTHER_SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsFalse(sceneWearables.IsDirty);
        }

        [Test]
        public void ClearOverrideOnFinalize()
        {
            // Arrange
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            // Act
            system.FinalizeComponents(world.Query(in FINALIZE_QUERY));

            // Assert
            AssertOverrideCleared();
        }

        [Test]
        public void ApplyOnlyOwnSceneWearablesToRemotePlayerThroughTheBridgeEntity()
        {
            // Arrange — the scene writes to the CRDT bridge's entity, while the SDKProfile lives on the
            // multiplayer bridge's separate one; the two share nothing but the CRDT id.
            CreateRemoteAvatarShape(CATALYST_WEARABLE, OTHER_SCENE_WEARABLE, SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);

            // Act
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride(globalRemoteEntity);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
            Assert.IsEmpty(GetOverride().Urns);
        }

        [Test]
        public void ApplyToRemotePlayerWhoseProfileLivesOnTheSameEntity()
        {
            // Arrange
            CreateRemoteAvatarShape(SCENE_WEARABLE, out Entity sceneEntity);
            world.Add(sceneEntity, NewSdkProfile(REMOTE_WALLET));

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride(globalRemoteEntity).Urns);
        }

        [Test]
        public void NotAddOverrideToRemotePlayerWithoutOwnSceneWearables()
        {
            // Arrange
            CreateRemoteAvatarShape(CATALYST_WEARABLE, OTHER_SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<SceneWearablesOverride>(globalRemoteEntity));
        }

        [Test]
        public void ReplaceRemoteSceneWearablesInPlace()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            List<URN> urns = GetOverride(globalRemoteEntity).Urns;
            ClearOverrideDirtyFlag(globalRemoteEntity);

            pbAvatarShape.Wearables.Add(SCENE_WEARABLE_2);
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride(globalRemoteEntity);
            Assert.AreSame(urns, sceneWearables.Urns);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE), new URN(SCENE_WEARABLE_2) }, sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }

        [Test]
        public void ClearRemoteOverrideWhenComponentIsRemoved()
        {
            // Arrange
            CreateRemoteAvatarShape(SCENE_WEARABLE, out Entity sceneEntity);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            world.Remove<PBAvatarShape>(sceneEntity);

            // Act
            system.Update(0);

            // Assert
            AssertOverrideCleared(globalRemoteEntity);
        }

        [Test]
        public void ClearRemoteOverrideWhenPlayerLeavesTheScene()
        {
            // Arrange — the multiplayer bridge's entity is destroyed, while the scene's own entity keeps its shape.
            CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            world.Destroy(bridgeEntity);

            // Act
            system.Update(0);

            // Assert
            AssertOverrideCleared(globalRemoteEntity);
        }

        [Test]
        public void ClearRemoteOverrideAsSoonAsTheBridgeEntityIsMarkedForDeletion()
        {
            // Arrange
            CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            world.Add(bridgeEntity, new DeleteEntityIntention());

            // Act
            system.Update(0);

            // Assert
            AssertOverrideCleared(globalRemoteEntity);
        }

        [Test]
        public void ReapplyRemoteOverrideWhenPlayerReturnsToTheScene()
        {
            // Arrange — the scene's entity outlives the player's absence; only the multiplayer bridge's one is recreated.
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            pbAvatarShape.IsDirty = false;

            world.Destroy(bridgeEntity);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            // Act
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride(globalRemoteEntity);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }

        [Test]
        public void NotReapplyRemoteOverrideWhenTheIdReturnsWithAnotherVersion()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            pbAvatarShape.IsDirty = false;

            world.Destroy(bridgeEntity);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            CRDTEntity nextVersion = CRDTEntity.Create(REMOTE_PLAYER_CRDT_ENTITY.EntityNumber, REMOTE_PLAYER_CRDT_ENTITY.EntityVersion + 1);

            // Act
            world.Create(new PlayerSceneCRDTEntity(nextVersion), NewSdkProfile(REMOTE_WALLET));
            system.Update(0);

            // Assert
            SceneWearablesOverride sceneWearables = GetOverride(globalRemoteEntity);
            Assert.IsEmpty(sceneWearables.Urns);
            Assert.IsFalse(sceneWearables.IsDirty);
        }

        [Test]
        public void ClearRemoteOverrideWhenSceneStopsBeingCurrent()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);
            pbAvatarShape.IsDirty = false;

            // Act
            sceneStateProvider.IsCurrent.Returns(false);
            system.OnSceneIsCurrentChanged(false);

            // Assert
            AssertOverrideCleared(globalRemoteEntity);
        }

        [Test]
        public void ClearRemoteOverrideOnFinalize()
        {
            // Arrange
            CreateRemoteAvatarShape(SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            // Act
            system.FinalizeComponents(world.Query(in FINALIZE_QUERY));

            // Assert
            AssertOverrideCleared(globalRemoteEntity);
        }

        [Test]
        public void NotApplyToRemotePlayerWhenSceneIsNotCurrent()
        {
            // Arrange
            sceneStateProvider.IsCurrent.Returns(false);
            CreateRemoteAvatarShape(SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<SceneWearablesOverride>(globalRemoteEntity));
        }

        [Test]
        public void RetryUntilTheRemotePlayerProfileArrives()
        {
            // Arrange — the multiplayer bridge creates the player entity before the profile reaches it.
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = world.Create(new PlayerSceneCRDTEntity(REMOTE_PLAYER_CRDT_ENTITY));
            system.Update(0);

            Assert.IsFalse(globalWorld.Has<SceneWearablesOverride>(globalRemoteEntity));

            // The dirty flag is reset at the end of every frame.
            pbAvatarShape.IsDirty = false;

            // Act
            world.Add(bridgeEntity, NewSdkProfile(REMOTE_WALLET));
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride(globalRemoteEntity).Urns);
        }

        [Test]
        public void StopRetryingOnceNoPlayerBacksTheEntity()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = world.Create(new PlayerSceneCRDTEntity(REMOTE_PLAYER_CRDT_ENTITY));
            system.Update(0);
            pbAvatarShape.IsDirty = false;

            world.Destroy(bridgeEntity);
            system.Update(0);

            // Act — a profile showing up later is not picked up without a new write from the scene.
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<SceneWearablesOverride>(globalRemoteEntity));
        }

        [Test]
        public void ClearThePreviousAvatarWhenTheIdLandsOnAnotherPlayer()
        {
            // Arrange
            Entity secondGlobalEntity = globalWorld.Create();
            entityParticipantTable.Register(SECOND_WALLET, secondGlobalEntity, RoomSource.Island);

            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);
            ClearOverrideDirtyFlag(globalRemoteEntity);

            world.Set(bridgeEntity, NewSdkProfile(SECOND_WALLET));
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            AssertOverrideCleared(globalRemoteEntity);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride(secondGlobalEntity).Urns);
        }

        [Test]
        public void SkipClearingAnAvatarThatIsGone()
        {
            // Arrange — the remote player disconnected: its avatar is destroyed and its id handed to a stranger.
            CreateRemoteAvatarShape(SCENE_WEARABLE, out Entity sceneEntity);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            globalWorld.Destroy(globalRemoteEntity);
            Entity stranger = globalWorld.Create(new SceneWearablesOverride(new List<URN> { new URN(OTHER_SCENE_WEARABLE) }));
            world.Remove<PBAvatarShape>(sceneEntity);

            // Act
            Assert.DoesNotThrow(() => system.Update(0));

            // Assert
            SceneWearablesOverride strangerWearables = GetOverride(stranger);
            CollectionAssert.AreEqual(new[] { new URN(OTHER_SCENE_WEARABLE) }, strangerWearables.Urns);
            Assert.IsFalse(strangerWearables.IsDirty);
        }

        private static ISceneData SceneDataSub(string sceneId)
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition(sceneId, new SceneMetadata()));
            return sceneData;
        }

        private static SDKProfile NewSdkProfile(string userId)
        {
            var sdkProfile = new SDKProfile();
            sdkProfile.OverrideWith(Profile.NewRandomProfile(userId));
            return sdkProfile;
        }

        private PBAvatarShape CreateScenePlayerAvatarShape(params string[] wearables)
        {
            var pbAvatarShape = new PBAvatarShape { IsDirty = true };
            pbAvatarShape.Wearables.AddRange(wearables);
            scenePlayerEntity = world.Create(pbAvatarShape, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
            return pbAvatarShape;
        }

        private PBAvatarShape CreateRemoteAvatarShape(params string[] wearables) =>
            CreateRemoteAvatarShape(wearables, out _);

        private void CreateRemoteAvatarShape(string wearable, out Entity sceneEntity) =>
            CreateRemoteAvatarShape(new[] { wearable }, out sceneEntity);

        private PBAvatarShape CreateRemoteAvatarShape(string[] wearables, out Entity sceneEntity)
        {
            var pbAvatarShape = new PBAvatarShape { IsDirty = true };
            pbAvatarShape.Wearables.AddRange(wearables);
            sceneEntity = world.Create(pbAvatarShape, REMOTE_PLAYER_CRDT_ENTITY);
            return pbAvatarShape;
        }

        private Entity CreateRemoteBridgeEntity(string userId) =>
            world.Create(new PlayerSceneCRDTEntity(REMOTE_PLAYER_CRDT_ENTITY), NewSdkProfile(userId));

        private SceneWearablesOverride GetOverride() =>
            GetOverride(globalPlayerEntity);

        private SceneWearablesOverride GetOverride(Entity avatarEntity) =>
            globalWorld.Get<SceneWearablesOverride>(avatarEntity);

        private void ClearOverrideDirtyFlag() =>
            ClearOverrideDirtyFlag(globalPlayerEntity);

        private void ClearOverrideDirtyFlag(Entity avatarEntity) =>
            globalWorld.Get<SceneWearablesOverride>(avatarEntity).IsDirty = false;

        private void AssertOverrideCleared() =>
            AssertOverrideCleared(globalPlayerEntity);

        private void AssertOverrideCleared(Entity avatarEntity)
        {
            SceneWearablesOverride sceneWearables = GetOverride(avatarEntity);
            Assert.IsEmpty(sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }
    }
}
