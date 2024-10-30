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
using System.Threading;
using System.Threading.Tasks;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastDownloadProvider : IDownloadProvider, IDisposable
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly IAcquiredBudget acquiredBudget;
        private readonly string targetGltfOriginalPath;
        private readonly ISceneData sceneData;
        private readonly World world;
        private readonly IPartitionComponent partitionComponent;
        private readonly IWebRequestController webRequestController;
        private readonly ReportData reportData;

        public GltFastDownloadProvider(World world, ISceneData sceneData, IPartitionComponent partitionComponent, string targetGltfOriginalPath, ReportData reportData,
            IWebRequestController webRequestController, IAcquiredBudget acquiredBudget)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
            this.targetGltfOriginalPath = targetGltfOriginalPath;
            this.reportData = reportData;
            this.webRequestController = webRequestController;
            this.acquiredBudget = acquiredBudget;
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
            bool isBaseGltfFetch = uri.OriginalString.Equals(targetGltfOriginalPath);
            string originalFilePath = GetFileOriginalPathFromUri(uri);

            if (!sceneData.SceneContent.TryGetContentUrl(originalFilePath, out URLAddress tryGetContentUrlResult))
            {
                if (isBaseGltfFetch) acquiredBudget.Release();
                throw new Exception($"Error on GLTF download ({targetGltfOriginalPath} - {originalFilePath}): NOT FOUND");
            }

            uri = new Uri(tryGetContentUrlResult);
            var commonArguments = new CommonArguments(URLAddress.FromString(GetUrl(uri)));

            byte[] data = Array.Empty<byte>();
            string error = string.Empty;
            string text = string.Empty;
            bool success;

            try
            {
                var downloadHandler = await webRequestController.GetAsync(commonArguments, new CancellationToken(), reportData).ExposeDownloadHandlerAsync();
                data = downloadHandler.data;

                if (!GltfValidator.IsGltfBinaryFormat(downloadHandler.nativeData))
                    text = downloadHandler.text;

                error = downloadHandler.error;
            }
            catch (UnityWebRequestException e)
            {
                error = $"Error on GLTF download ({targetGltfOriginalPath} - {uri}): {e.Error} - {e.Message}";
            }
            finally
            {
                if (isBaseGltfFetch) acquiredBudget.Release();
                success = string.IsNullOrEmpty(error);
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
            string textureOriginalPath = GetFileOriginalPathFromUri(uri);
            sceneData.SceneContent.TryGetContentUrl(textureOriginalPath, out URLAddress tryGetContentUrlResult);

            var texturePromise = Promise.Create(world, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(tryGetContentUrlResult, attempts: ATTEMPTS_COUNT),
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

        private string GetFileOriginalPathFromUri(Uri uri)
        {
            // On windows the URI may come with some invalid '\' in parts of the path
            string patchedUri = uri.OriginalString.Replace('\\', '/');
            return patchedUri.Replace(sceneData.SceneContent.ContentBaseUrl.Value, string.Empty);
        }
    }
}
