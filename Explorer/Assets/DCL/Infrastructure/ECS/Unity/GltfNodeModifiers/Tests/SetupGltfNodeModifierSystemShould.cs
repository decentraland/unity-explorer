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
using System.Linq;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.TestTools;
using Utility.Primitives;
using Entity = Arch.Core.Entity;

namespace ECS.Unity.GltfNodeModifiers.Tests
{
    public class SetupGltfNodeModifierSystemShould : UnitySystemTestBase<SetupGltfNodeModifierSystem>
    {
        private GameObject rootContainerGameObject;
        private GameObject rootGameObject;
        private GameObject childGameObject;
        private MeshRenderer rootRenderer;
        private MeshRenderer childRenderer;
        private Material originalRootMaterial;
        private Material originalChildMaterial;
        private Material testMaterial;
        private readonly List<GameObject> extraRoots = new ();

        [SetUp]
        public void SetUp()
        {
            system = new SetupGltfNodeModifierSystem(world);

            // Create test GameObjects with renderers
            rootContainerGameObject = new GameObject();
            rootGameObject = new GameObject("Root");
            childGameObject = new GameObject("Child");
            rootGameObject.transform.SetParent(rootContainerGameObject.transform);
            childGameObject.transform.SetParent(rootGameObject.transform);

            rootRenderer = rootContainerGameObject.AddComponent<MeshRenderer>();
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
            if (rootContainerGameObject != null)
                Object.DestroyImmediate(rootContainerGameObject);

            if (originalRootMaterial != null)
                Object.DestroyImmediate(originalRootMaterial);

            if (originalChildMaterial != null)
                Object.DestroyImmediate(originalChildMaterial);

            if (testMaterial != null)
                Object.DestroyImmediate(testMaterial);

            foreach (GameObject extraRoot in extraRoots)
                if (extraRoot != null)
                    Object.DestroyImmediate(extraRoot);

            extraRoots.Clear();
        }

        private GltfContainerComponent CreateGltfContainer()
        {
            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                world,
                new GetGltfContainerAssetIntention("test", "test_hash", new CancellationTokenSource()),
                PartitionComponent.TOP_PRIORITY);

            var asset = GltfContainerAsset.Create(rootContainerGameObject, null);
            asset.Renderers.Add(rootRenderer);
            asset.Renderers.Add(childRenderer);

            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            // To enable its 'Result' property, same outcome as if FinalizeGltfContainerLoadingSystem had ran
            promise.TryConsume(world, out var result);

