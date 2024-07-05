using Arch.Core;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using NUnit.Framework;
using System.Collections.Generic;
using UnityEngine;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

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
            var loadSystem = new LoadTextureSystem(world, new TexturesCache(), IWebRequestController.DEFAULT);
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
            var intention = new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(successPath),
                FilterMode = FilterMode.Bilinear,
                IsReadable = true,
            };

            var partition = PartitionComponent.TOP_PRIORITY;
            return Promise.Create(world, intention, partition);
        }
    }
}
