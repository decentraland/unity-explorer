using Arch.Core;
using ECS.TestSuite;
using ECS.Unity.Materials.Components;
using ECS.Unity.Materials.Systems;
using ECS.Unity.Textures.Components;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using Utility.Primitives;

namespace ECS.Unity.Materials.Tests
{
    public class LoadMaterialFromCacheSystemShould : UnitySystemTestBase<LoadMaterialFromCacheSystem>
    {
        private IMaterialsCache materialsCache;


        public void SetUp()
        {
            system = new LoadMaterialFromCacheSystem(world, materialsCache = Substitute.For<IMaterialsCache>());
        }


        public void FinishLoadingIfPresentInCache()
        {
            var materialComponent = new MaterialComponent(MaterialData.CreateBasicMaterial(
                new TextureComponent("test-texture", TextureWrapMode.Mirror, FilterMode.Bilinear),
                0.5f,
                Color.red,
                true));

            materialComponent.Status = StreamableLoading.LifeCycle.LoadingNotStarted;

            materialsCache.TryReferenceMaterial(in materialComponent.Data, out Arg.Any<Material>())
                          .Returns(c =>
                           {
                               c[1] = DefaultMaterial.New();
                               return true;
                           });

            Entity e = world.Create(in materialComponent);

            system.Update(0);

            materialsCache.Received(1).TryReferenceMaterial(in materialComponent.Data, out Arg.Any<Material>());

            materialComponent = world.Get<MaterialComponent>(e);

            Assert.That(materialComponent.Status, Is.EqualTo(StreamableLoading.LifeCycle.LoadingFinished));
            Assert.That(materialComponent.Result, Is.Not.Null);
        }


        public void DoNothingIfLoadingStarted([Values(StreamableLoading.LifeCycle.LoadingInProgress, StreamableLoading.LifeCycle.LoadingFinished, StreamableLoading.LifeCycle.Applied)] StreamableLoading.LifeCycle status)
        {
            var materialComponent = new MaterialComponent(MaterialData.CreateBasicMaterial(
                new TextureComponent("test-texture", TextureWrapMode.Mirror, FilterMode.Bilinear),
                0.5f,
                Color.red,
                true));

            materialComponent.Status = status;

            materialsCache.TryReferenceMaterial(in materialComponent.Data, out Arg.Any<Material>())
                          .Returns(c =>
                           {
                               c[1] = DefaultMaterial.New();
                               return true;
                           });

            Entity e = world.Create(in materialComponent);

            system.Update(0);

            materialsCache.DidNotReceive().TryReferenceMaterial(in materialComponent.Data, out Arg.Any<Material>());

            materialComponent = world.Get<MaterialComponent>(e);

            Assert.That(materialComponent.Status, Is.EqualTo(status));
            Assert.That(materialComponent.Result, Is.Null);
        }


        public void NotFinishLoadingIfNotPresentInCache()
        {
            var materialComponent = new MaterialComponent(MaterialData.CreatePBRMaterial(
                new TextureComponent("1", TextureWrapMode.Mirror, FilterMode.Bilinear),
                new TextureComponent("2", TextureWrapMode.MirrorOnce, FilterMode.Trilinear),
                new TextureComponent("3", TextureWrapMode.Repeat, FilterMode.Point),
                new TextureComponent("4", TextureWrapMode.Clamp, FilterMode.Point),
                0.5f,
                true,
                Color.red,
                Color.blue,
                Color.green,
                MaterialTransparencyMode.AlphaBlend,
                0,
                0,
                0,
                0,
                0
            ));

            materialComponent.Status = StreamableLoading.LifeCycle.LoadingNotStarted;

            materialsCache.TryReferenceMaterial(in materialComponent.Data, out Arg.Any<Material>())
                          .Returns(c =>
                           {
                               c[1] = null;
                               return false;
                           });

            Entity e = world.Create(in materialComponent);

            system.Update(0);

            materialsCache.Received(1).TryReferenceMaterial(in materialComponent.Data, out Arg.Any<Material>());

            materialComponent = world.Get<MaterialComponent>(e);

            Assert.That(materialComponent.Status, Is.EqualTo(StreamableLoading.LifeCycle.LoadingNotStarted));
            Assert.That(materialComponent.Result, Is.Null);
        }
    }
}
