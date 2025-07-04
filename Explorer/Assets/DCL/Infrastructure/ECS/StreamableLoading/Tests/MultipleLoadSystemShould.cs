using Arch.Core;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Textures;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.Tests
{
    [TestFixture]
    public class MultipleLoadSystemShould
    {
        private string successPath => $"file://{Application.dataPath + "/../TestResources/Images/alphaTexture.png"}";

        private const int REQUESTS_COUNT = 1000;

        [Test]
        public void MultipleLoadsShould()
        {
            // set-up
            var world = World.Create();
            var loadSystem = new LoadTextureSystem(world, new TexturesCache<GetTextureIntention>(), IWebRequestController.DEFAULT, IDiskCache<Texture2DData>.Null.INSTANCE,
                Substitute.For<IAvatarTextureUrlProvider>(), Substitute.For<IDecentralandUrlsSource>(),Substitute.For<ExtendedObjectPool<Texture2D>>());
            var promises = new List<Promise>(REQUESTS_COUNT);
            for (var i = 0; i < REQUESTS_COUNT; i++) promises.Add(NewPromise(world));

            // execute
            // forget all promises except the last one
            for (var i = 0; i < promises.Count - 1; i++)
                promises[i].ForgetLoading(world);

            loadSystem.Update(0);

            // compare
            Assert.AreEqual(1, world.CountEntities(QueryDescription.Null));
        }

        private Promise NewPromise(World world)
        {
            var intention = new GetTextureIntention(successPath, string.Empty, TextureWrapMode.Repeat, FilterMode.Bilinear, TextureType.Albedo);

            var partition = PartitionComponent.TOP_PRIORITY;
            return Promise.Create(world, intention, partition);
        }
    }
}
