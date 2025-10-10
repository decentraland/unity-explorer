using Arch.Core;
using CommunicationData.URLHelpers;
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
using UnityEngine;
using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;
using NftVideoPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.NFTShapes.GetNFTVideoIntention>;

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
        public void AbortLoadingWhenPBNftShapeIsDeleted()
        {
            var videoTexture = new Texture2DData(Texture2D.grayTexture);
            videoTexture.AddReference();

            var imageTexture = new Texture2DData(Texture2D.grayTexture);
            imageTexture.AddReference();

            var typePromise = NftTypePromise.Create(world, new GetNFTTypeIntention(URLAddress.FromString("URN")), PartitionComponent.TOP_PRIORITY);
            var videoPromise = NftVideoPromise.Create(world, new GetNFTVideoIntention(URLAddress.FromString("URN")), PartitionComponent.TOP_PRIORITY);
            var imagePromise = NftImagePromise.Create(world, new GetNFTImageIntention(URLAddress.FromString("URN")),  PartitionComponent.TOP_PRIORITY);

            Entity entity = world.Create(new NFTLoadingComponent("URN", typePromise)
            {
                VideoPromise = videoPromise,
                ImagePromise = imagePromise,
            });

            world.Add(videoPromise.Entity, new StreamableLoadingResult<Texture2DData>(videoTexture));
            world.Add(imagePromise.Entity, new StreamableLoadingResult<Texture2DData>(imageTexture));

            system!.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.TypePromise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.TypePromise.Entity, Is.EqualTo(Entity.Null));
            Assert.That(loadingComponent.VideoPromise!.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.VideoPromise!.Value.Entity, Is.EqualTo(Entity.Null));
            Assert.That(loadingComponent.ImagePromise!.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.ImagePromise!.Value.Entity, Is.EqualTo(Entity.Null));

            Assert.That(videoTexture.referenceCount, Is.EqualTo(0));
        }

        [Test]
        public void AbortLoadingIfEntityDeleted()
        {
            var videoTexture = new Texture2DData(Texture2D.grayTexture);
            videoTexture.AddReference();

            var imageTexture = new Texture2DData(Texture2D.grayTexture);
            imageTexture.AddReference();

            var typePromise = NftTypePromise.Create(world, new GetNFTTypeIntention(URLAddress.FromString("URN")), PartitionComponent.TOP_PRIORITY);
            var videoPromise = NftVideoPromise.Create(world, new GetNFTVideoIntention(URLAddress.FromString("URN")), PartitionComponent.TOP_PRIORITY);
            var imagePromise = NftImagePromise.Create(world, new GetNFTImageIntention(URLAddress.FromString("URN")),  PartitionComponent.TOP_PRIORITY);

            Entity entity = world.Create(new PBNftShape(), new DeleteEntityIntention(), new NFTLoadingComponent("URN", typePromise)
            {
                VideoPromise = videoPromise,
                ImagePromise = imagePromise,
            });

            world.Add(videoPromise.Entity, new StreamableLoadingResult<Texture2DData>(videoTexture));
            world.Add(imagePromise.Entity, new StreamableLoadingResult<Texture2DData>(imageTexture));

            system!.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.TypePromise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.TypePromise.Entity, Is.EqualTo(Entity.Null));
            Assert.That(loadingComponent.VideoPromise!.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.VideoPromise!.Value.Entity, Is.EqualTo(Entity.Null));
            Assert.That(loadingComponent.ImagePromise!.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.ImagePromise!.Value.Entity, Is.EqualTo(Entity.Null));

            Assert.That(videoTexture.referenceCount, Is.EqualTo(0));
        }
    }
}
