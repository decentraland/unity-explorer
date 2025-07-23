using DCL.ECSComponents;
using Decentraland.Common;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;
using ECS.Unity.GltfNodeModifiers.Systems;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;
using Entity = Arch.Core.Entity;

namespace ECS.Unity.GltfNodeModifiers.Tests
{
    public class UpdateGltfNodeModifierSystemShould : UnitySystemTestBase<UpdateGltfNodeModifierSystem>
    {
        private GameObject rootGameObject;
        private GameObject childGameObject;
        private MeshRenderer rootRenderer;
        private MeshRenderer childRenderer;
        private Material originalRootMaterial;
        private Material originalChildMaterial;
        private Material testMaterial;

        [SetUp]
        public void SetUp()
        {
            system = new UpdateGltfNodeModifierSystem(world);

            // Create test GameObjects with renderers
            rootGameObject = new GameObject("Root");
            childGameObject = new GameObject("Child");
            childGameObject.transform.SetParent(rootGameObject.transform);

            rootRenderer = rootGameObject.AddComponent<MeshRenderer>();
            childRenderer = childGameObject.AddComponent<MeshRenderer>();

            originalRootMaterial = DefaultMaterial.New();
            originalChildMaterial = DefaultMaterial.New();
            testMaterial = DefaultMaterial.New();

            rootRenderer.sharedMaterial = originalRootMaterial;
            childRenderer.sharedMaterial = originalChildMaterial;
        }

        [TearDown]
        public void TearDown()
        {
            if (rootGameObject != null)
                Object.DestroyImmediate(rootGameObject);

            if (originalRootMaterial != null)
                Object.DestroyImmediate(originalRootMaterial);

            if (originalChildMaterial != null)
                Object.DestroyImmediate(originalChildMaterial);

            if (testMaterial != null)
                Object.DestroyImmediate(testMaterial);
        }

        private GltfContainerComponent CreateGltfContainer()
        {
            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                world,
                new GetGltfContainerAssetIntention("test", "test_hash", new CancellationTokenSource()),
                PartitionComponent.TOP_PRIORITY);

            var asset = GltfContainerAsset.Create(rootGameObject, null);
            asset.Renderers.Add(rootRenderer);
            asset.Renderers.Add(childRenderer);

            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            return new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished,
                RootGameObject = rootGameObject,
                OriginalMaterials = new Dictionary<Renderer, Material>
                {
                    { rootRenderer, originalRootMaterial },
                    { childRenderer, originalChildMaterial },
                },
            };
        }

        [Test]
        public void UpdateGlobalModifier_TransitionFromIndividualToGlobal()
        {
            // Arrange - Start with individual modifier setup (simulating SetupGltfNodeModifierSystem outcome)
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.green),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome for individual modifier
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.green), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Update to global modifier
            var globalModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            world.Set(entity, globalModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childEntity), Is.True);
            Assert.That(world.Has<GltfNode>(childEntity), Is.False);
            Assert.That(world.Has<GltfNode>(entity), Is.True); // Container should now have GltfNode

            Components.GltfNodeModifiers updatedNodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(updatedNodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));
            Assert.That(updatedNodeModifiers.GltfNodeEntities[0], Is.EqualTo(entity));

            GltfNode gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2)); // Should have all renderers
        }

        [Test]
        public void UpdateIndividualModifiers_TransitionFromGlobalToIndividual()
        {
            // Arrange - Start with global modifier setup (simulating SetupGltfNodeModifierSystem outcome)
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome for global modifier
            world.Add(entity, new GltfNode(new[] { rootRenderer, childRenderer }, entity, string.Empty));

            world.Add(entity, CreatePbrMaterial(Color.red)); // Add material without PartitionComponent for global

            nodeModifiers.GltfNodeEntities.Add(entity);
            world.Set(entity, nodeModifiers);

            // Act - Update to individual modifiers
            var individualModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.green),
                    },
                },
            };

            world.Set(entity, individualModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNode>(entity), Is.False);
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(entity), Is.True);

            Components.GltfNodeModifiers updatedNodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(updatedNodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));

            Entity childNodeEntity = updatedNodeModifiers.GltfNodeEntities[0];
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);
        }

        [Test]
        public void UpdateExistingGltfNodeEntity_ModifyMaterial()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.green),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.green), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Update with new material
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreateUnlitMaterial(Color.blue),
                    },
                },
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<PBMaterial>(childEntity), Is.True);
            PBMaterial pbMaterial = world.Get<PBMaterial>(childEntity);
            Assert.That(pbMaterial.Unlit, Is.Not.Null);
            Assert.That(pbMaterial.IsDirty, Is.True);
        }

        [Test]
        public void CleanupOrphanedEntities_WhenModifiersRemoved()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.green),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.green), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Remove all modifiers
            var emptyModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
            };

            world.Set(entity, emptyModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childEntity), Is.True);
            Assert.That(world.Has<GltfNode>(childEntity), Is.False);
            Components.GltfNodeModifiers updatedNodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(updatedNodeModifiers.GltfNodeEntities.Count, Is.EqualTo(0));
        }

        [Test]
        public void SkipUpdateWhenNotDirty()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Not dirty
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers();
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers updatedNodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);

            // Should not create any new entities because isDirty is false
            Assert.That(updatedNodeModifiers.GltfNodeEntities, Is.Null.Or.Empty);
        }

        [Test]
        public void UpdateMaterialFromPbrToUnlit()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Update to Unlit material
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreateUnlitMaterial(Color.blue),
                    },
                },
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            PBMaterial pbMaterial = world.Get<PBMaterial>(childEntity);
            Assert.That(pbMaterial.Pbr, Is.Null);
            Assert.That(pbMaterial.Unlit, Is.Not.Null);
            Assert.That(pbMaterial.IsDirty, Is.True);
        }

        [Test]
        public void RemoveMaterialFromExistingEntity()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Update to modifier without material (shadow only)
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        CastShadows = true,

                        // No material
                    },
                },
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childEntity), Is.True);

            GltfNodeMaterialCleanupIntention cleanupIntention = world.Get<GltfNodeMaterialCleanupIntention>(childEntity);
            Assert.That(cleanupIntention.Destroy, Is.False); // Should not destroy entity, just clean up material
        }

        [Test]
        public void UpdateShadowOverrideFromDefaultToExplicit()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),

                        // No shadow override initially - defaults to casting shadows
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Update to explicitly disable shadows
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.blue),
                        CastShadows = false,
                    },
                },
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<PBMaterial>(childEntity), Is.True);
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void UpdateModifierWithoutAnyOverrides()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Setup system would have set this to false
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY, nodeModifiers);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Update to modifier without any overrides
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",

                        // No material or shadow overrides
                    },
                },
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childEntity), Is.True);

            // Shadow should default to On since no override specified
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        private static PBMaterial CreatePbrMaterial(Color color) =>
            new()
            {
                Pbr = new PBMaterial.Types.PbrMaterial
                {
                    AlbedoColor = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a },
                },
            };

        private static PBMaterial CreateUnlitMaterial(Color color) =>
            new()
            {
                Unlit = new PBMaterial.Types.UnlitMaterial
                {
                    DiffuseColor = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a },
                },
            };
    }
}
