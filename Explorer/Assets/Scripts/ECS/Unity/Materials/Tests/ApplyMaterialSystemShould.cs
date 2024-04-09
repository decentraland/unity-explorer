using Arch.Core;
using DCL.ECSComponents;
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

namespace ECS.Unity.Materials.Tests
{
    public class ApplyMaterialSystemShould : UnitySystemTestBase<ApplyMaterialSystem>
    {

        public void SetUp()
        {
            system = new ApplyMaterialSystem(world, Substitute.For<ISceneData>());
        }


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
    }
}
