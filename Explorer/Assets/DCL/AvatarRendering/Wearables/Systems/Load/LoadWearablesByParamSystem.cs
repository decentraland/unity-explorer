using Arch.Core;
using Arch.SystemGroups;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading;
using DCL.AvatarRendering.Wearables.Components;
using DCL.AvatarRendering.Wearables.Components.Intentions;
using DCL.AvatarRendering.Wearables.Helpers;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.Utility;
using DCL.WebRequests;
using ECS;
using ECS.Groups;
using ECS.Prioritization.Components;
using ECS.StreamableLoading;
using ECS.StreamableLoading.AssetBundles;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Common.Systems;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Threading;
using Utility.Multithreading;

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
        private readonly ITrimmedWearableStorage trimmedWearableStorage;
        private readonly string? builderContentURL;
        private readonly string expectedBuilderItemType = "wearable";
        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleRegistryVersionURL;

        internal IURLBuilder urlBuilder = new URLBuilder();

        public LoadWearablesByParamSystem(
            World world, IWebRequestController webRequestController,
            IStreamableCache<WearablesResponse, GetWearableByParamIntention> cache,
            IRealmData realmData, URLSubdirectory lambdaSubdirectory, URLSubdirectory wearablesSubdirectory, URLDomain assetBundleRegistryVersionURL,
            IWearableStorage wearableStorage, ITrimmedWearableStorage trimmedWearableStorage, string? builderContentURL = null
        ) : base(world, cache)
        {
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.wearablesSubdirectory = wearablesSubdirectory;
            this.assetBundleRegistryVersionURL = assetBundleRegistryVersionURL;
            this.webRequestController = webRequestController;
            this.realmData = realmData;
            avatarElementStorage = wearableStorage;
            this.trimmedWearableStorage = trimmedWearableStorage;
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

            var url = BuildUrlFromIntention(in intention);

            if (intention.NeedsBuilderAPISigning)
            {
                var lambdaResponse =
                    await ParseBuilderResponseAsync(
                        webRequestController.SignedFetchGetAsync(
                            new CommonArguments(url), string.Empty, ct)
                    );

                await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                    LoadBuilderItem(ref intention, lambdaResponse);
            }
            else
            {
                var lambdaResponse =
                    await ParseResponseAsync(
                        webRequestController.GetAsync(
                            new CommonArguments(url),
                            ct,
                            GetReportCategory()
                        )
                    );

                var assetBundlesVersions = await GetABVersionsAsync(lambdaResponse, ct);

                await using (await ExecuteOnThreadPoolScope.NewScopeWithReturnOnMainThreadAsync())
                {
                    intention.SetTotal(lambdaResponse.TotalAmount);

                    // Process elements in parallel for better performance
                    var pageElements = lambdaResponse.Page;
                    var elementTasks = new UniTask<ITrimmedWearable>[pageElements.Count];

                    for (int i = 0; i < pageElements.Count; i++)
                    {
                        var element = pageElements[i];
                        elementTasks[i] = ProcessElementAsync(element, partition, assetBundlesVersions, ct);
                    }

                    // Wait for all elements to be processed and add results to intention
                    var processedWearables = await UniTask.WhenAll(elementTasks);

                    for (int i = 0; i < processedWearables.Length; i++) { intention.AppendToResult(processedWearables[i]); }
                }
            }

            return new StreamableLoadingResult<WearablesResponse>(AssetFromPreparedIntention(in intention));
        }

        private async UniTask<AssetBundlesVersions> GetABVersionsAsync(IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedWearableDTO>> lambdaResponse, CancellationToken ct)
        {
            if (lambdaResponse.TotalAmount == 0)
                return AssetBundlesVersions.Create();

            var urns = ARRAY_POOL.Rent(lambdaResponse.Page.Count);

            for (int i = 0; i < lambdaResponse.Page.Count; i++)
                urns[i] = new URN(lambdaResponse.Page[i].Entity.Metadata.id);

            var result = await AssetBundleRegistryVersionHelper.GetABRegistryVersionsByPointersAsync(urns, webRequestController, assetBundleRegistryVersionURL, GetReportData(), ct);

            ARRAY_POOL.Return(urns);

            return result;
        }

        private async UniTask<ITrimmedWearable> ProcessElementAsync(ILambdaResponseElement<TrimmedWearableDTO> element, IPartitionComponent partition, AssetBundlesVersions assetBundlesVersions, CancellationToken ct)
        {
            var elementDTO = element.Entity;

            elementDTO.thumbnail = AssetBundleManifestHelper.SanitizeEntityHash(elementDTO.thumbnail);

            var wearable = trimmedWearableStorage.GetOrAddByDTO(elementDTO);

            // Run the asset bundle fallback check in parallel
            if (assetBundlesVersions.versions.TryGetValue(elementDTO.Metadata.id, out var wearableVersions))
                wearable.TrimmedDTO.assetBundleManifestVersion = AssetBundleManifestVersion.CreateManualManifest(wearableVersions.mac.version, wearableVersions.mac.buildDate, wearableVersions.windows.version,  wearableVersions.windows.buildDate);
            else
                await AssetBundleManifestFallbackHelper.CheckAssetBundleManifestFallbackAsync(World, wearable.TrimmedDTO, partition, ct);

            if (element.IndividualData != null)
                // Process individual data (this part needs to remain sequential per element for thread safety)
                foreach (var individualData in element.IndividualData)
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

            int ownedAmount = avatarElementStorage.GetOwnedNftCount(elementDTO.Metadata.id);
            wearable.SetAmount(ownedAmount);
            return wearable;
        }

        private void LoadBuilderItem(ref GetWearableByParamIntention intention, IBuilderLambdaResponse<IBuilderLambdaResponseElement<WearableDTO>> lambdaResponse)
        {
            if (string.IsNullOrEmpty(builderContentURL)) return;

            if (lambdaResponse.CollectionElements is { Count: > 0 })
            {
                int totalCount = 0;

                foreach (var element in lambdaResponse.CollectionElements)
                {
                    var elementDTO = element.BuildElementDTO(builderContentURL);

                    if (!string.IsNullOrEmpty(expectedBuilderItemType) && elementDTO.type != expectedBuilderItemType)
                        continue;

                    var avatarElement = avatarElementStorage.GetOrAddByDTO(elementDTO, false);

                    //Builder items will never have an asset bundle
                    if (avatarElement.DTO.assetBundleManifestVersion == null)
                        avatarElement.DTO.assetBundleManifestVersion = AssetBundleManifestVersion.CreateLSDAsset();

                    intention.AppendToResult(avatarElement);
                    totalCount++;
                }

                intention.SetTotal(totalCount);
            }
        }

        private WearablesResponse AssetFromPreparedIntention(in GetWearableByParamIntention intention)
        {
            return new WearablesResponse (intention.Results, intention.TotalAmount);
        }

        private async UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TrimmedWearableDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter)
        {
            return await adapter.CreateFromJson<TrimmedWearableDTO.LambdaResponse>(WRJsonParser.Newtonsoft);
        }

        private async UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<WearableDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter)
        {
            return await adapter.CreateFromJson<BuilderWearableDTO.BuilderLambdaResponse>(WRJsonParser.Newtonsoft);
        }
    }
}
