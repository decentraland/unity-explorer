using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using SceneRunner.Scene;
using System;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF.DownloadProvider
{
    internal class GltFastSceneDownloadProvider : GltFastDownloadProviderBase
    {
        private const int ATTEMPTS_COUNT = 6;

        private readonly string targetGltfOriginalPath;
        private readonly ISceneData sceneData;

        public GltFastSceneDownloadProvider(World world, ISceneData sceneData, IPartitionComponent partitionComponent, string targetGltfOriginalPath, ReportData reportData,
            IWebRequestController webRequestController, IAcquiredBudget acquiredBudget)
            : base(world, partitionComponent, reportData, webRequestController, acquiredBudget)
        {
            this.sceneData = sceneData;
            this.targetGltfOriginalPath = targetGltfOriginalPath;
        }

        protected override Uri GetDownloadUri(Uri uri)
        {
            bool isBaseGltfFetch = uri.OriginalString.Equals(targetGltfOriginalPath);
            string originalFilePath = GetFileOriginalPathFromUri(uri);

            if (!sceneData.SceneContent.TryGetContentUrl(originalFilePath, out Uri tryGetContentUrlResult))
            {
                if (isBaseGltfFetch) acquiredBudget.Release();
                throw new Exception($"Error on GLTF download ({targetGltfOriginalPath} - {originalFilePath}): NOT FOUND");
            }

            return tryGetContentUrlResult;
        }

        protected override string GetErrorMessage(Uri downloadUri, WebRequestException e)
        {
            return $"Error on GLTF download ({targetGltfOriginalPath} - {downloadUri}): {e.Error} - {e.Message}";
        }

        protected override bool ShouldReleaseBudget(Uri uri)
        {
            return uri.OriginalString.Equals(targetGltfOriginalPath);
        }

        protected override string GetTextureErrorMessage(Promise promiseResult)
        {
            return $"Error on GLTF Texture download: {promiseResult.Result.Value.Exception!.Message}";
        }

        private string GetFileOriginalPathFromUri(Uri uri)
        {
            // On windows the URI may come with some invalid '\' in parts of the path
            string patchedUri = uri.OriginalString.Replace('\\', '/');
            return patchedUri.Replace(sceneData.SceneContent.ContentBaseUrl.Value, string.Empty);
        }
    }
}
