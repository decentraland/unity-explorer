using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
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
        private const string PAGE_NUMBER = "pageNum";
        private const string PAGE_SIZE = "pageSize";
        private const string TRIMMED = "trimmed";
        private const string INCLUDE_AMOUNT = "includeAmount";
        private const string COLLECTION_ID = "collectionId";
        private const string ORDER_BY = "orderBy";
        private const string ORDER_DIRECTION = "direction";
        private const string NAME = "name";
        private const string INCLUDE_ENTITIES = "includeEntities";

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
            requestParameters.Add((TRIMMED, "true"));
            requestParameters.Add((INCLUDE_ENTITIES, "true"));

            if (requestOptions.PageNum.HasValue)
                requestParameters.Add((PAGE_NUMBER, requestOptions.PageNum.ToString()));

            if (requestOptions.PageSize.HasValue)
                requestParameters.Add((PAGE_SIZE, requestOptions.PageSize.ToString()));

            if (requestOptions.IncludeAmount ?? true)
                requestParameters.Add((INCLUDE_AMOUNT, "true"));

            if (requestOptions.CollectionId.HasValue)
                requestParameters.Add((COLLECTION_ID, requestOptions.CollectionId));

            if (requestOptions.OrderOperation.HasValue)
            {
                requestParameters.Add((ORDER_BY, requestOptions.OrderOperation.Value.By));
                requestParameters.Add((ORDER_DIRECTION, requestOptions.OrderOperation.Value.IsAscending ? "asc" : "desc"));
            }

            if(requestOptions.Name != null)
                requestParameters.Add((NAME, requestOptions.Name));

            results ??= new List<ITrimmedEmote>();

            var intention = new GetTrimmedEmotesByParamIntention(requestParameters, web3IdentityCache.Identity!.Address, results, 0, needsBuilderAPISigning);
            if (loadingArguments.HasValue)
                intention.CommonArguments = loadingArguments.Value;

            var promise = await OwnedEmotesPromise.Create(world, intention, PartitionComponent.TOP_PRIORITY).ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.Result.HasValue)
                return (results, 0);

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception!;

            return (promise.Result.Value.Asset.Emotes, promise.Result.Value.Asset.TotalAmount);
        }

        public async UniTask<IReadOnlyCollection<IEmote>?> GetByPointersAsync(
            IReadOnlyCollection<URN> emoteIds,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IEmote>? output = null)
        {
            output ??= new List<IEmote>();

            output.Clear();

            GetEmotesByPointersIntention intention = EmoteComponentsUtils.CreateGetEmotesByPointersIntention(bodyShape, emoteIds);
            var promise = PromiseByPointers.Create(world, intention, PartitionComponent.TOP_PRIORITY);
            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.Result.HasValue)
                return output;

            if (!promise.Result.Value.Succeeded)
                throw promise.Result.Value.Exception!;

            using var emotes = promise.Result.Value.Asset.ConsumeEmotes();
            output.AddRange(emotes.Value);

            return output;
        }
    }
}
