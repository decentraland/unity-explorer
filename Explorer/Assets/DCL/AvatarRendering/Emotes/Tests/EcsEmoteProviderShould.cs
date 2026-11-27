using Arch.Core;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class EcsEmoteProviderShould
    {
        [Test]
        public async Task KeepOverlappingOwnedPagesIndependentWhenTheFirstIsCanceled()
        {
            using var world = World.Create();
            using var identity = new IWeb3IdentityCache.Fake(Substitute.For<IWeb3Identity>());
            using var firstPage = new CancellationTokenSource();
            var provider = new EcsEmoteProvider(world, identity);
            var query = new QueryDescription().WithAll<GetTrimmedEmotesByParamIntention>();

            var first = provider.GetTrimmedByParamsAsync(new IEmoteProvider.OwnedEmotesRequestOptions(1, 16, null, null, "first"), firstPage.Token);
            var second = provider.GetTrimmedByParamsAsync(new IEmoteProvider.OwnedEmotesRequestOptions(2, 16, null, null, "second"), CancellationToken.None);
            var entities = new Entity[2];
            world.GetEntities(query, entities);

            Assert.That(entities.Count(e => world.Get<GetTrimmedEmotesByParamIntention>(e).Params.Any(p => p.Item2 == "first")), Is.EqualTo(1));
            Entity secondEntity = entities.Single(e => world.Get<GetTrimmedEmotesByParamIntention>(e).Params.Any(p => p.Item2 == "second"));
            firstPage.Cancel();
            Assert.That((await first.SuppressToResultAsync()).Success, Is.False);
            Assert.That(world.CountEntities(query), Is.EqualTo(1));

            world.Add(secondEntity, new StreamableLoadingResult<TrimmedEmotesResponse>(new TrimmedEmotesResponse(Array.Empty<ITrimmedEmote>(), 0)));
            Assert.That((await second).totalAmount, Is.Zero);
        }

        [TestCase(false, true)]
        [TestCase(true, true)]
        [TestCase(true, false)]
        public async Task PreserveEmptyResultsUnlessTheCallerRequestsErrors(bool failRequest, bool propagateErrors)
        {
            using var world = World.Create();
            using var identity = new IWeb3IdentityCache.Fake(Substitute.For<IWeb3Identity>());
            var provider = new EcsEmoteProvider(world, identity);
            var output = new List<ITrimmedEmote>();
            var options = new IEmoteProvider.OwnedEmotesRequestOptions(null, null, null, null, null, propagateErrors: propagateErrors);
            var request = provider.GetTrimmedByParamsAsync(options, CancellationToken.None, output);

            var query = new QueryDescription().WithAll<GetTrimmedEmotesByParamIntention>();
            var entities = new Entity[1];
            Assert.That(world.CountEntities(query), Is.EqualTo(1));
            world.GetEntities(query, entities);
            world.Add(entities[0], failRequest
                ? new StreamableLoadingResult<TrimmedEmotesResponse>(new ReportData(ReportCategory.EMOTE), new TimeoutException("Owned emote request timed out"))
                : new StreamableLoadingResult<TrimmedEmotesResponse>(new TrimmedEmotesResponse(Array.Empty<ITrimmedEmote>(), 0)));

            var result = await request.SuppressToResultAsync();
            Assert.That(result.Success, Is.EqualTo(!failRequest || !propagateErrors));
            Assert.That(output, Is.Empty);
            if (result.Success)
                Assert.That(result.Value.totalAmount, Is.Zero);
        }
    }
}
