using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Wearables.Tests
{
    public class WearableMetadataRequestShould
    {
        [TestCase(false, true)]
        [TestCase(true, false)]
        [TestCase(true, true)]
        public async Task ReleaseTheRequestAndItsPooledLists(bool cancel, bool publishResult)
        {
            using var world = World.Create();
            using var page = new CancellationTokenSource();
            var provider = new ECSWearablesProvider(null!, world, new WearableStorage());
            UniTask<IReadOnlyCollection<IWearable>?> request = provider.GetDTOsByPointersAsync(
                new URN[] { "urn:decentraland:off-chain:base-avatars:blue_tshirt" }, BodyShape.FEMALE, page.Token);
            QueryDescription query = new QueryDescription().WithAll<GetWearablesByPointersIntention>();
            var requests = new Entity[1];
            Assert.That(world.CountEntities(query), Is.EqualTo(1));
            world.GetEntities(query, requests);
            Entity entity = requests[0];
            var pointers = world.Get<GetWearablesByPointersIntention>(entity).Pointers;
            var wearable = Substitute.For<IWearable>();
            List<IWearable>? visible = null;
            if (publishResult)
            {
                visible = WearableComponentsUtils.WEARABLES_POOL.Get();
                visible.Add(wearable);
                world.Add(entity, new StreamableLoadingResult<WearablesResolution>(new WearablesResolution(visible)));
            }

            if (cancel) page.Cancel();
            var (wasCanceled, result) = await request.SuppressCancellationThrow();

            Assert.That(wasCanceled, Is.EqualTo(cancel));
            Assert.That(world.CountEntities(query), Is.Zero);
            Assert.That(pointers, Is.Empty);
            if (visible != null) Assert.That(visible, Is.Empty);
            if (!cancel) CollectionAssert.AreEqual(new[] { wearable }, result);
        }
    }
}
