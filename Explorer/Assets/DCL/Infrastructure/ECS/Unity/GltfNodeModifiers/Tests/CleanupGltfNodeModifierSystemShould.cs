using DCL.ECSComponents;
using Decentraland.Common;
using ECS.Abstract;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;
using ECS.Unity.GltfNodeModifiers.Systems;
using NUnit.Framework;
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

        [Test]
        public void HandleGltfNodeModifiersRemoval_CleanupAllEntities()
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

            var nodeModifiers = new Components.GltfNodeModifiers(new List<Entity>());
            world.Add(entity, nodeModifiers);
            world.Add(entity, PartitionComponent.TOP_PRIORITY);

            // Manually simulate SetupGltfNodeModifierSystem outcome
            Entity childEntity = world.Create();

            world.Add(childEntity, new GltfNode(new[] { childRenderer }, entity, "Child"));

            world.Add(childEntity, CreatePbrMaterial(Color.red), PartitionComponent.TOP_PRIORITY);

            nodeModifiers.GltfNodeEntities.Add(childEntity);
            world.Set(entity, nodeModifiers);

            // Act - Remove PBGltfNodeModifiers to trigger HandleGltfNodeModifiersRemoval query
            world.Remove<PBGltfNodeModifiers>(entity);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNode>(childEntity), Is.True);
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should be removed by system
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
