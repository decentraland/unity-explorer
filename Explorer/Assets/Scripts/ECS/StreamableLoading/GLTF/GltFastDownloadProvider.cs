using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
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
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastDownloadProvider : IDownloadProvider, IDisposable
    {
        private const int ATTEMPTS_COUNT = 6;

        private string targetGltfOriginalPath;
        private string targetGltfDirectoryPath;
        private ISceneData sceneData;
        private World world;
        private IPartitionComponent partitionComponent;
        private readonly IWebRequestController webRequestController;
        private ReportData reportData;

        public GltFastDownloadProvider(World world, ISceneData sceneData, IPartitionComponent partitionComponent, string targetGltfOriginalPath, ReportData reportData, IWebRequestController webRequestController)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
            this.targetGltfOriginalPath = targetGltfOriginalPath;
            this.reportData = reportData;
            this.webRequestController = webRequestController;
            targetGltfDirectoryPath = targetGltfOriginalPath.Remove(targetGltfOriginalPath.LastIndexOf('/') + 1);
        }

        private static string GetUrl(Uri uri) => (uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString()) ?? string.Empty;

        public async Task<IDownload> RequestAsync(Uri uri)
        {
            string originalFilePath = string.Concat(targetGltfDirectoryPath, GetFileNameFromUri(uri));

            if (!sceneData.SceneContent.TryGetContentUrl(originalFilePath, out var tryGetContentUrlResult))
                throw new Exception($"Error on GLTF download ({targetGltfOriginalPath} - {uri}): NOT FOUND");

            uri = new Uri(tryGetContentUrlResult);
            var commonArguments = new CommonArguments(URLAddress.FromString(GetUrl(uri)));

            byte[] data = Array.Empty<byte>();
            string error = string.Empty;
            string text = string.Empty;
            bool success = true;

            try
            {
                var downloadHandler = await webRequestController.GetAsync(commonArguments, new CancellationToken(), reportData).ExposeDownloadHandlerAsync();
                data = downloadHandler.data;
                if(!GltfValidator.IsGltfBinaryFormat(downloadHandler.nativeData))
                    text = downloadHandler.text;
            }
            catch (UnityWebRequestException e)
            {
                error = e.Error;
                success = false;
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
            string textureOriginalPath = string.Concat(targetGltfDirectoryPath, GetFileNameFromUri(uri));
            sceneData.SceneContent.TryGetContentUrl(textureOriginalPath, out var tryGetContentUrlResult);

            var texturePromise = Promise.Create(world, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(tryGetContentUrlResult, attempts: ATTEMPTS_COUNT),
            }, partitionComponent);

            // The textures fetching need to finish before the GLTF loading can continue its flow...
            var promiseResult = await texturePromise.ToUniTaskAsync(world, cancellationToken: new CancellationToken());

            if (promiseResult.Result is { Succeeded: false })
                throw new Exception($"Error on GLTF Texture download: {promiseResult.Result.Value.Exception!.Message}");

            return new TextureDownloadResult(promiseResult.Result?.Asset)
            {
                Error = promiseResult.Result?.Exception?.Message,
                Success = (bool)promiseResult.Result?.Succeeded,
            };
        }

        public void Dispose()
        {
        }

        private string GetFileNameFromUri(Uri uri)
        {
            // On windows the URI may come with some invalid '\' in parts of the path
            string patchedUri = uri.OriginalString.Replace('\\', '/');
            return patchedUri.Substring(patchedUri.LastIndexOf('/') + 1);
        }
    }
}
