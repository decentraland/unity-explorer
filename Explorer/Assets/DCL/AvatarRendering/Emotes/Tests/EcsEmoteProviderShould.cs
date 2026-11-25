using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Utilities.Extensions;
using DCL.Web3.Identities;
using ECS.StreamableLoading.Common.Components;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace DCL.AvatarRendering.Emotes.Tests
{
    public class EcsEmoteProviderShould
    {
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
