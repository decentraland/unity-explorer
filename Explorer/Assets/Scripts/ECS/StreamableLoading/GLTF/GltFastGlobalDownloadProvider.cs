using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using GLTFast.Loading;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastGlobalDownloadProvider : IDownloadProvider, IDisposable
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly IAcquiredBudget acquiredBudget;
        private readonly string contentSourceUrl;
        private readonly World world;
        private readonly IPartitionComponent partitionComponent;
        private readonly IWebRequestController webRequestController;
        private readonly ReportData reportData;
        private readonly IReadOnlyDictionary<string, string>? contentHashMap;

        public GltFastGlobalDownloadProvider(World world, string contentSourceUrl, IPartitionComponent partitionComponent, ReportData reportData,
            IWebRequestController webRequestController, IAcquiredBudget acquiredBudget
          // , IReadOnlyDictionary<string, string>? contentHashMap = null
            )
        {
            this.world = world;
            this.contentSourceUrl = contentSourceUrl;
            this.partitionComponent = partitionComponent;
            this.reportData = reportData;
            this.webRequestController = webRequestController;
            this.acquiredBudget = acquiredBudget;

            this.contentHashMap = contentHashMap;
        }

        public void Dispose()
        {
            acquiredBudget.Release();
        }

        private static string GetUrl(Uri uri) =>
            (uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString()) ?? string.Empty;

        // RequestAsync is used for fetching the GLTF file itself + some external textures. Whenever this
        // method's request of the base GLTF is finished, the propagated budget for assets loading must be released.
        public async Task<IDownload> RequestAsync(Uri uri)
        {
            // var downloadUri = new Uri(contentSourceUrl+"QmfTLR6kYxBf2akLLYaTm4x8tFNz245mxqmHs5FVcKMurk"); // DEBUGGING
            var downloadUri = new Uri(contentSourceUrl+uri);
            var commonArguments = new CommonArguments(URLAddress.FromString(GetUrl(downloadUri)));

            byte[] data = Array.Empty<byte>();
            string error = string.Empty;
            string text = string.Empty;
            bool success = false;

            DownloadHandler? downloadHandler = null;

            try
            {
                downloadHandler = await webRequestController.GetAsync(commonArguments, new CancellationToken(), reportData).ExposeDownloadHandlerAsync();
                data = downloadHandler.data;

                if (!GltfValidator.IsGltfBinaryFormat(downloadHandler.nativeData))
                    text = downloadHandler.text;

                error = downloadHandler.error;
            }
            catch (UnityWebRequestException e)
            {
                error = $"Error on GLTF download ({downloadUri}): {e.Error} - {e.Message}";
            }
            finally
            {
                if(uri.OriginalString.Contains(".glb"))
                    acquiredBudget.Release();
                success = string.IsNullOrEmpty(error);
                downloadHandler?.Dispose();
            }

            return new GltfDownloadResult
            {
                Data = data,
                Text = text,
                Error = error,
                Success = success,
            };
        }

        public async Task<ITextureDownload> RequestTextureAsync(Uri uri, bool nonReadable, bool forceLinear)
        {
            var downloadUri = new Uri(contentSourceUrl+uri);
            var texturePromise = Promise.Create(world, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(downloadUri.AbsoluteUri, attempts: ATTEMPTS_COUNT),
            }, partitionComponent);

            // The textures fetching need to finish before the GLTF loading can continue its flow...
            Promise promiseResult = await texturePromise.ToUniTaskAsync(world, cancellationToken: new CancellationToken());

            if (promiseResult.Result is { Succeeded: false })
                throw new Exception($"Error on GLTF Texture download: {promiseResult.Result.Value.Exception!.Message}");

            return new TextureDownloadResult(promiseResult.Result?.Asset)
            {
                Error = promiseResult.Result?.Exception?.Message,
                Success = (bool)promiseResult.Result?.Succeeded,
            };
        }
    }
}
