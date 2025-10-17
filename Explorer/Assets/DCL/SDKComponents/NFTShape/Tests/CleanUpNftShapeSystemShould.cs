using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.ECSComponents;
using DCL.SDKComponents.NFTShape.Component;
using DCL.SDKComponents.NFTShape.System;
using ECS.LifeCycle.Components;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.NFTShapes;
using ECS.StreamableLoading.Textures;
using ECS.TestSuite;
using NUnit.Framework;
using UnityEngine;
using NftTypePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.NFTShapes.NftTypeResult, ECS.StreamableLoading.NFTShapes.GetNFTTypeIntention>;
using NftImagePromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.NFTShapes.GetNFTImageIntention>;

namespace DCL.SDKComponents.NFTShape.Tests
{
    public class CleanUpNftShapeSystemShould : UnitySystemTestBase<CleanUpNftShapeSystem>
    {
        [SetUp]
        public void Setup()
        {
            system = new CleanUpNftShapeSystem(world);
        }

        [Test]
        public void AbortLoadingWhenPBNftShapeIsDeleted()
        {
            var texData = new TextureData(Texture2D.grayTexture);
            texData.AddReference();

            var typePromise = NftTypePromise.Create(world, new GetNFTTypeIntention(URLAddress.FromString("URN")), PartitionComponent.TOP_PRIORITY);
            var imagePromise = NftImagePromise.Create(world, new GetNFTImageIntention(URLAddress.FromString("URN")),  PartitionComponent.TOP_PRIORITY);

            Entity entity = world.Create(new NFTLoadingComponent("URN", typePromise)
            {
                ImagePromise = imagePromise,
            });

            world.Add(imagePromise.Entity, new StreamableLoadingResult<Texture2DData>(texData));

            system!.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.TypePromise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.TypePromise.Entity, Is.EqualTo(Entity.Null));
            Assert.That(loadingComponent.ImagePromise!.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.ImagePromise!.Value.Entity, Is.EqualTo(Entity.Null));
            Assert.That(texData.referenceCount, Is.EqualTo(0));
        }

        [Test]
        public void AbortLoadingIfEntityDeleted()
        {
            var texData = new TextureData(Texture2D.grayTexture);
            texData.AddReference();

            var typePromise = NftTypePromise.Create(world, new GetNFTTypeIntention(URLAddress.FromString("URN")), PartitionComponent.TOP_PRIORITY);
            var imagePromise = NftImagePromise.Create(world, new GetNFTImageIntention(URLAddress.FromString("URN")),  PartitionComponent.TOP_PRIORITY);

            Entity entity = world.Create(new PBNftShape(), new DeleteEntityIntention(), new NFTLoadingComponent("URN", typePromise)
            {
                ImagePromise = imagePromise,
            });

            world.Add(imagePromise.Entity, new StreamableLoadingResult<Texture2DData>(texData));

            system!.Update(0);

            Assert.That(world.TryGet(entity, out NFTLoadingComponent loadingComponent), Is.True);
            Assert.That(loadingComponent.TypePromise.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.TypePromise.Entity, Is.EqualTo(Entity.Null));
            Assert.That(loadingComponent.ImagePromise!.Value.LoadingIntention.CancellationTokenSource.IsCancellationRequested, Is.True);
            Assert.That(loadingComponent.ImagePromise!.Value.Entity, Is.EqualTo(Entity.Null));
            Assert.That(texData.referenceCount, Is.EqualTo(0));
        }
    }
}
