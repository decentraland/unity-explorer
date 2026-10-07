using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.AvatarShape.Components;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.SDKComponents.SceneWearables.Systems;
using ECS.LifeCycle.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;

namespace DCL.SDKComponents.SceneWearables.Tests
{
    public class PropagateSceneWearablesToPlayerSystemShould : UnitySystemTestBase<PropagateSceneWearablesToPlayerSystem>
    {
        private const string SCENE_ID = "bafkreiscene";
        private const string OTHER_SCENE_ID = "bafkreiotherscene";
        private const string CATALYST_WEARABLE = "urn:decentraland:off-chain:base-avatars:eyes_00";
        private const string SCENE_WEARABLE = "urn:decentraland:off-chain:scene-wearable:" + SCENE_ID + "-hathash";
        private const string SCENE_WEARABLE_2 = "urn:decentraland:off-chain:scene-wearable:" + SCENE_ID + "-kilthash";
        private const string OTHER_SCENE_WEARABLE = "urn:decentraland:off-chain:scene-wearable:" + OTHER_SCENE_ID + "-kilthash";

        private static readonly QueryDescription FINALIZE_QUERY = new QueryDescription().WithAll<CRDTEntity>();

        private World globalWorld = null!;
        private World? otherSceneWorld;
        private Entity globalPlayerEntity;
        private Entity scenePlayerEntity;
        private ISceneStateProvider sceneStateProvider = null!;

        [SetUp]
        public void SetUp()
        {
            globalWorld = World.Create();
            globalPlayerEntity = globalWorld.Create(new SceneWearablesOverride(new List<URN>()));

            sceneStateProvider = Substitute.For<ISceneStateProvider>();
            sceneStateProvider.IsCurrent.Returns(true);

            system = new PropagateSceneWearablesToPlayerSystem(world, SceneDataSub(SCENE_ID), sceneStateProvider, globalWorld, globalPlayerEntity);
        }

        protected override void OnTearDown()
        {
            otherSceneWorld?.Dispose();
            globalWorld.Dispose();
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

            using var otherSceneSystem = new PropagateSceneWearablesToPlayerSystem(otherSceneWorld, SceneDataSub(OTHER_SCENE_ID), otherSceneStateProvider, globalWorld, globalPlayerEntity);
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

        private static ISceneData SceneDataSub(string sceneId)
        {
            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition(sceneId, new SceneMetadata()));
            return sceneData;
        }

        private PBAvatarShape CreateScenePlayerAvatarShape(params string[] wearables)
        {
            var pbAvatarShape = new PBAvatarShape { IsDirty = true };
            pbAvatarShape.Wearables.AddRange(wearables);
            scenePlayerEntity = world.Create(pbAvatarShape, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
            return pbAvatarShape;
        }

        private SceneWearablesOverride GetOverride() =>
            globalWorld.Get<SceneWearablesOverride>(globalPlayerEntity);

        private void ClearOverrideDirtyFlag() =>
            globalWorld.Get<SceneWearablesOverride>(globalPlayerEntity).IsDirty = false;

        private void AssertOverrideCleared()
        {
            SceneWearablesOverride sceneWearables = GetOverride();
            Assert.IsEmpty(sceneWearables.Urns);
            Assert.IsTrue(sceneWearables.IsDirty);
        }
    }
}
