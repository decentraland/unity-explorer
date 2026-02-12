using Arch.Core;
using CommunicationData.URLHelpers;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.AvatarRendering.Wearables.Systems;
using DCL.Multiplayer.Connections.DecentralandUrls;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.TestSuite;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class BatchPointersSystemShould : UnitySystemTestBase<BatchWearablesDTOSystem>
    {
        [SetUp]
        public void SetUp()
        {
            IDecentralandUrlsSource? urls = Substitute.For<IDecentralandUrlsSource>();
            urls.Url(DecentralandUrl.EntitiesActive).Returns("/entities/active");

            system = new BatchWearablesDTOSystem(world, urls, TimeSpan.FromSeconds(2));
        }

        [Test]
        [TestCase(3, true)]
        [TestCase(0.5f, false)]
        public async Task RespectHeartbeatAsync(float delay, bool created)
        {
            system!.Update(0);

            var count = 0;

            for (var i = 1; i < 4; i++)
            {
                var urns = new List<URN>();

                for (var j = 0; j < 5; j++)
                {
                    urns.Add(new URN(count.ToString()));

                    count++;
                }

                world.Create(AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(world,
                    new GetWearableDTOByPointersIntention(urns, default(CommonLoadingArguments)), PartitionComponent.TOP_PRIORITY), PartitionComponent.TOP_PRIORITY);
            }

            await Task.Delay(TimeSpan.FromSeconds(delay));

            system!.Update(0);

            QueryDescription rootQuery = new QueryDescription().WithAll<AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>>();
            QueryDescription intentionQuery = new QueryDescription().WithAll<GetWearableDTOByPointersIntention>();

            if (created)
            {
                // One promise to replace individual promises

                // Assert.That(world.GetEntities());
            }
        }
    }
}
