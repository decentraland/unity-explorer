using Arch.Core;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.System;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.NFTShapes.URNs;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using Plugins.TexturesFuse.TexturesServerWrap.Unzips;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTShapeIntention>;

namespace DCL.SDKComponents.NFTShape.Tests
{
    public class CleanUpNftShapeSystemShould : UnitySystemTestBase<CleanUpNftShapeSystem>
    {
        private IURNSource? urnSource;

        [SetUp]
        public void Setup()
        {
            system = new CleanUpNftShapeSystem(world);
            urnSource = Substitute.For<IURNSource>();
        }

        [Test]
        public void AbortLoadingIfComponentDeleted()
        {
            var texData = new Texture2DData(Texture2D.grayTexture);
            texData.AddReference();

            var promise = Promise.Create(world, new GetNFTShapeIntention("URN", urnSource!), PartitionComponent.TOP_PRIORITY);

            Entity entity = world.Create(new NFTLoadingComponent(promise));
            world.Add(promise.Entity, new StreamableLoadingResult<Texture2DData>(texData));

            system!.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.Promise.Entity, Is.EqualTo(EntityReference.Null));

            Assert.That(texData.referenceCount, Is.EqualTo(0));
        }

        [Test]
        public void AbortLoadingIfEntityDeleted()
        {
            var texData = new Texture2DData(Texture2D.grayTexture);
            texData.AddReference();

            var promise = Promise.Create(world, new GetNFTShapeIntention("URN", urnSource!), PartitionComponent.TOP_PRIORITY);

            Entity entity = world.Create(new PBNftShape(), new DeleteEntityIntention(), new NFTLoadingComponent(promise));

            world.Add(promise.Entity, new StreamableLoadingResult<Texture2DData>(texData));

            system!.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.Promise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.Promise.Entity, Is.EqualTo(EntityReference.Null));

            Assert.That(texData.referenceCount, Is.EqualTo(0));
        }
    }
}
