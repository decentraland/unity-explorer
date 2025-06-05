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
using System;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine.Networking;
using Promise = ECS.StreamableLoading.Common.AssetPromise<ECS.StreamableLoading.Textures.Texture2DData, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF.DownloadProvider
{
    internal abstract class GltFastDownloadProviderBase : IGLTFastDisposableDownloadProvider
    {
        protected const int ATTEMPTS_COUNT = 6;

        protected readonly IAcquiredBudget acquiredBudget;
        protected readonly World world;
        protected readonly IPartitionComponent partitionComponent;
        protected readonly IWebRequestController webRequestController;
        protected readonly ReportData reportData;

        protected GltFastDownloadProviderBase(World world, IPartitionComponent partitionComponent, ReportData reportData, IWebRequestController webRequestController, IAcquiredBudget acquiredBudget)
        {
            this.world = world;
            this.partitionComponent = partitionComponent;
            this.reportData = reportData;
            this.webRequestController = webRequestController;
            this.acquiredBudget = acquiredBudget;
        }

        public void Dispose()
        {
            acquiredBudget.Release();
        }

        protected static string GetUrl(Uri uri) =>
            (uri.IsAbsoluteUri ? uri.AbsoluteUri : uri.ToString()) ?? string.Empty;

        // RequestAsync is used for fetching the GLTF file itself + some external textures. Whenever this
        // method's request of the base GLTF is finished, the propagated budget for assets loading must be released.
        public async Task<IDownload> RequestAsync(Uri uri)
        {
            var downloadUri = GetDownloadUri(uri);
            var commonArguments = new CommonArguments(URLAddress.FromString(GetUrl(downloadUri)));

            byte[] data = Array.Empty<byte>();
            string error = string.Empty;
            string text = string.Empty;
            bool success = false;

            try
            {
                data = await webRequestController.GetAsync(commonArguments, reportData).GetDataCopyAsync(CancellationToken.None);

                if (!GltfValidator.IsGltfBinaryFormat(data))
                    text = Encoding.UTF8.GetString(data);

                success = true;
            }
            catch (WebRequestException e)
            {
                error = GetErrorMessage(downloadUri, e);
            }
            finally
            {
                if (ShouldReleaseBudget(uri))
                    acquiredBudget.Release();
                success = string.IsNullOrEmpty(error);
            }

            return new GltfDownloadResult(data, text, error, success);
        }

        public async Task<ITextureDownload> RequestTextureAsync(Uri uri, bool nonReadable, bool forceLinear)
        {
            var downloadUri = GetDownloadUri(uri);
            var texturePromise = Promise.Create(world, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(downloadUri, attempts: ATTEMPTS_COUNT),
            }, partitionComponent);

            // The textures fetching need to finish before the GLTF loading can continue its flow...
            Promise promiseResult = await texturePromise.ToUniTaskAsync(world, cancellationToken: new CancellationToken());

            if (promiseResult.Result is { Succeeded: false })
                throw new Exception(GetTextureErrorMessage(promiseResult));

            return new TextureDownloadResult(promiseResult.Result?.Asset)
            {
                Error = promiseResult.Result?.Exception?.Message,
                Success = (bool)promiseResult.Result?.Succeeded,
            };
        }

        protected abstract Uri GetDownloadUri(Uri uri);

        protected abstract string GetErrorMessage(Uri downloadUri, WebRequestException e);
        protected abstract bool ShouldReleaseBudget(Uri uri);
        protected abstract string GetTextureErrorMessage(Promise promiseResult);
    }
}
