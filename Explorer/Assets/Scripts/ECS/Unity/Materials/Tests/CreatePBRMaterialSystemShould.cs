using Arch.Core;
using ECS.StreamableLoading.Components;
using ECS.StreamableLoading.Components.Common;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.Pool;

namespace ECS.Unity.Materials.Tests
{
    public class CreatePBRMaterialSystemShould : UnitySystemTestBase<CreatePBRMaterialSystem>
    {
        private const int ATTEMPTS_COUNT = 5;

        private Material pbrMat;

        [SetUp]
        public void SetUp()
        {
            pbrMat = Resources.Load<Material>(CreatePBRMaterialSystem.MATERIAL_PATH);
            IObjectPool<Material> pool = Substitute.For<IObjectPool<Material>>();
            pool.Get().Returns(_ => new Material(pbrMat));

            system = new CreatePBRMaterialSystem(world, pool, ATTEMPTS_COUNT);
            system.Initialize();
        }

        [Test]
        public void StartLoading()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingNotStarted;

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingInProgress));

            AssertTexturePromise(afterUpdate.AlbedoTexPromise, "albedo");
            AssertTexturePromise(afterUpdate.AlphaTexPromise, "alpha");
            AssertTexturePromise(afterUpdate.EmissiveTexPromise, "emissive");
            AssertTexturePromise(afterUpdate.BumpTexPromise, "bump");
        }

        [Test]
        public void ConstructMaterial()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);
            CreateAndFinalizeTexturePromise(ref component.AlphaTexPromise);
            CreateAndFinalizeTexturePromise(ref component.EmissiveTexPromise);
            CreateAndFinalizeTexturePromise(ref component.BumpTexPromise);

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingFinished));

            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.shader, Is.EqualTo(pbrMat.shader));
        }

        [Test]
        public void NotConstructMaterialIfTexturesLoadingNotFinished()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);
            CreateAndFinalizeTexturePromise(ref component.AlphaTexPromise);

            component.BumpTexPromise = world.Reference(world.Create());
            component.EmissiveTexPromise = world.Reference(world.Create());

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingInProgress));

            Assert.That(afterUpdate.Result, Is.Null);
        }

        private void CreateAndFinalizeTexturePromise(ref EntityReference entityReference)
        {
            var result = new StreamableLoadingResult<Texture2D>(Texture2D.grayTexture);
            Entity e = world.Create(result);
            entityReference = world.Reference(e);
        }

        private void AssertTexturePromise(in EntityReference entityReference, string src)
        {
            Assert.AreNotEqual(EntityReference.Null, entityReference);

            Assert.That(world.TryGet(entityReference.Entity, out GetTextureIntention intention), Is.True);
            Assert.That(intention.CommonArguments.URL, Is.EqualTo(src));
            Assert.That(intention.CommonArguments.Attempts, Is.EqualTo(ATTEMPTS_COUNT));
        }

        private static MaterialComponent CreateMaterialComponent() =>
            new (MaterialData.CreatePBRMaterial(
                new TextureComponent("albedo", TextureWrapMode.Mirror, FilterMode.Point),
                new TextureComponent("alpha", TextureWrapMode.Mirror, FilterMode.Trilinear),
                new TextureComponent("emissive", TextureWrapMode.Mirror, FilterMode.Bilinear),
                new TextureComponent("bump", TextureWrapMode.Mirror, FilterMode.Point),
                0.5f,
                true,
                Color.red,
                Color.green,
                Color.blue,
                MaterialTransparencyMode.AlphaBlend,
                0.3f,
                0.4f,
                0.5f,
                0.6f,
                0.7f,
                0
            ));
    }
}
