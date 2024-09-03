using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.Web3.Identities;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using System;
using System.Collections.Generic;
using System.Threading;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.WearablesResponse, DCL.AvatarRendering.Wearables.Components.Intentions.GetWearableByParamIntention>;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSWearablesProvider : IWearablesProvider
    {
        private const string PAGE_NUMBER = "pageNum";
        private const string PAGE_SIZE = "pageSize";
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

        private readonly IWeb3IdentityCache web3IdentityCache;
        private readonly List<(string, string)> requestParameters = new ();

        private World? world;

        public ECSWearablesProvider(
            IWeb3IdentityCache web3IdentityCache)
        {
            this.web3IdentityCache = web3IdentityCache;
        }

        public void Initialize(World world)
        {
            this.world = world;
        }

        public async UniTask<IReadOnlyList<IWearable>> GetAsync(int pageSize, int pageNumber, CancellationToken ct,
            IWearablesProvider.SortingField sortingField = IWearablesProvider.SortingField.Date, IWearablesProvider.OrderBy orderBy = IWearablesProvider.OrderBy.Descending,
            string? category = null, IWearablesProvider.CollectionType collectionType = IWearablesProvider.CollectionType.All,
            string? name = null, List<IWearable>? results = null)
        {
            requestParameters.Clear();
            requestParameters.Add((PAGE_NUMBER, pageNumber.ToString()));
            requestParameters.Add((PAGE_SIZE, pageSize.ToString()));

            if (!string.IsNullOrEmpty(category))
                requestParameters.Add((CATEGORY, category));

            requestParameters.Add((ORDER_BY, sortingField.ToString()));
            requestParameters.Add((ORDER_DIRECTION, GetDirectionParamValue(orderBy)));

            if ((collectionType & IWearablesProvider.CollectionType.Base) != 0)
                requestParameters.Add((COLLECTION_TYPE, BASE_WEARABLE_COLLECTION_TYPE));

            if ((collectionType & IWearablesProvider.CollectionType.OnChain) != 0)
                requestParameters.Add((COLLECTION_TYPE, ON_CHAIN_COLLECTION_TYPE));

            if ((collectionType & IWearablesProvider.CollectionType.ThirdParty) != 0)
                requestParameters.Add((COLLECTION_TYPE, THIRD_PARTY_COLLECTION_TYPE));

            if (!string.IsNullOrEmpty(name))
                requestParameters.Add((SEARCH, name));

            results ??= new List<IWearable>();

            var wearablesPromise = ParamPromise.Create(world!,
                new GetWearableByParamIntention(requestParameters, web3IdentityCache.Identity!.Address, results),
                PartitionComponent.TOP_PRIORITY);

            wearablesPromise = await wearablesPromise.ToUniTaskAsync(world!, cancellationToken: ct);

            ct.ThrowIfCancellationRequested();

            if (wearablesPromise.Result == null) return ArraySegment<IWearable>.Empty;
            if (!wearablesPromise.Result.HasValue) return ArraySegment<IWearable>.Empty;
            if (!wearablesPromise.Result!.Value.Succeeded) return ArraySegment<IWearable>.Empty;

            // Should be the same assigned in the intention as `results`
            return (wearablesPromise.Result.Value.Asset.Wearables);
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
