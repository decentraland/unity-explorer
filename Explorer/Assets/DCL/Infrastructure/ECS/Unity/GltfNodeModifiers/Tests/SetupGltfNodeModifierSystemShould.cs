using Arch.Core;
using DCL.ECSComponents;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GltfNodeModifiers.Components;
using ECS.Unity.GltfNodeModifiers.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;

namespace ECS.Unity.GltfNodeModifiers.Tests
{
    public class SetupGltfNodeModifierSystemShould : UnitySystemTestBase<SetupGltfNodeModifierSystem>
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
            system = new SetupGltfNodeModifierSystem(world);

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
                RootGameObject = rootGameObject
            };
        }

        [Test]
        public void SetupGlobalModifier_SingleModifierWithEmptyPath()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = { new PBGltfNodeModifiers.Types.GltfNodeModifier
                {
                    Path = "",
                    Material = new PBMaterial
                {
                    Pbr = new PBMaterial.Types.PbrMaterial
                    {
                        AlbedoColor = new Decentraland.Common.Color4 { R = 1f, G = 0f, B = 0f, A = 1f }
                    }
                },
                    OverrideShadows = true
                }}
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.True);
            Assert.That(world.Has<GltfNode>(entity), Is.True);
            Assert.That(world.Has<PBMaterial>(entity), Is.True);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.OriginalMaterials, Is.Not.Null);
            Assert.That(updatedContainer.OriginalMaterials.Count, Is.EqualTo(2));
            Assert.That(updatedContainer.GltfNodeEntities, Is.Not.Null);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));
            Assert.That(updatedContainer.GltfNodeEntities[0], Is.EqualTo(entity));

            var gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2));
            Assert.That(gltfNode.ContainerEntity, Is.EqualTo(entity));
            Assert.That(gltfNode.Path, Is.EqualTo(string.Empty));
        }

        [Test]
        public void SetupIndividualModifiers_MultipleModifiersWithPaths()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = new PBMaterial
                        {
                            Pbr = new PBMaterial.Types.PbrMaterial
                            {
                                AlbedoColor = new Decentraland.Common.Color4 { R = 0f, G = 1f, B = 0f, A = 1f }
                            }
                        }
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.True);
            Assert.That(world.Has<GltfNode>(entity), Is.False);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities, Is.Not.Null);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));

            var childNodeEntity = updatedContainer.GltfNodeEntities[0];
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);

            var gltfNode = world.Get<GltfNode>(childNodeEntity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(1));
            Assert.That(gltfNode.Renderers[0], Is.EqualTo(childRenderer));
            Assert.That(gltfNode.ContainerEntity, Is.EqualTo(entity));
            Assert.That(gltfNode.Path, Is.EqualTo("Child"));
        }

        [Test]
        public void StoreOriginalMaterials_OnlyOnce()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                                 Modifiers = {
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "",
                         Material = CreatePbrMaterial(Color.red)
                     }
                 }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act - First update
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.OriginalMaterials[rootRenderer], Is.EqualTo(originalRootMaterial));
            Assert.That(updatedContainer.OriginalMaterials[childRenderer], Is.EqualTo(originalChildMaterial));

            // Change renderer materials
            rootRenderer.sharedMaterial = testMaterial;
            childRenderer.sharedMaterial = testMaterial;

            // Act - Second update (should not re-store materials)
            var secondModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        Material = CreateUnlitMaterial(Color.blue)
                    }
                }
            };

            world.Set(entity, secondModifiers);
            system.Update(0);

            // Assert - Original materials should still be the same
            updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.OriginalMaterials[rootRenderer], Is.EqualTo(originalRootMaterial));
            Assert.That(updatedContainer.OriginalMaterials[childRenderer], Is.EqualTo(originalChildMaterial));
        }

        [Test]
        public void ApplyShadowOverrides_ToRenderers()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers { Modifiers =
            {
                new PBGltfNodeModifiers.Types.GltfNodeModifier
                {
                    Path = "Child",
                    OverrideShadows = false
                }
            } };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void IgnoreModifiersWithEmptyPathInIndividualMode()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                                         new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "Child",
                         Material = CreatePbrMaterial(Color.green)
                     },
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "", // This should be ignored in individual mode
                         Material = CreateUnlitMaterial(Color.blue)
                     }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1)); // Only one entity created
            Assert.That(world.Has<GltfNode>(entity), Is.False);
        }

        [Test]
        public void CreateEntityEvenWithoutOverrides()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child"
                        // No material or shadow overrides
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1)); // Entity should be created even without overrides

            var childNodeEntity = updatedContainer.GltfNodeEntities[0];
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.False); // No material should be added

            // Shadow should default to On since no override was specified
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void IgnoreModifiersWithInvalidPath()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "NonExistentPath",
                        Material = CreatePbrMaterial(Color.red)
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0)); // No entities should be created for invalid path
        }

        [Test]
        public void HandleShadowOnlyModifier()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        OverrideShadows = false
                        // No material override
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));

            var childNodeEntity = updatedContainer.GltfNodeEntities[0];
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.False); // No material should be added
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void HandleDefaultShadowBehavior()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red)
                        // No shadow override - should default to casting shadows
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));

            var childNodeEntity = updatedContainer.GltfNodeEntities[0];
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);
            // Should default to casting shadows when no override specified
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void SkipUpdateWhenContainerNotFinished()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            gltfContainer.State = LoadingState.Loading; // Not finished

            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red)
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.False); // Should not be added
        }

        [Test]
        public void HandleGlobalModifierShadowOverride()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "",
                        OverrideShadows = false // Turn off shadows globally
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            // Both renderers should have shadows turned off
            Assert.That(rootRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));

            var gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2)); // Should include all renderers
        }

        [Test]
        public void HandleGlobalModifierWithoutOverrides()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = ""
                        // No material or shadow overrides - should still create global node
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            Assert.That(world.Has<Components.GltfNodeModifiers>(entity), Is.True);
            Assert.That(world.Has<GltfNode>(entity), Is.True);
            Assert.That(world.Has<PBMaterial>(entity), Is.False); // No material should be added

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities, Is.Not.Null);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));
            Assert.That(updatedContainer.GltfNodeEntities[0], Is.EqualTo(entity));

            var gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2)); // Should include all renderers

            // Should default to casting shadows when no override specified
            Assert.That(rootRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
            Assert.That(childRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.On));
        }

        [Test]
        public void HandleMultipleModifiersForDifferentPaths()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red)
                    },
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "", // Root
                        OverrideShadows = false
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1)); // Only Child should be created

            var childNodeEntity = updatedContainer.GltfNodeEntities[0];
            var gltfNode = world.Get<GltfNode>(childNodeEntity);
            Assert.That(gltfNode.Path, Is.EqualTo("Child"));
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);
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

        private static PBMaterial CreateUnlitMaterial(Color color)
        {
            return new PBMaterial
            {
                Unlit = new PBMaterial.Types.UnlitMaterial
                {
                    DiffuseColor = new Decentraland.Common.Color4 { R = color.r, G = color.g, B = color.b, A = color.a }
                }
            };
        }
    }
}
