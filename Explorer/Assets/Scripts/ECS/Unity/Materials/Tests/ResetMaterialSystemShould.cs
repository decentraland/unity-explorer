using Arch.Core;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.PrimitiveRenderer.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Rendering;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class ResetMaterialSystemShould : UnitySystemTestBase<ResetMaterialSystem>
    {
        private IMaterialsCache materialsCache;

        private Entity entity;
        private MeshRenderer renderer;

        [SetUp]
        public void SetUp()
        {
            materialsCache = Substitute.For<IMaterialsCache>();
            system = new ResetMaterialSystem(world, materialsCache);

            renderer = new GameObject(nameof(ResetMaterialSystemShould)).AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.sharedMaterial = new Material(Shader.Find("DCL/Universal Render Pipeline/Lit"));

            var matComp = new MaterialComponent { Status = MaterialComponent.LifeCycle.MaterialApplied, Result = DefaultMaterial.Shared };
            var rendComp = new PrimitiveMeshRendererComponent { MeshRenderer = renderer };

            entity = world.Create(matComp, rendComp);
        }

        [Test]
        public void SetDefaultMaterial()
        {
            system.Update(0);

            Assert.That(renderer.sharedMaterial, Is.EqualTo(DefaultMaterial.Shared));
        }

        [Test]
        public void DereferenceMaterial()
        {
            system.Update(0);

            materialsCache.Received(1).Dereference(Arg.Any<MaterialData>());
        }

        [Test]
        public void DeleteComponent()
        {
            system.Update(0);

            Assert.That(world.Has<PrimitiveMeshRendererComponent>(entity), Is.False);
        }
    }
}
