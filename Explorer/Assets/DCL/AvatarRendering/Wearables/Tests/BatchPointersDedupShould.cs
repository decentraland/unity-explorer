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

namespace DCL.AvatarRendering.Wearables.Tests
{
    /// <summary>
    /// Pins the scene-wide pointer de-duplication in
    /// <see cref="Loading.Systems.Abstract.BatchPointersSystemBase{TIntention,TAsset}"/>.
    /// Two avatars sharing the body-shape URN plus one common wearable URN must contribute each
    /// distinct URN to the batch exactly once. On unmodified dev the shared URNs are appended once
    /// per avatar, so the batch carries duplicates and Count != Distinct().Count().
    /// </summary>
    public class BatchPointersDedupShould : UnitySystemTestBase<BatchWearablesDTOSystem>
    {
        private static readonly QueryDescription INTENTION_QUERY = new QueryDescription().WithAll<GetWearableDTOByPointersIntention>();

        [SetUp]
        public void SetUp()
        {
            IDecentralandUrlsSource urls = Substitute.For<IDecentralandUrlsSource>();
            urls.Url(DecentralandUrl.EntitiesActiveElements).Returns("/entities/active");

            // Zero heartbeat: the second Update gathers and dispatches in one call.
            system = new BatchWearablesDTOSystem(world, urls, TimeSpan.Zero);
        }

        [Test]
        public void DeduplicateSharedPointersAcrossAvatars()
        {
            system.Update(0); // arm nextDispatch (empty batch is disposed)

            var body = new URN("urn:body-shape");
            var common = new URN("urn:common-wearable");

            CreateAvatarPromise(new List<URN> { body, common, new ("urn:a-unique") });
            CreateAvatarPromise(new List<URN> { body, common, new ("urn:b-unique") });

            system.Update(0); // gather + dispatch

            Assert.That(world.CountEntities(INTENTION_QUERY), Is.EqualTo(1));
            var entities = new Entity[1];
            world.GetEntities(INTENTION_QUERY, entities);
            GetWearableDTOByPointersIntention intention = world.Get<GetWearableDTOByPointersIntention>(entities[0]);

            // The shared body-shape + common wearable URNs must not be appended once per avatar.
            Assert.That(intention.Pointers.Count, Is.EqualTo(intention.Pointers.Distinct().Count()));

            // Every distinct URN survives (dedup keeps >= 1 of each): 2 shared + 2 unique.
            Assert.That(intention.Pointers, Is.EquivalentTo(new List<URN>
            {
                body, common, new ("urn:a-unique"), new ("urn:b-unique"),
            }));
        }

        private void CreateAvatarPromise(List<URN> urns)
        {
            world.Create(
                AssetPromise<WearablesDTOList, GetWearableDTOByPointersIntention>.Create(world,
                    new GetWearableDTOByPointersIntention(urns, new CommonLoadingArguments(URLAddress.FromString("test"))),
                    PartitionComponent.TOP_PRIORITY),
                (IPartitionComponent)PartitionComponent.TOP_PRIORITY);
        }
    }
}
