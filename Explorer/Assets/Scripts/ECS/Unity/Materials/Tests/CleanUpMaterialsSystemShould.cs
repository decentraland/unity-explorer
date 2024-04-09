using Arch.Core;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
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


        public void SetUp()
        {
            system = new CleanUpMaterialsSystem(world, destroyMaterial = Substitute.For<DestroyMaterial>());

            e = world.Create(new MaterialComponent(new MaterialData()) { AlbedoTexPromise = AssetPromise<Texture2D, GetTextureIntention>.Create(world, new GetTextureIntention { CommonArguments = new CommonLoadingArguments("url") }, PartitionComponent.TOP_PRIORITY) }, new DeleteEntityIntention());
        }


        public void AbortLoadingIntentions()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = StreamableLoading.LifeCycle.LoadingInProgress;

            CancellationToken ct = component.AlbedoTexPromise.Value.LoadingIntention.CommonArguments.CancellationToken;

            system.Update(0);

            Assert.That(ct.IsCancellationRequested, Is.True);
            Assert.That(world.Get<MaterialComponent>(e).AlbedoTexPromise, Is.EqualTo(null));
        }


        public void Dereference()
        {
            ref MaterialComponent component = ref world.Get<MaterialComponent>(e);
            component.Status = StreamableLoading.LifeCycle.LoadingFinished;
            component.Result = DefaultMaterial.New();

            system.Update(0);

            destroyMaterial.Received(1)(in component.Data, component.Result);
        }
    }
}
