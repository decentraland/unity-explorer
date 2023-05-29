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

namespace ECS.Unity.Materials.Tests
{
    public class CreateBasicMaterialSystemShould : UnitySystemTestBase<CreateBasicMaterialSystem>
    {
        private const int ATTEMPTS_COUNT = 5;

        private IMaterialsCache materialsCache;

        [SetUp]
        public void SetUp()
        {
            system = new CreateBasicMaterialSystem(world, materialsCache = Substitute.For<IMaterialsCache>(), ATTEMPTS_COUNT);
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
        }

        [Test]
        public void ConstructMaterial()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CreateAndFinalizeTexturePromise(ref component.AlbedoTexPromise);

            Entity e = world.Create(component);

            system.Update(0);

            MaterialComponent afterUpdate = world.Get<MaterialComponent>(e);
            Assert.That(afterUpdate.Status, Is.EqualTo(MaterialComponent.LifeCycle.LoadingFinished));

            Assert.That(afterUpdate.Result, Is.Not.Null);
            Assert.That(afterUpdate.Result.shader, Is.EqualTo(system.sharedMaterial.shader));

            materialsCache.Received(1).Add(in afterUpdate.Data, afterUpdate.Result);
        }

        [Test]
        public void NotConstructMaterialIfTexturesLoadingNotFinished()
        {
            MaterialComponent component = CreateMaterialComponent();

            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

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
            new (MaterialData.CreateBasicMaterial(
                new TextureComponent("albedo", TextureWrapMode.Mirror, FilterMode.Point),
                0,
                Color.red,
                false));
    }
}