            return new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished,
                RootGameObject = rootContainerGameObject,
            };
        }

        [Test]
        public void SetupGlobalModifier_SingleModifierWithEmptyPath()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        Material = new PBMaterial
                        {
                            Pbr = new PBMaterial.Types.PbrMaterial
                            {
                                AlbedoColor = new Color4 { R = 1f, G = 0f, B = 0f, A = 1f },
                            },
                        },
                        CastShadows = true,
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.True);
            Assert.That(world.Has<GltfNode>(entity), Is.True);
            Assert.That(world.Has<PBMaterial>(entity), Is.True);

            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.OriginalMaterials, Is.Not.Null);
            Assert.That(nodeModifiers.OriginalMaterials.Count, Is.EqualTo(2));
            Assert.That(nodeModifiers.GltfNodeEntities, Is.Not.Null);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));
            Assert.That(nodeModifiers.GltfNodeEntities.ContainsKey(entity), Is.True);

            GltfNode gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2));
            Assert.That(gltfNode.ContainerEntity, Is.EqualTo(entity));
            Assert.That(gltfNode.Path, Is.EqualTo(string.Empty));
        }

        [Test]
        public void SetupIndividualModifiers_MultipleModifiersWithPaths()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = new PBMaterial
                        {
                            Pbr = new PBMaterial.Types.PbrMaterial
                            {
                                AlbedoColor = new Color4 { R = 0f, G = 1f, B = 0f, A = 1f },
                            },
                        },
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.True);
            Assert.That(world.Has<GltfNode>(entity), Is.False);

            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities, Is.Not.Null);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));

            Entity childNodeEntity = nodeModifiers.GltfNodeEntities.Keys.First();
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);

            GltfNode gltfNode = world.Get<GltfNode>(childNodeEntity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(1));
            Assert.That(gltfNode.Renderers[0], Is.EqualTo(childRenderer));
            Assert.That(gltfNode.ContainerEntity, Is.EqualTo(entity));
            Assert.That(gltfNode.Path, Is.EqualTo("Child"));
        }

        [Test]
        public void StoreOriginalMaterials_OnlyOnce()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act - First update
            system.Update(0);

            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.OriginalMaterials[rootRenderer], Is.EqualTo(originalRootMaterial));
            Assert.That(nodeModifiers.OriginalMaterials[childRenderer], Is.EqualTo(originalChildMaterial));

            // Change renderer materials
            rootRenderer.sharedMaterial = testMaterial;
            childRenderer.sharedMaterial = testMaterial;

            // Act - Second update (should not re-store materials)
            var secondModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        Material = CreateUnlitMaterial(Color.blue),
                    },
                },
            };

            world.Set(entity, secondModifiers);
            system.Update(0);

            // Assert - Original materials should still be the same
            nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.OriginalMaterials[rootRenderer], Is.EqualTo(originalRootMaterial));
            Assert.That(nodeModifiers.OriginalMaterials[childRenderer], Is.EqualTo(originalChildMaterial));
        }

        [Test]
        public void ApplyShadowOverrides_ToRenderers()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        CastShadows = false,
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void IgnoreModifiersWithEmptyPathInIndividualMode()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.green),
                    },
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "", // This should be ignored in individual mode
                        Material = CreateUnlitMaterial(Color.blue),
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1)); // Only one entity created
            Assert.That(world.Has<GltfNode>(entity), Is.False);
        }

        [Test]
        public void CreateEntityEvenWithoutOverrides()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",

                        // No material or shadow overrides
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1)); // Entity should be created even without overrides

            Entity childNodeEntity = nodeModifiers.GltfNodeEntities.Keys.First();
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.False); // No material should be added

            // Shadow should default to On since no override was specified
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void IgnoreModifiersWithInvalidPath()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "NonExistentPath",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Expect error log for invalid path
            LogAssert.Expect(LogType.Error, "GLTF Node path 'NonExistentPath' not found.");

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(0)); // No entities should be created for invalid path
        }

        [Test]
        public void HandleShadowOnlyModifier()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        CastShadows = false,

                        // No material override
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));

            Entity childNodeEntity = nodeModifiers.GltfNodeEntities.Keys.First();
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.False); // No material should be added
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void HandleDefaultShadowBehavior()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),

                        // No shadow override - should default to casting shadows
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));

            Entity childNodeEntity = nodeModifiers.GltfNodeEntities.Keys.First();
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);

            // Should default to casting shadows when no override specified
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void SkipUpdateWhenContainerNotFinished()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();
            gltfContainer.State = LoadingState.Loading; // Not finished

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should not be added
        }

        [Test]
        public void HandleGlobalModifierShadowOverride()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        CastShadows = false, // Turn off shadows globally
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            // Both renderers should have shadows turned off
            Assert.That(rootRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));

            GltfNode gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2)); // Should include all renderers
        }

        [Test]
        public void HandleGlobalModifierWithoutOverrides()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",

                        // No material or shadow overrides - should still create global node
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.True);
            Assert.That(world.Has<GltfNode>(entity), Is.True);
            Assert.That(world.Has<PBMaterial>(entity), Is.False); // No material should be added

            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities, Is.Not.Null);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));
            Assert.That(nodeModifiers.GltfNodeEntities.ContainsKey(entity), Is.True);

            GltfNode gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2)); // Should include all renderers

            // Should default to casting shadows when no override specified
            Assert.That(rootRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void HandleMultipleModifiersForDifferentPaths()
        {
            // Arrange
            GltfContainerComponent gltfContainer = CreateGltfContainer();

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red),
                    },
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "", // Root
                        CastShadows = false,
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1)); // Only Child should be created

            Entity childNodeEntity = nodeModifiers.GltfNodeEntities.Keys.First();
            GltfNode gltfNode = world.Get<GltfNode>(childNodeEntity);
            Assert.That(gltfNode.Path, Is.EqualTo("Child"));
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);
        }

        [Test]
        public void IgnoreModifiersWithInvalidPath_LocalDevelopment_ShowsAvailablePaths()
        {
            // Arrange - Create container with hierarchy paths (simulating local development mode)
            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                world,
                new GetGltfContainerAssetIntention("test", "test_hash", new CancellationTokenSource()),
                PartitionComponent.TOP_PRIORITY);

            var hierarchyPaths = new[] { "Child", "Root" }; // Simulate captured hierarchy paths
            var asset = GltfContainerAsset.Create(rootContainerGameObject, null, hierarchyPaths: hierarchyPaths);
            asset.Renderers.Add(rootRenderer);
            asset.Renderers.Add(childRenderer);

            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            // To enable its 'Result' property, same outcome as if FinalizeGltfContainerLoadingSystem had ran
            promise.TryConsume(world, out var result);

            var gltfContainer = new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished,
                RootGameObject = rootContainerGameObject,
            };

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "InvalidPath",
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Expect error logs for invalid path (both error messages)
            LogAssert.Expect(LogType.Error, "GLTF Node path 'InvalidPath' not found.");
            LogAssert.Expect(LogType.Error, "GLTF Node available paths with renderers:\n  - Child\n  - Root");

            // Act
            system.Update(0);

            // Assert
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(0)); // No entities should be created for invalid path
        }

        [Test]
        public void ResolveShortPath_WhenSceneWrapperPresent()
        {
            // Mirrors Pride_LampostBig.glb: multiple root nodes force glTFast to insert a "Scene" wrapper,
            // so GetChild(0) is the wrapper and the lamp sits one level deeper than in the single-root variant.
            (GameObject container, MeshRenderer lamp) = BuildLamppostWithWrapper();
            GltfContainerComponent gltfContainer = CreateContainerComponent(container, lamp);

            // The path authored for the un-wrapped model must keep working.
            Entity entity = SetupWithPath(gltfContainer, "LampostBig_Lamp");

            AssertResolvedTo(entity, lamp, "LampostBig_Lamp");
        }

        [Test]
        public void ResolveExactLongPath_WhenSceneWrapperPresent()
        {
            (GameObject container, MeshRenderer lamp) = BuildLamppostWithWrapper();
            GltfContainerComponent gltfContainer = CreateContainerComponent(container, lamp);

            // The full path (as Explorer reports it for the wrapped model) resolves via the exact match.
            Entity entity = SetupWithPath(gltfContainer, "LampostBig/LampostBig_Lamp");

            AssertResolvedTo(entity, lamp, "LampostBig/LampostBig_Lamp");
        }

        [Test]
        public void ResolveWrapperPrefixedPath_WhenNoSceneWrapper()
        {
            // Mirrors LampostBig.glb: single root node, no wrapper, GetChild(0) is "LampostBig" itself.
            (GameObject container, MeshRenderer lamp) = BuildLamppostWithoutWrapper();
            GltfContainerComponent gltfContainer = CreateContainerComponent(container, lamp);

            // A path copied from the wrapped variant must still resolve by dropping the leading root segment.
            Entity entity = SetupWithPath(gltfContainer, "LampostBig/LampostBig_Lamp");

            AssertResolvedTo(entity, lamp, "LampostBig/LampostBig_Lamp");
        }

        [Test]
        public void ResolveAmbiguousShortPath_PicksFirstInHierarchyOrderAndWarns()
        {
            // Two root nodes each contain a "Lamp" child (legal in glTF: names are unique only among siblings).
            var container = new GameObject("container");
            extraRoots.Add(container);

            var wrapper = new GameObject("Scene");
            wrapper.transform.SetParent(container.transform);

            MeshRenderer first = AddNode(wrapper.transform, "LampostBig", "Lamp");
            MeshRenderer second = AddNode(wrapper.transform, "LampostSmall", "Lamp");

            GltfContainerComponent gltfContainer = CreateContainerComponent(container, first, second);

            LogAssert.Expect(LogType.Warning,
                "GLTF Node path 'Lamp' is ambiguous: 2 renderers matched it under different root nodes. Using the first one in hierarchy order.");

            Entity entity = SetupWithPath(gltfContainer, "Lamp");

            // First in sibling order wins, deterministically.
            AssertResolvedTo(entity, first, "Lamp");
        }

        /// <summary>
        ///     container → "Scene" (wrapper) → "LampostBig" → "LampostBig_Lamp" (+ a sibling root node).
        /// </summary>
        private (GameObject container, MeshRenderer lamp) BuildLamppostWithWrapper()
        {
            var container = new GameObject("container");
            extraRoots.Add(container);

            var wrapper = new GameObject("Scene");
            wrapper.transform.SetParent(container.transform);

            MeshRenderer lamp = AddNode(wrapper.transform, "LampostBig", "LampostBig_Lamp");

            // Extra root-level node: this is what tips glTFast into creating the wrapper.
            var prop = new GameObject("Plane.016");
            prop.transform.SetParent(wrapper.transform);

            return (container, lamp);
        }

        /// <summary>
        ///     container → "LampostBig" → "LampostBig_Lamp".
        /// </summary>
        private (GameObject container, MeshRenderer lamp) BuildLamppostWithoutWrapper()
        {
            var container = new GameObject("container");
            extraRoots.Add(container);

            MeshRenderer lamp = AddNode(container.transform, "LampostBig", "LampostBig_Lamp");
            return (container, lamp);
        }

        private static MeshRenderer AddNode(Transform parent, string rootNodeName, string leafName)
        {
            var rootNode = new GameObject(rootNodeName);
            rootNode.transform.SetParent(parent);

            var leaf = new GameObject(leafName);
            leaf.transform.SetParent(rootNode.transform);

            return leaf.AddComponent<MeshRenderer>();
        }

        private GltfContainerComponent CreateContainerComponent(GameObject container, params Renderer[] renderers)
        {
            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(
                world,
                new GetGltfContainerAssetIntention("test", "test_hash", new CancellationTokenSource()),
                PartitionComponent.TOP_PRIORITY);

            var asset = GltfContainerAsset.Create(container, null);

            foreach (Renderer renderer in renderers)
                asset.Renderers.Add(renderer);

            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));
            promise.TryConsume(world, out _);

            return new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished,
                RootGameObject = container,
            };
        }

        private Entity SetupWithPath(GltfContainerComponent gltfContainer, string path)
        {
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers =
                {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = path,
                        Material = CreatePbrMaterial(Color.red),
                    },
                },
            };

            Entity entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);
            return entity;
        }

        private void AssertResolvedTo(Entity entity, Renderer expectedRenderer, string expectedPath)
        {
            Components.GltfNodeModifiers nodeModifiers = world.Get<Components.GltfNodeModifiers>(entity);
            Assert.That(nodeModifiers.GltfNodeEntities.Count, Is.EqualTo(1));

            Entity nodeEntity = nodeModifiers.GltfNodeEntities.Keys.First();
            GltfNode gltfNode = world.Get<GltfNode>(nodeEntity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(1));
            Assert.That(gltfNode.Renderers[0], Is.EqualTo(expectedRenderer));
            Assert.That(gltfNode.Path, Is.EqualTo(expectedPath));
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
