using Arch.Core;
using DCL.ECSComponents;
using ECS.Prioritization.Components;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using NSubstitute;
using NUnit.Framework;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;
using ECS.StreamableLoading.Common.Components;
using ECS.Unity.GLTFContainer.Asset.Components;
using ECS.Unity.GLTFContainer.Components;
using ECS.StreamableLoading.Common;
using ECS.Unity.GltfNodeModifiers.Components;
using System.Collections.Generic;

namespace ECS.Unity.Materials.Tests
{
    public class ApplyMaterialSystemShould : UnitySystemTestBase<ApplyMaterialSystem>
    {
        [SetUp]
        public void SetUp()
        {
            system = new ApplyMaterialSystem(world, Substitute.For<ISceneData>());
        }

        [Test]
        public void ApplyMaterialIfLoadingFinished()
        {
            MeshRenderer renderer = new GameObject(nameof(ApplyMaterialIfLoadingFinished)).AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;

            Material mat = DefaultMaterial.New();

            var matComp = new MaterialComponent { Status = StreamableLoading.LifeCycle.LoadingFinished, Result = mat };

            Entity e = world.Create(new PBMaterial(), matComp, new PBMeshRenderer(), new PrimitiveMeshRendererComponent { MeshRenderer = renderer });

            system.Update(0);

            Assert.That(renderer.sharedMaterial, Is.EqualTo(mat));
            Assert.That(world.Get<MaterialComponent>(e).Status, Is.EqualTo(StreamableLoading.LifeCycle.Applied));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void ApplyMaterialIfRendererIsDirty()
        {
            MeshRenderer renderer = new GameObject(nameof(ApplyMaterialIfLoadingFinished)).AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;

            Material mat = DefaultMaterial.New();

            var matComp = new MaterialComponent { Status = StreamableLoading.LifeCycle.Applied, Result = mat };

            Entity e = world.Create(new PBMaterial(), matComp, new PBMeshRenderer { IsDirty = true }, new PrimitiveMeshRendererComponent { MeshRenderer = renderer });

            system.Update(0);

            Assert.That(renderer.sharedMaterial, Is.EqualTo(mat));
            Assert.That(world.Get<MaterialComponent>(e).Status, Is.EqualTo(StreamableLoading.LifeCycle.Applied));
            Assert.That(renderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off));
        }

        [Test]
        public void ApplyMaterialToGltfNode()
        {
            // Arrange
            var originalMaterial = new Material(DefaultMaterial.Get());
            var newMaterial = new Material(DefaultMaterial.Get());
            var testGameObject = new GameObject("TestRenderer");
            var meshRenderer = testGameObject.AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = originalMaterial;

            var rootGameObject = new GameObject();
            var asset = GltfContainerAsset.Create(rootGameObject, null);
            asset.Renderers.Add(meshRenderer);

            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention(), new PartitionComponent());
            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            // Create container entity
            var containerEntity = world.Create();
            var gltfContainerComponent = new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished,
                OriginalMaterials = new Dictionary<Renderer, Material> { { meshRenderer, originalMaterial } }
            };
            world.Add(containerEntity, gltfContainerComponent);

            // Create GltfNode entity
            var materialComponent = new MaterialComponent
            {
                Result = newMaterial,
                Status = StreamableLoading.LifeCycle.LoadingFinished
            };

            var gltfNode = new GltfNode
            {
                Renderers = new List<Renderer> { meshRenderer },
                ContainerEntity = containerEntity,
                Path = "TestNode"
            };

            var gltfNodeEntity = world.Create(gltfNode, materialComponent, new PBMaterial
            {
                Pbr = new PBMaterial.Types.PbrMaterial
                {
                    AlbedoColor = new Decentraland.Common.Color4 { R = 1f, G = 0f, B = 0f, A = 1f }
                }
            });

            // Act
            system.Update(0);

            // Assert
            Assert.AreEqual(newMaterial, meshRenderer.sharedMaterial);
            Assert.AreEqual(StreamableLoading.LifeCycle.Applied, world.Get<MaterialComponent>(gltfNodeEntity).Status);
            Assert.That(meshRenderer.shadowCastingMode, Is.EqualTo(ShadowCastingMode.Off)); // Default material behavior

            // Cleanup
            Object.DestroyImmediate(testGameObject);
            Object.DestroyImmediate(rootGameObject);
            Object.DestroyImmediate(originalMaterial);
            Object.DestroyImmediate(newMaterial);
        }
    }
}
