using Arch.Core;
using DCL.ECSComponents;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.Unity.GLTFContainer.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;

namespace ECS.Unity.GLTFContainer.Tests
{
    public class GltfNodeModifierSystemShould : UnitySystemTestBase<GltfNodeModifierSystem>
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
            system = new GltfNodeModifierSystem(world);

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
            Assert.That(world.Has<GltfNodeModifiers>(entity), Is.True);
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
            Assert.That(world.Has<GltfNodeModifiers>(entity), Is.True);
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
        public void UpdateGlobalModifier_TransitionFromIndividualToGlobal()
        {
            // Arrange - Start with individual modifier
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
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var originalChildEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Update to global modifier
             var globalModifiers = new PBGltfNodeModifiers
             {
                 IsDirty = true,
                 Modifiers = {
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "",
                         Material = new PBMaterial
                         {
                             Pbr = new PBMaterial.Types.PbrMaterial
                             {
                                 AlbedoColor = new Decentraland.Common.Color4 { R = 1f, G = 0f, B = 0f, A = 1f }
                             }
                         }
                     }
                 }
             };

            world.Set(entity, globalModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(originalChildEntity), Is.True);
            Assert.That(world.Has<GltfNode>(originalChildEntity), Is.False);
            Assert.That(world.Has<GltfNode>(entity), Is.True); // Container should now have GltfNode

            updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));
            Assert.That(updatedContainer.GltfNodeEntities[0], Is.EqualTo(entity));

            var gltfNode = world.Get<GltfNode>(entity);
            Assert.That(gltfNode.Renderers.Count, Is.EqualTo(2)); // Should have all renderers
        }

        [Test]
        public void UpdateIndividualModifiers_TransitionFromGlobalToIndividual()
        {
            // Arrange - Start with global modifier
             var gltfContainer = CreateGltfContainer();
             var gltfNodeModifiers = new PBGltfNodeModifiers
             {
                 Modifiers = {
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "",
                         Material = new PBMaterial
                         {
                             Pbr = new PBMaterial.Types.PbrMaterial
                             {
                                 AlbedoColor = new Decentraland.Common.Color4 { R = 1f, G = 0f, B = 0f, A = 1f }
                             }
                         }
                     }
                 }
             };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            // Act - Update to individual modifiers
             var individualModifiers = new PBGltfNodeModifiers
             {
                 IsDirty = true,
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

            world.Set(entity, individualModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNode>(entity), Is.False);
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(entity), Is.True);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(1));

            var childNodeEntity = updatedContainer.GltfNodeEntities[0];
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.True);
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);
        }

        [Test]
        public void UpdateExistingGltfNodeEntity_ModifyMaterial()
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
                     }
                 }
             };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var childNodeEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Update with new material
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers = {
                     new PBGltfNodeModifiers.Types.GltfNodeModifier
                     {
                         Path = "Child",
                         Material = CreateUnlitMaterial(Color.blue)
                     }
                }
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<PBMaterial>(childNodeEntity), Is.True);
            var pbMaterial = world.Get<PBMaterial>(childNodeEntity);
             Assert.That(pbMaterial.Unlit, Is.Not.Null);
            Assert.That(pbMaterial.IsDirty, Is.True);
        }

         [Test]
         public void CleanupOrphanedEntities_WhenModifiersRemoved()
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
                     }
                 }
             };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var childNodeEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Remove all modifiers
            var emptyModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers = { } // Empty list
            };

            world.Set(entity, emptyModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childNodeEntity), Is.True);
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.False);
            updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0));
        }

        [Test]
         public void HandleGltfNodeModifiersRemoval_CleanupAllEntities()
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
                     }
                 }
             };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var childNodeEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Remove PBGltfNodeModifiers component
            world.Remove<PBGltfNodeModifiers>(entity);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childNodeEntity), Is.True);
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.False);
            Assert.That(world.Has<GltfNodeModifiers>(entity), Is.False);
            updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0));
        }

        [Test]
         public void HandleGltfNodeModifiersCleanup_WithCleanupIntention()
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
                     }
                 }
             };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var childNodeEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Add cleanup intention
            world.Add(entity, new GltfNodeModifiersCleanupIntention());
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childNodeEntity), Is.True);
            Assert.That(world.Has<GltfNode>(childNodeEntity), Is.False);
            Assert.That(world.Has<GltfNodeModifiersCleanupIntention>(entity), Is.False);
            updatedContainer = world.Get<GltfContainerComponent>(entity);
            Assert.IsTrue(updatedContainer.GltfNodeEntities.Count.Equals(0));
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

        [Test]
        public void IgnoreModifiersWithoutOverrides()
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
            Assert.That(updatedContainer.GltfNodeEntities.Count, Is.EqualTo(0)); // No entities should be created
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
        public void SkipUpdateWhenNotDirty()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                IsDirty = false, // Not dirty
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreatePbrMaterial(Color.red)
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            world.Add(entity, new GltfNodeModifiers()); // Add to simulate existing setup

            // Act
            system.Update(0);

            // Assert
            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            // Should not create any new entities because isDirty is false
            Assert.That(updatedContainer.GltfNodeEntities, Is.Null.Or.Empty);
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
            Assert.That(world.Has<GltfNodeModifiers>(entity), Is.False); // Should not be added
        }

        [Test]
        public void UpdateMaterialFromPbrToUnlit()
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
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var childNodeEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Update to Unlit material
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        Material = CreateUnlitMaterial(Color.blue)
                    }
                }
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            var pbMaterial = world.Get<PBMaterial>(childNodeEntity);
            Assert.That(pbMaterial.Pbr, Is.Null);
            Assert.That(pbMaterial.Unlit, Is.Not.Null);
            Assert.That(pbMaterial.IsDirty, Is.True);
        }

        [Test]
        public void RemoveMaterialFromExistingEntity()
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
                    }
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);
            system.Update(0);

            var updatedContainer = world.Get<GltfContainerComponent>(entity);
            var childNodeEntity = updatedContainer.GltfNodeEntities[0];

            // Act - Update to modifier without material (shadow only)
            var updatedModifiers = new PBGltfNodeModifiers
            {
                IsDirty = true,
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "Child",
                        OverrideShadows = true
                        // No material
                    }
                }
            };

            world.Set(entity, updatedModifiers);
            system.Update(0);

            // Assert
            Assert.That(world.Has<GltfNodeMaterialCleanupIntention>(childNodeEntity), Is.True);

            var cleanupIntention = world.Get<GltfNodeMaterialCleanupIntention>(childNodeEntity);
            Assert.That(cleanupIntention.Destroy, Is.False); // Should not destroy entity, just clean up material
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

        [Test]
        public void HandleCleanupWithNoGltfNodeEntities()
        {
            // Arrange
            var gltfContainer = CreateGltfContainer();
            var gltfNodeModifiers = new PBGltfNodeModifiers
            {
                Modifiers = {
                    new PBGltfNodeModifiers.Types.GltfNodeModifier
                    {
                        Path = "NON-EXISTENT",
                    },
                }
            };

            var entity = world.Create(gltfNodeModifiers, gltfContainer, PartitionComponent.TOP_PRIORITY);

            // Act - First normal update
            system.Update(0);

            gltfContainer = world.Get<GltfContainerComponent>(entity);
            Assert.IsTrue(gltfContainer.GltfNodeEntities.Count.Equals(0));

            // Add cleanup intention after normal operation
            world.Add(entity, new GltfNodeModifiersCleanupIntention());

            // Act - Second update with cleanup intention - Should not throw
            Assert.DoesNotThrow(() => system.Update(0));

            // Assert
            Assert.That(world.Has<GltfNodeModifiersCleanupIntention>(entity), Is.False);
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
    }
}
