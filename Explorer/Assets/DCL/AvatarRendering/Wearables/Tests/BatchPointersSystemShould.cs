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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class BatchPointersSystemShould : UnitySystemTestBase<BatchWearablesDTOSystem>
    {
        private static readonly QueryDescription ROOT_QUERY = new QueryDescription().WithAll<AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>>();
        private static readonly QueryDescription INTENTION_QUERY = new QueryDescription().WithAll<GetWearableDTOByPointersIntention>();

        [SetUp]
        public void SetUp()
        {
            IDecentralandUrlsSource? urls = Substitute.For<IDecentralandUrlsSource>();
            urls.Url(DecentralandUrl.EntitiesActive).Returns("/entities/active");

            system = new BatchWearablesDTOSystem(world, urls, TimeSpan.FromSeconds(2));
        }

        [Test]
        public async Task DispatchTheBatchAsync()
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
                    new GetWearableDTOByPointersIntention(urns, new CommonLoadingArguments(URLAddress.FromString("test"))), PartitionComponent.TOP_PRIORITY), (IPartitionComponent)PartitionComponent.TOP_PRIORITY);
            }

            await Task.Delay(TimeSpan.FromSeconds(3));

            system!.Update(0);

            // One promise to replace individual promises

            int entitiesCount = world.CountEntities(ROOT_QUERY);
            Assert.That(entitiesCount, Is.EqualTo(1));

            var entities = new Entity[1];
            world.GetEntities(ROOT_QUERY, entities);

            AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention> rootPromise = world.Get<AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>>(entities[0]);
            var expectedUrns = Enumerable.Range(0, 15).Select(i => new URN(i.ToString())).ToList();

            Assert.That(rootPromise.LoadingIntention.Pointers, Is.EquivalentTo(expectedUrns));

            entitiesCount = world.CountEntities(INTENTION_QUERY);
            Assert.That(entitiesCount, Is.EqualTo(1));

            world.GetEntities(INTENTION_QUERY, entities);
            GetWearableDTOByPointersIntention intention = world.Get<GetWearableDTOByPointersIntention>(entities[0]);

            Assert.That(intention.Pointers, Is.EquivalentTo(expectedUrns));
        }

        [Test]
        public void CancelAndDestroyOriginalPromises()
        {
            system!.Update(0);

            var count = 0;

            var cts = new List<CancellationTokenSource>();

            for (var i = 1; i < 4; i++)
            {
                var urns = new List<URN>();

                for (var j = 0; j < 5; j++)
                {
                    urns.Add(new URN(count.ToString()));

                    count++;
                }

                var intention = new GetWearableDTOByPointersIntention(urns, new CommonLoadingArguments(URLAddress.FromString("test")));

                cts.Add(intention.CancellationTokenSource);

                world.Create(AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(world,
                    intention, PartitionComponent.TOP_PRIORITY), (IPartitionComponent)PartitionComponent.TOP_PRIORITY);
            }

            system!.Update(0);

            Assert.That(cts.All(c => c.IsCancellationRequested), Is.True);

            Assert.That(world.CountEntities(ROOT_QUERY), Is.EqualTo(0));
            Assert.That(world.CountEntities(INTENTION_QUERY), Is.EqualTo(0));
        }
    }
}
