using Arch.Core;
using ECS.LifeCycle.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using NSubstitute;
using NUnit.Framework;
using System.Threading;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class CleanUpMaterialsSystemShould : UnitySystemTestBase<CleanUpMaterialsSystem>
    {
        private DestroyMaterial destroyMaterial;

        private Entity e;
        private Entity texPromise;

        [SetUp]
        public void SetUp()
        {
            system = new CleanUpMaterialsSystem(world, destroyMaterial = Substitute.For<DestroyMaterial>());

            e = world.Create(new MaterialComponent { AlbedoTexPromise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention()) }, new DeleteEntityIntention());
        }

        [Test]
        public void AbortLoadingIntentions()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = MaterialComponent.LifeCycle.LoadingInProgress;

            CancellationToken ct = component.AlbedoTexPromise.LoadingIntention.CommonArguments.CancellationToken;

            system.Update(0);

            Assert.That(ct.IsCancellationRequested, Is.True);
            Assert.That(world.Get<MaterialComponent>(e).AlbedoTexPromise, Is.EqualTo(AssetPromise<Texture2D, GetTextureIntention>.NULL));
        }

        [Test]
        public void Dereference()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = MaterialComponent.LifeCycle.LoadingFinished;
            component.Result = DefaultMaterial.New();

            system.Update(0);

            destroyMaterial.Received(1)(in component.Data, component.Result);
        }
    }
}
