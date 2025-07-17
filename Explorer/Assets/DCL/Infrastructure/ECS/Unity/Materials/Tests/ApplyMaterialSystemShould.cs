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

        /*[Test]
        public void ApplyMaterialToGltfContainer()
        {
            // Arrange
            var originalMaterial = new Material(DefaultMaterial.Get());
            var newMaterial = new Material(DefaultMaterial.Get());
            var meshRenderer = new GameObject("TestRenderer").AddComponent<MeshRenderer>();
            meshRenderer.sharedMaterial = originalMaterial;

            var asset = GltfContainerAsset.Create(new GameObject(), null);
            asset.Renderers.Add(meshRenderer);

            var promise = AssetPromise<GltfContainerAsset, GetGltfContainerAssetIntention>.Create(world, new GetGltfContainerAssetIntention(), new PartitionComponent());
            world.Add(promise.Entity, new StreamableLoadingResult<GltfContainerAsset>(asset));

            var gltfContainerComponent = new GltfContainerComponent
            {
                Promise = promise,
                State = LoadingState.Finished
            };

            var materialComponent = new MaterialComponent
            {
                Result = newMaterial,
                Status = StreamableLoading.LifeCycle.LoadingFinished
            };

            var entity = world.Create(gltfContainerComponent, materialComponent, new PBMaterial());

            // Act
            system.Update(0);

            // Assert
            var updatedGltfContainer = world.Get<GltfContainerComponent>(entity);
            Assert.AreEqual(newMaterial, meshRenderer.sharedMaterial);
            Assert.IsNotNull(updatedGltfContainer.OriginalMaterials);
            Assert.AreEqual(1, updatedGltfContainer.OriginalMaterials.Count);
            Assert.AreEqual(originalMaterial, updatedGltfContainer.OriginalMaterials[0].material);
            Assert.AreEqual(StreamableLoading.LifeCycle.Applied, world.Get<MaterialComponent>(entity).Status);
        }*/
    }
}
