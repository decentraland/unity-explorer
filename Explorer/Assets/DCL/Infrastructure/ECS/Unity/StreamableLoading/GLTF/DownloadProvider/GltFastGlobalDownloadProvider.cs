using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Diagnostics;
using DCL.Ipfs;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using System;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.TextureData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF.DownloadProvider
{
    internal class GltFastGlobalDownloadProvider : GltFastDownloadProviderBase
    {
        private readonly string contentSourceUrl;

        private ContentDefinition[]? contentMappings;

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

        public override void SetContentMappings(ContentDefinition[] contentMappings)
        {
            // Could consider making a copy, but right now it would be extra garbage for no reason
            this.contentMappings = contentMappings;
        }

        protected override Uri GetDownloadUri(Uri uri)
        {
            return TryGetContentHash(uri, out string? hash)
                ? new Uri(contentSourceUrl + hash)
                : new Uri(contentSourceUrl + uri.OriginalString);
        }

        private bool TryGetContentHash(Uri uri, out string? hash)
        {
            if (contentMappings == null)
            {
                hash = string.Empty;
                return false;
            }

            foreach (var mapping in contentMappings)
                // We use EndsWith to bypass 'male/' and 'female/' prefixes in wearables
                // The GLB assets do not make that distinction when referencing external assets such as textures
                if (mapping.file.EndsWith(uri.OriginalString, StringComparison.OrdinalIgnoreCase))
                {
                    hash = mapping.hash;
                    return true;
                }

            hash = string.Empty;
            return false;
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
