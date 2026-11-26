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
using ECS.StreamableLoading.AssetBundles;
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
        private readonly IWearableStorage wearableStorage;

        public ECSWearablesProvider(
            IWeb3IdentityCache web3IdentityCache,
            World world,
            IWearableStorage wearableStorage)
        {
            this.web3IdentityCache = web3IdentityCache;
            this.world = world;
            this.wearableStorage = wearableStorage;
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

            if (parameters.Categories != null)
                foreach (string category in parameters.Categories)
                    requestParameters.Add((CATEGORY, category));

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

            var intentionResults = new List<ITrimmedWearable>();
            // The URL is built from these parameters frames later, so the intention gets its own copy instead of the list the next call clears
            var intention = new GetTrimmedWearableByParamIntention(new List<(string, string)>(requestParameters), web3IdentityCache.Identity!.Address, intentionResults, 0, needsBuilderAPISigning);
            if (loadingArguments.HasValue)
                intention.CommonArguments = loadingArguments.Value;

            var wearablesPromise = ParamPromise.Create(world,
                intention,
                PartitionComponent.TOP_PRIORITY);

            wearablesPromise = await wearablesPromise.ToUniTaskAsync(world, cancellationToken: ct);

            ct.ThrowIfCancellationRequested();

            if (wearablesPromise.Result == null) return (results, 0);
            if (!wearablesPromise.Result.HasValue) return (results, 0);
            if (!wearablesPromise.Result!.Value.Succeeded) return (results, 0);

            results.AddRange(wearablesPromise.Result.Value.Asset.Wearables);
            return (results, wearablesPromise.Result.Value.Asset.TotalAmount);
        }

        public async UniTask<IWearable?> GetForEquipAsync(URN pointer, BodyShape bodyShape, CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();
            if (pointer.IsNullOrEmpty()) return null;

            pointer = pointer.Shorten();
            bodyShape = WearableComponentsUtils.GetBodyShapeForEquip(pointer, bodyShape);

            if (wearableStorage.TryGetElement(pointer, out IWearable cached) && !cached.IsLoading)
            {
                if (cached.Model.Succeeded)
                {
                    if (IsReadyForEquip(cached, pointer, bodyShape)) return cached;
                    if (!cached.IsCompatibleWithBodyShape(bodyShape)) return null;

                    if (string.IsNullOrEmpty(cached.DTO!.ContentDownloadUrl) && cached.DTO.assetBundleManifestVersion is { assetBundleManifestRequestFailed: true })
                    {
                        await AssetBundleManifestFallbackHelper.CheckAssetBundleManifestFallbackAsync(world, cached.DTO, PartitionComponent.TOP_PRIORITY, ct,
                            skipException: true, retryCachedFailure: true);

                        if (cached.DTO.assetBundleManifestVersion.assetBundleManifestRequestFailed) return null;
                    }

                    var slots = cached.WearableAssetResults[bodyShape].Results;
                    for (var i = 0; i < slots?.Length; i++)
                        if (slots[i] is { Exception: not null }) slots[i] = null;
                }
                else if (cached.Model.Exception != null)
                    cached.Model = default;
            }

            List<URN> pointers = WearableComponentsUtils.POINTERS_POOL.Get()!;
            pointers.Add(pointer);
            var intention = new GetWearablesByPointersIntention(pointers, bodyShape, allWearableCategories) { RetryCachedFailures = true };
            var promise = WearablePromise.Create(world, intention, PartitionComponent.TOP_PRIORITY);

            try
            {
                promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);
                if (promise.Result is not { Succeeded: true } result || result.Asset.Wearables.Count != 1) return null;
                IWearable candidate = result.Asset.Wearables[0];
                return IsReadyForEquip(candidate, pointer, bodyShape) ? candidate : null;
            }
            finally
            {
                ConsumeOrCancelPromise(ref promise);

                if (promise.Result is { Succeeded: true } result)
                {
                    foreach (IWearable wearable in result.Asset.Wearables)
                        foreach (var slot in wearable.WearableAssetResults[bodyShape].Results!)
                            slot?.Asset?.Dereference();

                    result.Asset.Dispose();
                }

                intention.Dispose();
            }
        }

        private static bool IsReadyForEquip(IWearable wearable, URN pointer, BodyShape bodyShape) =>
            wearable.GetUrn().Equals(pointer) && wearable.IsCompatibleWithBodyShape(bodyShape) && wearable.HasUsableMainAsset(bodyShape);

        public UniTask<IReadOnlyCollection<IWearable>?> GetByPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IWearable>? results = null) =>
            ResolvePointersAsync(pointers, bodyShape, false, ct, results);

        public UniTask<IReadOnlyCollection<IWearable>?> GetDTOsByPointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            CancellationToken ct,
            List<IWearable>? results = null) =>
            ResolvePointersAsync(pointers, bodyShape, true, ct, results);

        private async UniTask<IReadOnlyCollection<IWearable>?> ResolvePointersAsync(IReadOnlyCollection<URN> pointers,
            BodyShape bodyShape,
            bool dtoOnly,
            CancellationToken ct,
            List<IWearable>? results)
        {
            var promise = WearablePromise.Create(world,

                // We pass all categories as force renderer to force the download of all of them
                // Otherwise they will be skipped if any wearable is hiding the category
                WearableComponentsUtils.CreateGetWearablesByPointersIntention(bodyShape, pointers, allWearableCategories, dtoOnly),
                PartitionComponent.TOP_PRIORITY);

            try
            {
                promise = await promise.ToUniTaskAsync(world, cancellationToken: ct);
                if (promise.Result is not { Succeeded: true } result) return null;

                results ??= new List<IWearable>();
                results.AddRange(result.Asset.Wearables);
                return results;
            }
            finally
            {
                if (dtoOnly)
                {
                    ConsumeOrCancelPromise(ref promise);
                    if (promise.Result is { Succeeded: true } result) result.Asset.Dispose();
                    promise.LoadingIntention.Dispose();
                }
            }
        }

        private void ConsumeOrCancelPromise(ref WearablePromise promise)
        {
            if (promise.IsConsumed || promise.TryConsume(world, out _)) return;

            world.Get<GetWearablesByPointersIntention>(promise.Entity).HideWearablesResolution.Release();
            promise.ForgetLoading(world);
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
