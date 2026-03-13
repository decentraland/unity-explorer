using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
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
using Utility;
using ParamPromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Helpers.TrimmedWearablesResponse, DCL.AvatarRendering.Wearables.Components.Intentions.GetTrimmedWearableByParamIntention>;
using WearablePromise = ECS.StreamableLoading.Common.AssetPromise<DCL.AvatarRendering.Wearables.Components.WearablesResolution,
    DCL.AvatarRendering.Wearables.Components.Intentions.GetWearablesByPointersIntention>;

namespace DCL.AvatarRendering.Wearables
{
    public class ECSWearablesProvider : IWearablesProvider
    {
        private const string NETWORK = "network";
        private const string CATEGORY = "category";
        private const string COLLECTION_TYPE = "collectionType";
        private const string ON_CHAIN_COLLECTION_TYPE = "on-chain";
        private const string THIRD_PARTY_COLLECTION_TYPE = "third-party";
        private const string BASE_WEARABLE_COLLECTION_TYPE = "base-wearable";
        private const string IS_SMART_WEARABLE = "isSmartWearable";

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

        public async UniTask<(IReadOnlyList<ITrimmedWearable> results, int totalAmount)> GetTrimmedByParamsAsync(
            IWearablesProvider.Params parameters,
            CancellationToken ct,
            List<ITrimmedWearable>? results = null,
            CommonLoadingArguments? loadingArguments = null,
            bool needsBuilderAPISigning = false)
        {
            requestParameters.Clear();
            requestParameters.Add((IElementsProviderQueryParams.PAGE_NUMBER, parameters.PageNumber.ToString()));
            requestParameters.Add((IElementsProviderQueryParams.PAGE_SIZE, parameters.PageSize.ToString()));
            requestParameters.Add((IElementsProviderQueryParams.TRIMMED, "true"));

            if (!string.IsNullOrEmpty(parameters.Network))
                requestParameters.Add((NETWORK, parameters.Network));

            if (parameters.IncludeAmount ?? true)
                requestParameters.Add((IElementsProviderQueryParams.INCLUDE_AMOUNT, "true"));

            if (!string.IsNullOrEmpty(parameters.Category))
                requestParameters.Add((CATEGORY, parameters.Category));

            requestParameters.Add((IElementsProviderQueryParams.ORDER_BY, parameters.SortingField.ToString()));
            requestParameters.Add((IElementsProviderQueryParams.ORDER_DIRECTION, GetDirectionParamValue(parameters.OrderBy)));

            if (EnumUtils.HasFlag(parameters.CollectionType, IWearablesProvider.CollectionType.Base))
                requestParameters.Add((COLLECTION_TYPE, BASE_WEARABLE_COLLECTION_TYPE));

            if (EnumUtils.HasFlag(parameters.CollectionType, IWearablesProvider.CollectionType.OnChain))
                requestParameters.Add((COLLECTION_TYPE, ON_CHAIN_COLLECTION_TYPE));

            if (EnumUtils.HasFlag(parameters.CollectionType, IWearablesProvider.CollectionType.ThirdParty))
                requestParameters.Add((COLLECTION_TYPE, THIRD_PARTY_COLLECTION_TYPE));

            if (parameters.SmartWearablesOnly)
                requestParameters.Add((IS_SMART_WEARABLE, "true"));

            if (!string.IsNullOrEmpty(parameters.Name))
                requestParameters.Add((IElementsProviderQueryParams.NAME, parameters.Name));

            results ??= new List<ITrimmedWearable>();

            var intention = new GetTrimmedWearableByParamIntention(requestParameters, web3IdentityCache.Identity!.Address, results, 0, needsBuilderAPISigning);
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

        public async UniTask<IReadOnlyCollection<IWearable>?> GetByPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IWearable>? results = null)
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

            results ??= new List<IWearable>();

            results.AddRange(result.Asset.Wearables);

            return results;
        }

        private static string GetDirectionParamValue(IWearablesProvider.OrderBy orderBy)
        {
            switch (orderBy)
            {
                case IWearablesProvider.OrderBy.Ascending:
                default:
                    return IElementsProviderQueryParams.Values.ASCENDING;
                case IWearablesProvider.OrderBy.Descending:
                    return IElementsProviderQueryParams.Values.DESCENDING;
            }
        }

    }
}
