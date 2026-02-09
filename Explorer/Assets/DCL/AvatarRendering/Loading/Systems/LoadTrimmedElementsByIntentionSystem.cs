using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.AvatarRendering.Loading.Components;
using DCL.AvatarRendering.Loading.DTO;
using DCL.AvatarRendering.Wearables.Components;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS;
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

namespace DCL.AvatarRendering.Loading.Systems.Abstract
{
    public abstract class LoadTrimmedElementsByIntentionSystem<TAsset, TIntention, TAvatarElement, TAvatarElementDTO, TFullAvatarElement, TFullAvatarElementDTO> :
        LoadSystemBase<TAsset, TIntention>
        where TIntention: struct, IAttachmentsLoadingIntention<TAvatarElement>, IEquatable<TIntention>
        where TAvatarElementDTO : TrimmedAvatarAttachmentDTO
        where TAvatarElement : ITrimmedAvatarAttachment
        where TFullAvatarElementDTO : AvatarAttachmentDTO
        where TFullAvatarElement : IAvatarAttachment<TFullAvatarElementDTO>, TAvatarElement
    {
        private static readonly ArrayPool<URN> ARRAY_POOL = ArrayPool<URN>.Create(25, 2);

        private readonly ITrimmedAvatarElementStorage<TAvatarElement, TAvatarElementDTO> trimmedAvatarElementStorage;
        private readonly IAvatarElementStorage<TFullAvatarElement, TFullAvatarElementDTO> avatarElementStorage;
        private readonly IRealmData realmData;
        private readonly URLDomain assetBundleRegistryVersionURL;
        private readonly URLSubdirectory lambdaSubdirectory;
        private readonly URLSubdirectory elementSubdirectory;
        private readonly IWebRequestController webRequestController;
        private readonly string? builderContentURL;
        private readonly string expectedBuilderItemType;

        internal IURLBuilder urlBuilder = new URLBuilder();

        protected LoadTrimmedElementsByIntentionSystem(
            World world,
            IStreamableCache<TAsset, TIntention> cache,
            ITrimmedAvatarElementStorage<TAvatarElement, TAvatarElementDTO> trimmedAvatarElementStorage,
            IAvatarElementStorage<TFullAvatarElement, TFullAvatarElementDTO> avatarElementStorage,
            IRealmData realmData,
            URLSubdirectory lambdaSubdirectory,
            URLSubdirectory elementSubdirectory,
            URLDomain assetBundleRegistryVersionURL,
            IWebRequestController webRequestController,
            string expectedBuilderItemType,
            DiskCacheOptions<TAsset, TIntention>? diskCacheOptions = null,
            string? builderContentURL = null
        ) : base(world, cache, diskCacheOptions)
        {
            this.trimmedAvatarElementStorage = trimmedAvatarElementStorage;
            this.avatarElementStorage = avatarElementStorage;
            this.realmData = realmData;
            this.lambdaSubdirectory = lambdaSubdirectory;
            this.elementSubdirectory = elementSubdirectory;
            this.assetBundleRegistryVersionURL = assetBundleRegistryVersionURL;
            this.webRequestController = webRequestController;
            this.builderContentURL = builderContentURL;
            this.expectedBuilderItemType = expectedBuilderItemType;
        }

        private URLAddress BuildUrlFromIntention(in TIntention intention)
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
                          .AppendSubDirectory(elementSubdirectory);
            }

            for (var i = 0; i < urlEncodedParams.Count; i++)
                urlBuilder.AppendParameter(urlEncodedParams[i]);

            return urlBuilder.Build();
        }

        protected sealed override async UniTask<StreamableLoadingResult<TAsset>> FlowInternalAsync(TIntention intention,
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
                    var elementTasks = new UniTask<TAvatarElement>[pageElements.Count];

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

            return new StreamableLoadingResult<TAsset>(AssetFromPreparedIntention(in intention));
        }

        private async UniTask<AssetBundlesVersions> GetABVersionsAsync(IAttachmentLambdaResponse<ILambdaResponseElement<TAvatarElementDTO>> lambdaResponse, CancellationToken ct)
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

        private void LoadBuilderItem(ref TIntention intention, IBuilderLambdaResponse<IBuilderLambdaResponseElement<TFullAvatarElementDTO>> lambdaResponse)
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

        private async UniTask<TAvatarElement> ProcessElementAsync(ILambdaResponseElement<TAvatarElementDTO> element, IPartitionComponent partition, AssetBundlesVersions assetBundlesVersions, CancellationToken ct)
        {
            var elementDTO = element.Entity;

            elementDTO.thumbnail = AssetBundleManifestHelper.SanitizeEntityHash(elementDTO.thumbnail);

            var wearable = trimmedAvatarElementStorage.GetOrAddByDTO(elementDTO);

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

            wearable.SetAmount(element.IndividualData?.Count ?? 1);
            return wearable;
        }

        protected abstract UniTask<IAttachmentLambdaResponse<ILambdaResponseElement<TAvatarElementDTO>>> ParseResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter);

        protected abstract UniTask<IBuilderLambdaResponse<IBuilderLambdaResponseElement<TFullAvatarElementDTO>>> ParseBuilderResponseAsync(GenericDownloadHandlerUtils.Adapter<GenericGetRequest, GenericGetArguments> adapter);

        protected abstract TAsset AssetFromPreparedIntention(in TIntention intention);
    }
}
