using Arch.Core;
using CommunicationData.URLHelpers;
using CRDT;
using CrdtEcsBridge.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.ECSComponents;
using DCL.Ipfs;
using DCL.Optimization.PerformanceBudgeting;
using DCL.SDKComponents.SceneWearables.Systems;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SDKComponents.SceneWearables.Tests
{
    public class ResolveSceneWearablePathsSystemShould : UnitySystemTestBase<ResolveSceneWearablePathsSystem>
    {
        private const string SCENE_ID = "bafkreisceneid";
        private const string HAT_PATH = "assets/wearables/cap_hat.glb";
        private const string HAT_HASH = "bafkreihathash";
        private const string UPPERCASE_HAT_PATH = "assets/wearables/Crown_HAT.GLB";
        private const string UPPERCASE_HAT_HASH = "bafkreicrownhash";
        private const string CATALYST_WEARABLE = "urn:decentraland:off-chain:base-avatars:eyes_00";
        private const string EXPECTED_URN = SceneWearableDTOFactory.SCENE_WEARABLE_PREFIX + ":" + SCENE_ID + "-" + HAT_HASH + "-224e2dcb";

        private WearableStorage wearableStorage = null!;
        private IPerformanceBudget unloadBudget = null!;

        [SetUp]
        public void SetUp()
        {
            ISceneContent sceneContent = Substitute.For<ISceneContent>();

            sceneContent.TryGetHash(HAT_PATH, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = HAT_HASH;
                             return true;
                         });

            sceneContent.TryGetHash(UPPERCASE_HAT_PATH, out Arg.Any<string>())
                        .Returns(x =>
                         {
                             x[1] = UPPERCASE_HAT_HASH;
                             return true;
                         });

            ISceneData sceneData = Substitute.For<ISceneData>();
            sceneData.SceneShortInfo.Returns(new SceneShortInfo(Vector2Int.zero, SCENE_ID));
            sceneData.SceneContent.Returns(sceneContent);

            sceneData.SceneEntityDefinition.Returns(new SceneEntityDefinition(SCENE_ID, new SceneMetadata(), AssetBundleManifestVersion.CreateManualManifest())
            {
                content = new[] { new ContentDefinition { file = HAT_PATH, hash = HAT_HASH } },
            });

            wearableStorage = new WearableStorage();
            unloadBudget = Substitute.For<IPerformanceBudget>();
            unloadBudget.TrySpendBudget().Returns(true);
            system = new ResolveSceneWearablePathsSystem(world, sceneData, wearableStorage, false);
        }

        [Test]
        public void ReplaceScenePathWithSeededUrn()
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { CATALYST_WEARABLE, HAT_PATH }, IsDirty = true };
            world.Create(pbAvatarShape);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { CATALYST_WEARABLE, EXPECTED_URN }, pbAvatarShape.Wearables);
            Assert.IsTrue(wearableStorage.TryGetElement(EXPECTED_URN, out IWearable wearable));
            Assert.IsTrue(wearable.Model.Succeeded);
            Assert.AreEqual("hat", wearable.GetCategory());
        }

        [Test]
        public void ReplaceScenePathOnPlayerEntity()
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true };
            world.Create(pbAvatarShape, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { EXPECTED_URN }, pbAvatarShape.Wearables);
        }

        [Test]
        public void DropUnresolvableScenePaths()
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { "assets/wearables/no_category.glb", CATALYST_WEARABLE, "assets/wearables/missing_hat.glb" }, IsDirty = true };
            world.Create(pbAvatarShape);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { CATALYST_WEARABLE }, pbAvatarShape.Wearables);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void DropItemsThatAreNeitherUrnNorGlbPath(bool onPlayerEntity)
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { "wearables/kilt/kilt.json", CATALYST_WEARABLE, "not-a-urn" }, IsDirty = true };

            if (onPlayerEntity)
                world.Create(pbAvatarShape, new CRDTEntity(SpecialEntitiesID.PLAYER_ENTITY));
            else
                world.Create(pbAvatarShape);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { CATALYST_WEARABLE }, pbAvatarShape.Wearables);
        }

        [Test]
        public void BeIdempotent()
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true };
            world.Create(pbAvatarShape);
            system.Update(0);
            wearableStorage.TryGetElement(EXPECTED_URN, out IWearable seeded);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { EXPECTED_URN }, pbAvatarShape.Wearables);
            Assert.IsTrue(wearableStorage.TryGetElement(EXPECTED_URN, out IWearable wearable));
            Assert.AreSame(seeded, wearable);
        }

        [Test]
        public void IgnoreNotDirtyComponent()
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = false };
            world.Create(pbAvatarShape);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { HAT_PATH }, pbAvatarShape.Wearables);
            Assert.IsFalse(wearableStorage.TryGetElement(EXPECTED_URN, out _));
        }

        [Test]
        public void OverwriteNotLoadedStorageEntry()
        {
            // Arrange
            IWearable notLoaded = IWearable.NewEmpty();
            wearableStorage.Set(new URN(EXPECTED_URN), notLoaded);

            var pbAvatarShape = new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true };
            world.Create(pbAvatarShape);

            // Act
            system.Update(0);

            // Assert
            Assert.IsTrue(wearableStorage.TryGetElement(EXPECTED_URN, out IWearable wearable));
            Assert.AreNotSame(notLoaded, wearable);
            Assert.IsTrue(wearable.Model.Succeeded);
        }

        [Test]
        public void ResolveUppercaseExtension()
        {
            // Arrange
            var pbAvatarShape = new PBAvatarShape { Wearables = { UPPERCASE_HAT_PATH }, IsDirty = true };
            world.Create(pbAvatarShape);

            // Act
            system.Update(0);

            // Assert
            CollectionAssert.AreEqual(new[] { SceneWearableDTOFactory.SCENE_WEARABLE_PREFIX + ":" + SCENE_ID + "-" + UPPERCASE_HAT_HASH + "-afa65d20" }, pbAvatarShape.Wearables);
        }

        [Test]
        public void KeepSeededWearableWhileSceneIsLoaded()
        {
            // Arrange
            world.Create(new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true });
            system.Update(0);

            // Act
            wearableStorage.Unload(unloadBudget);

            // Assert
            Assert.IsTrue(wearableStorage.TryGetElement(EXPECTED_URN, out _));
        }

        [Test]
        public void QualifySeededWearablesForUnloadingOnFinalize()
        {
            // Arrange
            world.Create(new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true });
            system.Update(0);

            // Act
            system.FinalizeComponents(world.Query(QueryDescription.Null));
            wearableStorage.Unload(unloadBudget);

            // Assert
            Assert.IsFalse(wearableStorage.TryGetElement(EXPECTED_URN, out _));
        }

        [Test]
        public void KeepWearableSeededAgainAfterFinalize()
        {
            // Arrange
            world.Create(new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true });
            system.Update(0);
            system.FinalizeComponents(world.Query(QueryDescription.Null));
            world.Create(new PBAvatarShape { Wearables = { HAT_PATH }, IsDirty = true });
            system.Update(0);

            // Act
            wearableStorage.Unload(unloadBudget);

            // Assert
            Assert.IsTrue(wearableStorage.TryGetElement(EXPECTED_URN, out _));
        }
    }
}
