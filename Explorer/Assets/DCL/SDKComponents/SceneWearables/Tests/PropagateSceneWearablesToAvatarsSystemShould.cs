using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.AvatarRendering.Loading.Components;
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
using UnityEngine;
using Color3 = Decentraland.Common.Color3;

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
            EcsTestsUtils.SetUpFeaturesRegistry();

            globalWorld = World.Create();
            globalPlayerEntity = globalWorld.Create(new SceneAvatarShapeOverride(new List<URN>()));
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
        public void ReplaceWearablesWithCatalogAndOwnSceneWearablesOnly()
        {
            // Arrange
            CreateScenePlayerAvatarShape(CATALYST_WEARABLE, OTHER_SCENE_WEARABLE, SCENE_WEARABLE);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(CATALYST_WEARABLE), new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
        }

        [Test]
        public void KeepProfileWearablesWhenTheListIsEmpty()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape();
            pbAvatarShape.BodyShape = BodyShape.FEMALE;

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            Assert.IsEmpty(sceneOverride.Wearables);
            Assert.AreEqual(BodyShape.FEMALE, sceneOverride.BodyShape);
            Assert.IsTrue(sceneOverride.IsDirty);
        }

        [Test]
        public void KeepProfileWearablesWhenOnlyOtherScenesWearablesAreListed()
        {
            // Arrange
            CreateScenePlayerAvatarShape(OTHER_SCENE_WEARABLE);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            Assert.IsTrue(sceneOverride.IsEmpty);
            Assert.IsFalse(sceneOverride.IsDirty);
        }

        [Test]
        public void ApplyBodyShapeColorsAndShowOnlyWearables()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            pbAvatarShape.BodyShape = BodyShape.FEMALE;
            pbAvatarShape.SkinColor = new Color3 { R = 0.1f, G = 0.2f, B = 0.3f };
            pbAvatarShape.HairColor = new Color3 { R = 1f };
            pbAvatarShape.EyeColor = new Color3 { G = 1f };
            pbAvatarShape.ShowOnlyWearables = true;

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            Assert.AreEqual(BodyShape.FEMALE, sceneOverride.BodyShape);
            Assert.AreEqual(new Color(0.1f, 0.2f, 0.3f), sceneOverride.SkinColor);
            Assert.AreEqual(new Color(1f, 0f, 0f), sceneOverride.HairColor);
            Assert.AreEqual(new Color(0f, 1f, 0f), sceneOverride.EyesColor);
            Assert.AreEqual(true, sceneOverride.ShowOnlyWearables);
            Assert.IsTrue(sceneOverride.IsDirty);
        }

        [Test]
        public void LeaveUnsetFieldsToTheProfile()
        {
            // Arrange
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            Assert.IsNull(sceneOverride.BodyShape);
            Assert.IsNull(sceneOverride.SkinColor);
            Assert.IsNull(sceneOverride.HairColor);
            Assert.IsNull(sceneOverride.EyesColor);
            Assert.IsNull(sceneOverride.ShowOnlyWearables);
        }

        [Test]
        public void IgnoreNameAndExpressionFields()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            pbAvatarShape.Name = "Scene name";
            pbAvatarShape.Id = "scene-id";
            pbAvatarShape.ExpressionTriggerId = "wave";
            pbAvatarShape.ExpressionTriggerTimestamp = 1;
            pbAvatarShape.Talking = true;
            pbAvatarShape.Emotes.Add("urn:decentraland:off-chain:base-emotes:wave");
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(GetOverride().IsDirty);
        }

        [Test]
        public void ReapplyChangedColorOnly()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            pbAvatarShape.HairColor = new Color3 { R = 1f };
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            Assert.AreEqual(new Color(1f, 0f, 0f), sceneOverride.HairColor);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
        }

        [Test]
        public void ApplyOnlyTheFirstWearablesBeyondTheLimit()
        {
            // Arrange
            var wearables = new string[40];

            for (var i = 0; i < wearables.Length; i++)
                wearables[i] = $"urn:decentraland:off-chain:base-avatars:item_{i}";

            CreateScenePlayerAvatarShape(wearables);

            // Act
            system.Update(0);

            // Assert
            List<URN> applied = GetOverride().Wearables;
            Assert.AreEqual(32, applied.Count);
            Assert.AreEqual(new URN(wearables[31]), applied[31]);
        }

        [Test]
        public void AddOverrideToLocalPlayerOnFirstUse()
        {
            // Arrange
            globalWorld.Remove<SceneAvatarShapeOverride>(globalPlayerEntity);
            CreateScenePlayerAvatarShape(SCENE_WEARABLE);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
        }

        [Test]
        public void IgnoreAvatarShapeOfOtherEntities()
        {
            // Arrange
            world.Create(new PBAvatarShape { Wearables = { SCENE_WEARABLE }, IsDirty = true }, new CRDTEntity(512));

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            Assert.IsEmpty(sceneOverride.Wearables);
            Assert.IsFalse(sceneOverride.IsDirty);
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
            Assert.IsFalse(globalWorld.Has<SceneAvatarShapeOverride>(globalRemoteEntity));
            Assert.IsEmpty(GetOverride().Wearables);
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
            Assert.IsEmpty(GetOverride().Wearables);
        }

        [Test]
        public void NotMarkDirtyWhenSceneWearablesAreUnchanged()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape(CATALYST_WEARABLE, SCENE_WEARABLE);
            system.Update(0);
            ClearOverrideDirtyFlag();

            pbAvatarShape.Wearables.Add(OTHER_SCENE_WEARABLE);
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(CATALYST_WEARABLE), new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsFalse(sceneOverride.IsDirty);
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
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE), new URN(SCENE_WEARABLE_2) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
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
        public void ClearBodyShapeAndColorsWhenComponentIsRemoved()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateScenePlayerAvatarShape();
            pbAvatarShape.BodyShape = BodyShape.FEMALE;
            pbAvatarShape.SkinColor = new Color3 { R = 1f };
            pbAvatarShape.ShowOnlyWearables = false;
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
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
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
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride().Wearables);
        }

        [Test]
        public void KeepOverrideOfAnotherSceneWhenNotApplied()
        {
            // Arrange
            globalWorld.Get<SceneAvatarShapeOverride>(globalPlayerEntity).Wearables.Add(OTHER_SCENE_WEARABLE);

            // Act
            system.OnSceneIsCurrentChanged(false);
            system.FinalizeComponents(world.Query(in FINALIZE_QUERY));

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(OTHER_SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsFalse(sceneOverride.IsDirty);
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
            SceneAvatarShapeOverride sceneOverride = GetOverride();
            CollectionAssert.AreEqual(new[] { new URN(OTHER_SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsFalse(sceneOverride.IsDirty);
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
        public void ApplyToRemotePlayerThroughTheBridgeEntity()
        {
            // Arrange
            CreateRemoteAvatarShape(CATALYST_WEARABLE, OTHER_SCENE_WEARABLE, SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride(globalRemoteEntity);
            CollectionAssert.AreEqual(new[] { new URN(CATALYST_WEARABLE), new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
            Assert.IsTrue(GetOverride().IsEmpty);
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
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride(globalRemoteEntity).Wearables);
        }

        [Test]
        public void NotAddOverrideToRemotePlayerWhenNothingIsOverridden()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(OTHER_SCENE_WEARABLE);
            pbAvatarShape.Name = "Scene name";
            pbAvatarShape.ExpressionTriggerId = "wave";
            CreateRemoteBridgeEntity(REMOTE_WALLET);

            // Act
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<SceneAvatarShapeOverride>(globalRemoteEntity));
        }

        [Test]
        public void AddOverrideToRemotePlayerForBodyShapeAlone()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape();
            pbAvatarShape.BodyShape = BodyShape.FEMALE;
            CreateRemoteBridgeEntity(REMOTE_WALLET);

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride(globalRemoteEntity);
            Assert.AreEqual(BodyShape.FEMALE, sceneOverride.BodyShape);
            Assert.IsEmpty(sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
        }

        [Test]
        public void ReplaceRemoteSceneWearablesInPlace()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            List<URN> urns = GetOverride(globalRemoteEntity).Wearables;
            ClearOverrideDirtyFlag(globalRemoteEntity);

            pbAvatarShape.Wearables.Add(SCENE_WEARABLE_2);
            pbAvatarShape.IsDirty = true;

            // Act
            system.Update(0);

            // Assert
            SceneAvatarShapeOverride sceneOverride = GetOverride(globalRemoteEntity);
            Assert.AreSame(urns, sceneOverride.Wearables);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE), new URN(SCENE_WEARABLE_2) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
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
            // Arrange
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
            // Arrange
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
            SceneAvatarShapeOverride sceneOverride = GetOverride(globalRemoteEntity);
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, sceneOverride.Wearables);
            Assert.IsTrue(sceneOverride.IsDirty);
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
            SceneAvatarShapeOverride sceneOverride = GetOverride(globalRemoteEntity);
            Assert.IsEmpty(sceneOverride.Wearables);
            Assert.IsFalse(sceneOverride.IsDirty);
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
            Assert.IsFalse(globalWorld.Has<SceneAvatarShapeOverride>(globalRemoteEntity));
        }

        [Test]
        public void RetryUntilTheRemotePlayerProfileArrives()
        {
            // Arrange
            PBAvatarShape pbAvatarShape = CreateRemoteAvatarShape(SCENE_WEARABLE);
            Entity bridgeEntity = world.Create(new PlayerSceneCRDTEntity(REMOTE_PLAYER_CRDT_ENTITY));
            system.Update(0);

            Assert.IsFalse(globalWorld.Has<SceneAvatarShapeOverride>(globalRemoteEntity));

            pbAvatarShape.IsDirty = false;

            // Act
            world.Add(bridgeEntity, NewSdkProfile(REMOTE_WALLET));
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride(globalRemoteEntity).Wearables);
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

            // Act
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            // Assert
            Assert.IsFalse(globalWorld.Has<SceneAvatarShapeOverride>(globalRemoteEntity));
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
            CollectionAssert.AreEqual(new[] { new URN(SCENE_WEARABLE) }, GetOverride(secondGlobalEntity).Wearables);
        }

        [Test]
        public void SkipClearingAnAvatarThatIsGone()
        {
            // Arrange — the remote player disconnected: its avatar is destroyed and its id handed to a stranger.
            CreateRemoteAvatarShape(SCENE_WEARABLE, out Entity sceneEntity);
            CreateRemoteBridgeEntity(REMOTE_WALLET);
            system.Update(0);

            globalWorld.Destroy(globalRemoteEntity);
            Entity stranger = globalWorld.Create(new SceneAvatarShapeOverride(new List<URN> { new URN(OTHER_SCENE_WEARABLE) }));
            world.Remove<PBAvatarShape>(sceneEntity);

            // Act
            Assert.DoesNotThrow(() => system.Update(0));

            // Assert
            SceneAvatarShapeOverride strangerOverride = GetOverride(stranger);
            CollectionAssert.AreEqual(new[] { new URN(OTHER_SCENE_WEARABLE) }, strangerOverride.Wearables);
            Assert.IsFalse(strangerOverride.IsDirty);
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

        private SceneAvatarShapeOverride GetOverride() =>
            GetOverride(globalPlayerEntity);

        private SceneAvatarShapeOverride GetOverride(Entity avatarEntity) =>
            globalWorld.Get<SceneAvatarShapeOverride>(avatarEntity);

        private void ClearOverrideDirtyFlag() =>
            ClearOverrideDirtyFlag(globalPlayerEntity);

        private void ClearOverrideDirtyFlag(Entity avatarEntity) =>
            globalWorld.Get<SceneAvatarShapeOverride>(avatarEntity).IsDirty = false;

        private void AssertOverrideCleared() =>
            AssertOverrideCleared(globalPlayerEntity);

        private void AssertOverrideCleared(Entity avatarEntity)
        {
            SceneAvatarShapeOverride sceneOverride = GetOverride(avatarEntity);
            Assert.IsTrue(sceneOverride.IsEmpty);
            Assert.IsTrue(sceneOverride.IsDirty);
        }
    }
}
