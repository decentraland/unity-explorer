using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Loading.Systems.Abstract;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;
using AssetBundleRegistryVersionsPromise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.AssetBundlesVersions, ECS.StreamableLoading.AssetBundles.GetAssetBundleRegistryVersionsIntention>;

namespace DCL.AvatarRendering.Wearables.Systems.Load
{
    [UpdateInGroup(typeof(LoadGlobalSystemGroup))]
    [LogCategory(ReportCategory.WEARABLE)]
    public partial class LoadWearablesByParamSystem : LoadSystemBase<WearablesResponse, GetWearableByParamIntention>
    {
        private static readonly ArrayPool<URN> ARRAY_POOL = ArrayPool<URN>.Create(25, 2);

        private readonly URLSubdirectory lambdaSubdirectory;
        private readonly URLSubdirectory wearablesSubdirectory;
        private readonly IWebRequestController webRequestController;
        private readonly IAvatarElementStorage<IWearable, WearableDTO> avatarElementStorage;
        private readonly string? builderContentURL;
        private readonly string expectedBuilderItemType = "wearable";
        private readonly IRealmData realmData;

        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadWearablesByParamSystem(
            World world, IWebRequestController webRequestController,
            IRealmData realmData, URLSubdirectory lambdaSubdirectory, URLSubdirectory wearablesSubdirectory,
            IWearableStorage wearableStorage, string? builderContentURL = null
        ) : base(world, NoCache<WearablesResponse, GetWearableByParamIntention>.INSTANCE)
        {
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.wearablesSubdirectory = wearablesSubdirectory;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            this.avatarElementStorage = wearableStorage;
            this.builderContentURL = builderContentURL;
        }

        private URLAddress BuildUrlFromIntention(in GetWearableByParamIntention intention)
        {
            string userID = intention.UserID;
            IReadOnlyList<(string, string)> urlEncodedParams = intention.Params;
            urlBuilder.Clear();

            if (intention.CommonArguments.URL != URLAddress.EMPTY && intention.NeedsBuilderAPISigning)
            {
                var url = new Uri(intention.CommonArguments.URL);
                urlBuilder.AppendDomain(URLDomain.FromString($"{url.Scheme}://{url.Host}"))
                          .AppendSubDirectory(URLSubdirectory.FromString(url.AbsolutePath));
            }
            else
            {
                urlBuilder.AppendDomainWithReplacedPath(realmData.Ipfs.LambdasBaseUrl, lambdaSubdirectory)
                          .AppendSubDirectory(URLSubdirectory.FromString(userID))
                          .AppendSubDirectory(wearablesSubdirectory);
            }

            for (var i = 0; i < urlEncodedParams.Count; i++)
                urlBuilder.AppendParameter(urlEncodedParams[i]);

            return urlBuilder.Build();
        }

        protected sealed override async UniTask<StreamableLoadingResult<WearablesResponse>> FlowInternalAsync(GetWearableByParamIntention intention,
            StreamableLoadingState state, IPartitionComponent partition, CancellationToken ct)
        {
            await realmData.WaitConfiguredAsync();

            URLAddress url = BuildUrlFromIntention(in intention);

            if (intention.NeedsBuilderAPISigning)
            {
                IBuilderLambdaResponse<IBuilderLambdaResponseElement<WearableDTO>>? lambdaResponse =
                    await ParseBuilderResponseAsync(
                        webRequestController.SignedFetchGetAsync(
                            new CommonArguments(url), string.Empty, ct)
                    );

                await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                    LoadBuilderItem(ref intention, lambdaResponse);
            }
            else
            {
                IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedWearableDTO>>? lambdaResponse =
                    await ParseResponseAsync(
                        webRequestController.GetAsync(
                            new CommonArguments(url),
                            ct,
                            GetReportCategory()
                        )
                    );

                var assetBundlesVersions = await GetABVersions(lambdaResponse, partition, ct);

                await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                {
                    intention.SetTotal(lambdaResponse.TotalAmount);

                    // Process elements in parallel for better performance
                    IReadOnlyList<ILambdaResponseElement<TrimmedWearableDTO>> pageElements = lambdaResponse.Page;
                    var elementTasks = new UniTask<ITrimmedWearable>[pageElements.Count];

                    for (var i = 0; i < pageElements.Count; i++)
                    {
                        ILambdaResponseElement<TrimmedWearableDTO>? element = pageElements[i];
                        elementTasks[i] = ProcessElementAsync(element, partition, assetBundlesVersions, ct);
                    }

                    // Wait for all elements to be processed and add results to intention
                    ITrimmedWearable[]? processedWearables = await UniTask.WhenAll(elementTasks);

                    for (var i = 0; i < processedWearables.Length; i++) { intention.AppendToResult(processedWearables[i]); }
                }
            }

            return new StreamableLoadingResult<WearablesResponse>(AssetFromPreparedIntention(in intention));
        }

