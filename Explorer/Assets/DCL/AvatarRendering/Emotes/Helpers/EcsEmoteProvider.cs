using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Components;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using System;
using System.Collections.Generic;
using System.Threading;
using PromiseByPointers = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.EmotesResolution,
    DCL.AvatarRendering.Emotes.GetEmotesByPointersIntention>;
using OwnedEmotesPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Emotes.TrimmedEmotesResponse,
    DCL.AvatarRendering.Emotes.GetTrimmedEmotesByParamIntention>;

namespace DCL.AvatarRendering.Emotes
{
    public class EcsEmoteProvider : IEmoteProvider
    {
        private const string COLLECTION_ID = "collectionId";

        private readonly World world;
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly List<(string, string)> requestParameters = new ();

        public EcsEmoteProvider(World world,
            IWeb3IdentityCache web3IdentityCache)
        {
            this.world = world;
            this.web3IdentityCache = web3IdentityCache;
        }

        public async UniTask<(IReadOnlyList<ITrimmedEmote> results, int totalAmount)> GetTrimmedByParamsAsync(
            IEmoteProvider.OwnedEmotesRequestOptions requestOptions,
            CancellationToken ct,
            List<ITrimmedEmote>? results = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false
        )
        {
            requestParameters.Clear();
            requestParameters.Add((IElementsProviderQueryParams.TRIMMED, "true"));

            if (requestOptions.PageNum.HasValue)
                requestParameters.Add((IElementsProviderQueryParams.PAGE_NUMBER, requestOptions.PageNum.ToString()));

            if (requestOptions.PageSize.HasValue)
                requestParameters.Add((IElementsProviderQueryParams.PAGE_SIZE, requestOptions.PageSize.ToString()));

            if (requestOptions.IncludeAmount ?? true)
                requestParameters.Add((IElementsProviderQueryParams.INCLUDE_AMOUNT, "true"));

            if (requestOptions.CollectionId.HasValue)
                requestParameters.Add((COLLECTION_ID, requestOptions.CollectionId));

            if (requestOptions.OrderOperation.HasValue)
            {
                requestParameters.Add((IElementsProviderQueryParams.ORDER_BY, requestOptions.OrderOperation.Value.By));
                requestParameters.Add((IElementsProviderQueryParams.ORDER_DIRECTION, requestOptions.OrderOperation.Value.IsAscending ? "asc" : "desc"));
            }

            if(requestOptions.Name != null)
                requestParameters.Add((IElementsProviderQueryParams.NAME, requestOptions.Name));

            results ??= new List<ITrimmedEmote>();

            var intentionResults = new List<ITrimmedEmote>();
            var intention = new GetTrimmedEmotesByParamIntention(requestParameters, web3IdentityCache.Identity!.Address, intentionResults, 0, needsBuilderAPISigning);
            if (loadingArguments.HasValue)
                intention.CommonArguments = loadingArguments.Value;

            var promise = await OwnedEmotesPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY).ToUniTaskAsync(world, cancellationToken: ct);

            ct.ThrowIfCancellationRequested();

            var response = promise.Result!.Value;
            if (!response.Succeeded)
            {
                if (requestOptions.PropagateErrors)
                    throw response.Exception!;

                return (results, 0);
            }

            results.AddRange(response.Asset.Emotes);
            return (results, response.Asset.TotalAmount);
        }

        public async UniTask<IReadOnlyCollection<IEmote>?> GetByPointersAsync(
            IReadOnlyCollection<URN> emoteIds,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IEmote>? output = null)
        {
            output ??= new List<IEmote>();
            await ResolvePointersAsync(EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, emoteIds), ct, output);
            return output;
        }

        public async UniTask GetMetadataByPointersAsync(IReadOnlyCollection<URN> pointers, CancellationToken ct, List<IEmote> results)
        {
            using var intention = EmoteComponentsUtils.CreateGetEmotesByPointersIntention(default, pointers, metadataOnly: true);
            await ResolvePointersAsync(intention, ct, results);
        }

        private async UniTask ResolvePointersAsync(GetEmotesByPointersIntention intention, CancellationToken ct, List<IEmote> output)
        {
            output.Clear();
            var promise = PromiseByPointers.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            try { promise = await promise.ToUniTaskAsync(world, cancellationToken: ct); }
            catch (OperationCanceledException) when (intention.MetadataOnly)
            {
                if (promise.TryConsume(world, out var completed))
                {
                    if (completed.Succeeded)
                        completed.Asset.ConsumeEmotes().Dispose();
                }
                else
                    promise.ForgetLoading(world);

                throw;
            }

            if (!promise.Result.HasValue)
                return;

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception!;

            using var emotes = promise.Result.Value.Asset.ConsumeEmotes();

            output.AddRange(emotes.Value);
            foreach (var emote in output)
                emote.PopulateTrimmedModel();
        }
    }
}
