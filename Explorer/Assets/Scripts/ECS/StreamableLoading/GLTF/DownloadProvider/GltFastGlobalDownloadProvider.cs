using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Diagnostics;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using System;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF.DownloadProvider
{
    internal class GltFastGlobalDownloadProvider : GltFastDownloadProviderBase
    {
        private readonly string contentSourceUrl;

        public GltFastGlobalDownloadProvider(
            World world,
            string contentSourceUrl,
            IPartitionComponent partitionComponent,
            ReportData reportData,
            IWebRequestController webRequestController,
            IAcquiredBudget acquiredBudget)
            : base(world, partitionComponent, reportData, webRequestController, acquiredBudget)
        {
            this.contentSourceUrl = contentSourceUrl;
        }

        protected override Uri GetDownloadUri(Uri uri)
        {
            return new Uri(contentSourceUrl + uri);
        }

        protected override string GetErrorMessage(Uri downloadUri, UnityWebRequestException e)
        {
            return $"Error on GLTF download ({downloadUri}): {e.Error} - {e.Message}";
        }

        protected override bool ShouldReleaseBudget(Uri uri)
        {
            return uri.OriginalString.Contains(".glb");
        }

        protected override string GetTextureErrorMessage(Promise promiseResult)
        {
            return $"Error on GLTF Texture download: {promiseResult.Result.Value.Exception!.Message}";
        }
    }
}