        private async UniTask<StreamableLoadingResult<AssetBundlesVersions>> GetABVersions(IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedWearableDTO>> lambdaResponse, IPartitionComponent partition, CancellationToken ct)
        {
            URN[] urns = ARRAY_POOL.Rent(lambdaResponse.TotalAmount);

            for (int i = 0; i < lambdaResponse.TotalAmount; i++)
                urns[i] = new URN(lambdaResponse.Page[i].Entity.Metadata.id);

            var promise = AssetBundleRegistryVersionsPromise.Create(World,
                GetAssetBundleRegistryVersionsIntention.Create(urns, new CommonLoadingArguments()),
                partition);

            ARRAY_POOL.Return(urns);

            return (await promise.ToUniTaskAsync(World, cancellationToken: ct)).Result.Value;
        }

        private async UniTask<ITrimmedWearable> ProcessElementAsync(ILambdaResponseElement<TrimmedWearableDTO> element, IPartitionComponent partition, StreamableLoadingResult<AssetBundlesVersions> assetBundlesVersions, CancellationToken ct)
        {
            TrimmedWearableDTO elementDTO = element.Entity;
            ITrimmedWearable wearable = new TrimmedWearable(elementDTO);

            // Run the asset bundle fallback check in parallel
            if (!assetBundlesVersions.Succeeded)
                await AssetBundleManifestFallbackHelper.CheckAssetBundleManifestFallbackAsync(World, wearable.TrimmedDTO, partition, ct);
            else if (assetBundlesVersions.Asset.versions.TryGetValue(elementDTO.Metadata.id, out var wearableVersions))
                wearable.TrimmedDTO.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest(wearableVersions.mac.version, wearableVersions.mac.originalBuildDate, wearableVersions.windows.version,  wearableVersions.windows.originalBuildDate);
            else
                await AssetBundleManifestFallbackHelper.CheckAssetBundleManifestFallbackAsync(World, wearable.TrimmedDTO, partition, ct);

            // Process individual data (this part needs to remain sequential per element for thread safety)
            foreach (ElementIndividualDataDto individualData in element.IndividualData)
            {
                // Probably a base wearable, wrongly return individual data. Skip it
                if (elementDTO.Metadata.id == individualData.id) continue;

                long.TryParse(individualData.transferredAt, out long transferredAt);
                decimal.TryParse(individualData.price, out decimal price);

                avatarElementStorage.SetOwnedNft(
                    elementDTO.Metadata.id,
                    new NftBlockchainOperationEntry(
                        individualData.id,
                        individualData.tokenId,
                        DateTimeOffset.FromUnixTimeSeconds(transferredAt).DateTime,
                        price
                    )
                );

                ReportHub.Log(ReportCategory.OUTFITS, $"<color=green>[WEARABLE_STORAGE_POPULATED]</color> Key: '{elementDTO.Metadata.id}' now maps to Value: '{individualData.id}' (Token: {individualData.tokenId})");
            }

            return wearable;
        }

        private void LoadBuilderItem(ref GetWearableByParamIntention intention, IBuilderLambdaResponse<IBuilderLambdaResponseElement<WearableDTO>> lambdaResponse)
        {
            if (string.IsNullOrEmpty(builderContentURL)) return;

            if (lambdaResponse.CollectionElements is { Count: > 0 })
            {
                var totalCount = 0;

                foreach (IBuilderLambdaResponseElement<WearableDTO>? element in lambdaResponse.CollectionElements)
                {
                    WearableDTO elementDTO = element.BuildElementDTO(builderContentURL);

                    if (!string.IsNullOrEmpty(expectedBuilderItemType) && elementDTO.type != expectedBuilderItemType)
                        continue;

                    ITrimmedWearable avatarElement = avatarElementStorage.GetOrAddByDTO(elementDTO, false);

                    //Builder items will never have an asset bundle
                    if (avatarElement.TrimmedDTO.assetBundleManifestVersion == null)
                        avatarElement.TrimmedDTO.assetBundleManifestVersion = AssetBundleManifestVersion.CreateLSDAsset();

                    intention.AppendToResult(avatarElement);
                    totalCount++;
                }

                intention.SetTotal(totalCount);
            }
        }

        private WearablesResponse AssetFromPreparedIntention(in GetWearableByParamIntention intention) =>
            new (intention.Results, intention.TotalAmount);

        private async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedWearableDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<TrimmedWearableDTO.LambdaResponse>(WRJsonParser.Newtonsoft);

        private async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<WearableDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter) =>
            await adapter.CreateFromJson<BuilderWearableDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);
    }
}
