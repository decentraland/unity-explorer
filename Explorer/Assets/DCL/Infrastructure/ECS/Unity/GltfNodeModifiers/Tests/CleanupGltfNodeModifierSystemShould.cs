using DCL.ECSComponents;
using Decentraland.Common;
using ECS.Abstract;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;
using ECS.Unity.GltfNodeModifiers.Systems;
using ECS.Unity.Materials;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using System.Collections.Generic;
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
        private Material secondTestMaterial;

        [SetUp]
        public void SetUp()
        {
            system = new CleanupGltfNodeModifierSystem(world, new EntityEventBuffer<GltfContainerComponent>(1));

            // Create test GameObjects with renderers
            rootGameObject = new GameObject("Root");
            childGameObject = new GameObject("Child");
            childGameObject.transform.SetParent(rootGameObject.transform);

            rootRenderer = rootGameObject.AddComponent<MeshRenderer>();
            childRenderer = childGameObject.AddComponent<MeshRenderer>();

            originalRootMaterial = DefaultMaterial.New();
            originalChildMaterial = DefaultMaterial.New();
            testMaterial = DefaultMaterial.New();
            secondTestMaterial = DefaultMaterial.New();

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

            if (secondTestMaterial != null)
                Object.DestroyImmediate(secondTestMaterial);
        }

        [Test]
        public void HandleComponentRemoval_CleanupAllEntities()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            // Create entity with components (simulating setup completion)
            Entity entity = world.Create();
            world.Add(entity, gltfNodeModifiers);

            var nodeModifiers = new Components.GltfNodeModifiers(new Dictionary<Entity, string>(), new Dictionary<Renderer, Material>());
            world.Add(entity, nodeModifiers);
            world.Add(entity, PartitionComponent.TOP_PRIORITY);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();
            GltfNode gltfNode = new GltfNode(new[] { childRenderer }, entity, "Child");
            world.Add(childEntity, gltfNode, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

            // Update the GltfNodeEntities dictionary properly
            ref var nodeModifiersRef = ref world.TryGetRef<Components.GltfNodeModifiers>(entity, out bool exists);
            nodeModifiersRef.GltfNodeEntities.Add(childEntity, gltfNode.Path!);

            // Act - Remove PBGltfNodeModifiers to trigger HandleComponentRemoval query
            world.Remove<PBGltfNodeModifiers>(entity);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNode>(childEntity), Is.True);
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should be removed by system
        }

        [Test]
        public void HandleEntityDestruction_CleanupAllEntities()
        {
            // Arrange - Simulate SetupGltfNodeModifierSystem outcome
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.blue),
                    },
                },
            };

            // Create entity with components (simulating setup completion)
            Entity entity = world.Create();
            world.Add(entity, gltfNodeModifiers);

            var nodeModifiers = new Components.GltfNodeModifiers(new Dictionary<Entity, string>(), new Dictionary<Renderer, Material>());
            world.Add(entity, nodeModifiers);
            world.Add(entity, PartitionComponent.TOP_PRIORITY);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();
            GltfNode gltfNode = new GltfNode(new[] { childRenderer }, entity, "Child");
            world.Add(childEntity, gltfNode, CreatePbrMaterial(Color.blue), PartitionComponent.TOP_PRIORITY);

            // Update the GltfNodeEntities dictionary properly
            ref var nodeModifiersRef = ref world.TryGetRef<Components.GltfNodeModifiers>(entity, out bool exists);
            nodeModifiersRef.GltfNodeEntities.Add(childEntity, gltfNode.Path!);

            // Act - Add DeleteEntityIntention to trigger HandleEntityDestruction query
            world.Add(entity, new DeleteEntityIntention());
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNode>(childEntity), Is.True);
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should be removed by system
        }

        [Test]
        public void HandleGltfContainerChange_CleanupAllEntities()
        {
            // Arrange - Create EventBuffer to simulate container changes
            var eventBuffer = new EntityEventBuffer<GltfContainerComponent>(1);
            var cleanupSystem = new CleanupGltfNodeModifierSystem(world, eventBuffer);

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.yellow),
                    },
                },
            };

            // Create entity with components (simulating setup completion)
            Entity entity = world.Create();
            world.Add(entity, gltfNodeModifiers);

            var nodeModifiers = new Components.GltfNodeModifiers(new Dictionary<Entity, string>(), new Dictionary<Renderer, Material>());
            world.Add(entity, nodeModifiers);
            world.Add(entity, PartitionComponent.TOP_PRIORITY);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();
            GltfNode gltfNode = new GltfNode(new[] { childRenderer }, entity, "Child");
            world.Add(childEntity, gltfNode, CreatePbrMaterial(Color.yellow), PartitionComponent.TOP_PRIORITY);

            // Update the GltfNodeEntities dictionary properly
            ref var nodeModifiersRef = ref world.TryGetRef<Components.GltfNodeModifiers>(entity, out bool exists);
            nodeModifiersRef.GltfNodeEntities.Add(childEntity, gltfNode.Path!);

            // Simulate GltfContainerComponent change event
            var gltfContainerComponent = new GltfContainerComponent
            {
                State = LoadingState.Unknown // Simulating invalidation
            };
            world.Add(entity, gltfContainerComponent);

            // Act - Add event to buffer and trigger HandleGltfContainerChange
            eventBuffer.Add(entity, gltfContainerComponent);
            cleanupSystem.Update(0);

            // Assert
            Assert.That(world.Has<GltfNode>(childEntity), Is.True);
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should be removed by system
        }

        [Test]
        public void ReleaseDeferredNodeMaterialsAfterContainersAreDestroyed()
        {
            // Arrange
            var destroyMaterial = Substitute.For<DestroyMaterial>();
            var resetMaterialSystem = new ResetMaterialSystem(world, destroyMaterial, Substitute.For<ISceneData>());

            Entity firstContainer = CreateContainerWithMaterialOverride(rootRenderer, originalRootMaterial, testMaterial, out Entity firstNode);
            Entity secondContainer = CreateContainerWithMaterialOverride(childRenderer, originalChildMaterial, secondTestMaterial, out Entity secondNode);

            // Act
            world.Add(firstContainer, new DeleteEntityIntention());
            world.Add(secondContainer, new DeleteEntityIntention());
            system.Update(0);
            world.Destroy(firstContainer);
            world.Destroy(secondContainer);

            Assert.DoesNotThrow(() => resetMaterialSystem.Update(0));

            // Assert
            destroyMaterial.Received(1)(in Arg.Any<MaterialData>(), testMaterial);
            destroyMaterial.Received(1)(in Arg.Any<MaterialData>(), secondTestMaterial);
            Assert.That(world.IsAlive(firstNode), Is.False);
            Assert.That(world.IsAlive(secondNode), Is.False);
            Assert.That(rootRenderer.sharedMaterial, Is.EqualTo(originalRootMaterial));
            Assert.That(childRenderer.sharedMaterial, Is.EqualTo(originalChildMaterial));
        }

        private Entity CreateContainerWithMaterialOverride(Renderer renderer, Material originalMaterial, Material nodeMaterial, out Entity nodeEntity)
        {
            Entity container = world.Create(
                new PBGltfNodeModifiers { Modifiers = { new PBGltfNodeModifiers.Types.GltfNodeModifier { Path = "Child", Material = CreatePbrMaterial(Color.blue) } } },
                new Components.GltfNodeModifiers(new Dictionary<Entity, string>(), new Dictionary<Renderer, Material> { { renderer, originalMaterial } }),
                PartitionComponent.TOP_PRIORITY);

            renderer.sharedMaterial = nodeMaterial;

            var gltfNode = new GltfNode(new[] { renderer }, container, "Child");
            nodeEntity = world.Create(gltfNode, CreatePbrMaterial(Color.blue), PartitionComponent.TOP_PRIORITY,
                new MaterialComponent { Status = StreamableLoading.LifeCycle.Applied, Result = nodeMaterial });

            world.Get<Components.GltfNodeModifiers>(container).GltfNodeEntities.Add(nodeEntity, gltfNode.Path!);
            return container;
        }

        private static PBMaterial CreatePbrMaterial(Color color) =>
            new()
            {
                Pbr = new PBMaterial.Types.PbrMaterial
                {
                    AlbedoColor = new Color4 { R = color.r, G = color.g, B = color.b, A = color.a },
                },
            };
    }
}
