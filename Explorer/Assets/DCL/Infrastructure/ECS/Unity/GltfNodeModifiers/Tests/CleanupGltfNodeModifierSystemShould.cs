using DCL.ECSComponents;
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
using Utility.Primitives;
using Entity = Arch.Core.Entity;

namespace ECS.Unity.GltfNodeModifiers.Tests
{
    public class CleanupGltfNodeModifierSystemShould : UnitySystemTestBase<CleanupGltfNodeModifierSystem>
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
            system = new CleanupGltfNodeModifierSystem(world);

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
                GltfNodeEntities = new List<Entity>(),
                OriginalMaterials = new Dictionary<Renderer, Material>
                {
                    { rootRenderer, originalRootMaterial },
                    { childRenderer, originalChildMaterial }
                }
            };
        }

                [Test]
         public void HandleGltfNodeModifiersRemoval_CleanupAllEntities()
         {
             // Arrange - Simulate SetupGltfNodeModifierSystem outcome
             var gltfContainer = CreateGltfContainer();
             var gltfNodeModifiers = new PBGltfNodeModifiers
             {
                 IsDirty = false,
                 Modifiers = {
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "Child",
                         Material = CreatePbrMaterial(Color.red)
                     }
                 }
             };

             // Create entity with components (simulating setup completion)
             var entity = world.Create();
             world.Add(entity, gltfNodeModifiers);
             world.Add(entity, gltfContainer);
             world.Add(entity, new Components.GltfNodeModifiers());
             world.Add(entity, PartitionComponent.TOP_PRIORITY);

             // Manually simulate SetupGltfNodeModifierSystem outcome
             var childEntity = world.Create();
             world.Add(childEntity, new GltfNode
             {
                 Renderers = new System.Collections.Generic.List<Renderer> { childRenderer },
                 ContainerEntity = entity,
                 Path = "Child"
             });
             world.Add(childEntity, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

             var updatedContainer = world.Get<GltfContainerComponent>(entity);
             updatedContainer.GltfNodeEntities.Add(childEntity);
             world.Set(entity, updatedContainer);

             // Act - Remove PBGltfNodeModifiers to trigger HandleGltfNodeModifiersRemoval query
             world.Remove<PBGltfNodeModifiers>(entity);
             system.Update(0);

             // Assert
             Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childEntity), Is.True);
             Assert.That(world.Has<GltfNode>(childEntity), Is.False);
             Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should be removed by system
             updatedContainer = world.Get<GltfContainerComponent>(entity);
             Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0));
        }

                [Test]
         public void HandleGltfNodeModifiersCleanup_WithCleanupIntention()
         {
             // Arrange - Simulate SetupGltfNodeModifierSystem outcome
             var gltfContainer = CreateGltfContainer();
             var gltfNodeModifiers = new PBGltfNodeModifiers
             {
                 IsDirty = false,
                 Modifiers = {
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "Child",
                         Material = CreatePbrMaterial(Color.blue)
                     }
                 }
             };

             // Create entity with all required components for HandleGltfNodeModifiersCleanup query
             var entity = world.Create();
             world.Add(entity, gltfNodeModifiers); // Required for cleanup query
             world.Add(entity, gltfContainer);
             world.Add(entity, new Components.GltfNodeModifiers());

             // Manually simulate SetupGltfNodeModifierSystem outcome
             var childEntity = world.Create();
             world.Add(childEntity, new GltfNode
             {
                 Renderers = new System.Collections.Generic.List<Renderer> { childRenderer },
                 ContainerEntity = entity,
                 Path = "Child"
             });
             world.Add(childEntity, CreatePbrMaterial(Color.blue), PartitionComponent.TOP_PRIORITY);

             var updatedContainer = world.Get<GltfContainerComponent>(entity);
             updatedContainer.GltfNodeEntities.Add(childEntity);
             world.Set(entity, updatedContainer);

             // Act - Add cleanup intention to trigger HandleGltfNodeModifiersCleanup query
             world.Add(entity, new GltfNodeModifiersCleanupIntention());
             system.Update(0);

             // Assert
             Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childEntity), Is.True);
             Assert.That(world.Has<GltfNode>(childEntity), Is.False);
             Assert.That(world.Has<GltfNodeModifiersCleanupIntention>(entity), Is.False); // Should be removed
             updatedContainer = world.Get<GltfContainerComponent>(entity);
             Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0));
        }

                        [Test]
        public void HandleCleanupWithNoGltfNodeEntities()
        {
            // Arrange - Entity with components but no actual GltfNode entities (simulating empty setup)
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false,
                Modifiers = { } // Empty modifiers
            };

            // Create entity with all required components for HandleGltfNodeModifiersCleanup query
            var entity = world.Create();
            world.Add(entity, gltfNodeModifiers); // Required for cleanup query
            world.Add(entity, gltfContainer);
            world.Add(entity, new Components.GltfNodeModifiers());
            world.Add(entity, new GltfNodeModifiersCleanupIntention());

            // Act - Run cleanup system (should handle empty GltfNodeEntities gracefully)
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeModifiersCleanupIntention>(entity), Is.False); // Should be removed
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0)); // Should remain empty
        }

                [Test]
        public void HandleCleanupWithNullGltfNodeEntities()
        {
            // Arrange - Entity with components but null GltfNodeEntities
            var gltfContainer = CreateGltfContainer();
            gltfContainer.GltfNodeEntities = null; // Explicitly null

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false,
                Modifiers = { } // Empty modifiers
            };

            // Create entity with all required components for HandleGltfNodeModifiersCleanup query
            var entity = world.Create();
            world.Add(entity, gltfNodeModifiers); // Required for cleanup query
            world.Add(entity, gltfContainer);
            world.Add(entity, new Components.GltfNodeModifiers());
            world.Add(entity, new GltfNodeModifiersCleanupIntention());

            // Act - Run cleanup system (should not throw even with null GltfNodeEntities)
            Assert.DoesNotThrow(() => system.Update(0));

            // Assert
            Assert.That(world.Has<GltfNodeModifiersCleanupIntention>(entity), Is.False); // Should be removed
        }



        private static PBMaterial CreatePbrMaterial(Color color)
        {
            return new PBMaterial
            {
                Pbr = new PBMaterial.Types.PbrMaterial
                {
                    AlbedoColor = new Decentraland.Common.Color4 { R = color.r, G = color.g, B = color.b, A = color.a }
                }
            };
        }
     }
}
