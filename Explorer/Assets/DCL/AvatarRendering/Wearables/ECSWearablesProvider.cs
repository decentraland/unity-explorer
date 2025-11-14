using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using Runtime.Wearables;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearablesResponse, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSWearablesProvider : IWearablesProvider
    {
        private const string PAGE_NUMBER = "pageNum";
        private const string PAGE_SIZE = "pageSize";
        private const string NETWORK = "network";
        private const string INCLUDE_AMOUNT = "includeAmount";
        private const string CATEGORY = "category";
        private const string ORDER_BY = "orderBy";
        private const string COLLECTION_TYPE = "collectionType";
        private const string ORDER_DIRECTION = "direction";
        private const string SEARCH = "name";
        private const string ASCENDING = "ASC";
        private const string DESCENDING = "DESC";
        private const string ON_CHAIN_COLLECTION_TYPE = "on-chain";
        private const string THIRD_PARTY_COLLECTION_TYPE = "third-party";
        private const string BASE_WEARABLE_COLLECTION_TYPE = "base-wearable";

        private readonly string[] allWearableCategories = WearableCategories.CATEGORIES_PRIORITY.ToArray();
        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly List<(string, string)> requestParameters = new ();
        private readonly World world;

        public ECSWearablesProvider(
            IWeb3IdentityCache web3IdentityCache,
            World world)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.world = world;
        }

        public async UniTask<(IReadOnlyList<IWearable> results, int totalAmount)> GetAsync(
            int pageSize, int pageNumber, CancellationToken ct,
            IWearablesProvider.SortingField sortingField = IWearablesProvider.SortingField.Date,
            IWearablesProvider.OrderBy orderBy = IWearablesProvider.OrderBy.Descending,
            string? category = null,
            IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All,
            string? name = null,
            List<IWearable>? results = null,
            string? network = null,
            bool? includeAmount = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false)
        {
            requestParameters.Clear();
            requestParameters.Add((PAGE_NUMBER, pageNumber.ToString()));
            requestParameters.Add((PAGE_SIZE, pageSize.ToString()));

            if (!string.IsNullOrEmpty(network))
                requestParameters.Add((NETWORK, network));

            if (includeAmount ?? true)
                requestParameters.Add((INCLUDE_AMOUNT, "true"));
            
            if (!string.IsNullOrEmpty(category))
                requestParameters.Add((CATEGORY, category));

            requestParameters.Add((ORDER_BY, sortingField.ToString()));
            requestParameters.Add((ORDER_DIRECTION, GetDirectionParamValue(orderBy)));

            if (collectionType.HasFlag(IWearablesProvider.CollectionType.Base))
                requestParameters.Add((COLLECTION_TYPE, BASE_WEARABLE_COLLECTION_TYPE));

            if (collectionType.HasFlag(IWearablesProvider.CollectionType.OnChain))
                requestParameters.Add((COLLECTION_TYPE, ON_CHAIN_COLLECTION_TYPE));

            if (collectionType.HasFlag(IWearablesProvider.CollectionType.ThirdParty))
                requestParameters.Add((COLLECTION_TYPE, THIRD_PARTY_COLLECTION_TYPE));

            if (!string.IsNullOrEmpty(name))
                requestParameters.Add((SEARCH, name));

            results ??= new List<IWearable>();

            var intention = new GetWearableByParamIntention(requestParameters, web3IdentityCache.Identity!.Address, results, 0, needsBuilderAPISigning);
            if (loadingArguments.HasValue)
                intention.CommonArguments = loadingArguments.Value;

            var wearablesPromise = ParamPromise.Create(world!,
                intention,
                PartitionComponent.TOP_PRIORITY);

            wearablesPromise = await wearablesPromise.ToUniTaskAsync(world!, cancellationToken: ct);

            ct.ThrowIfCancellationRequested();

            if (wearablesPromise.Result == null) return (results, 0);
            if (!wearablesPromise.Result.HasValue) return (results, 0);
            if (!wearablesPromise.Result!.Value.Succeeded) return (results, 0);

            // Should be the same assigned in the intention as `results`
            return (wearablesPromise.Result.Value.Asset.Wearables,
                wearablesPromise.Result.Value.Asset.TotalAmount);
        }

        public async UniTask<IReadOnlyCollection<IWearable>?> RequestPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct)
        {
            var promise = WearablePromise.Create(world,

                // We pass all categories as force renderer to force the download of all of them
                // Otherwise they will be skipped if any wearable is hiding the category
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(bodyShape, pointers, allWearableCategories),
                PartitionComponent.TOP_PRIORITY);

            promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);

            if (!promise.TryGetResult(world, out var result))
                return null;

            if (!result.Succeeded)
                return null;

            return result.Asset.Wearables;
        }

        private static string GetDirectionParamValue(IWearablesProvider.OrderBy orderBy)
        {
            switch (orderBy)
            {
                case IWearablesProvider.OrderBy.Ascending:
                default:
                    return ASCENDING;
                case IWearablesProvider.OrderBy.Descending:
                    return DESCENDING;
            }
        }
    }
}
