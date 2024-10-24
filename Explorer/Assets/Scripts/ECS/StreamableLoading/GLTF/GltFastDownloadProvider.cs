using Arch.Core;
using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using GLTFast;
using GLTFast.Loading;
using SceneRunner.Scene;
using System;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
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

        public GltFastDownloadProvider(World world, ISceneData sceneData, IPartitionComponent partitionComponent, string targetGltfOriginalPath, IAcquiredBudget acquiredBudget)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
            this.targetGltfOriginalPath = targetGltfOriginalPath;
            this.acquiredBudget = acquiredBudget;
        }

        public void Dispose()
        {
            acquiredBudget.Release();
        }

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

            // TODO: Replace for WebRequestController (Planned in PR #1670)
            using (var webRequest = new UnityWebRequest(uri))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();

                try { await webRequest.SendWebRequest().WithCancellation(new CancellationToken()); }
                catch { throw new Exception($"Error on GLTF download ({targetGltfOriginalPath} - {uri}): {webRequest.downloadHandler.error} - {webRequest.downloadHandler.text}"); }
                finally { if (isBaseGltfFetch) acquiredBudget.Release(); }

                return new GltfDownloadResult
                {
                    Data = webRequest.downloadHandler.data,
                    Error = webRequest.downloadHandler.error,
                    Text = webRequest.downloadHandler.text,
                    Success = webRequest.result == UnityWebRequest.Result.Success,
                };
            }
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

    public struct GltfDownloadResult : IDownload
    {
        private const uint GLB_SIGNATURE = 0x46546c67;

        public bool Success { get; set; }
        public string Error { get; set; }
        public byte[] Data { get; set; }
        public string Text { get; set; }

        public bool? IsBinary
        {
            get
            {
                if (Data == null) return false;
                var gltfBinarySignature = BitConverter.ToUInt32(Data, 0);
                return gltfBinarySignature == GLB_SIGNATURE;
            }
        }

        public void Dispose()
        {
            Data = null!;
        }
    }

    public struct TextureDownloadResult : ITextureDownload
    {
        public bool Success { get; set; }
        public string? Error { get; set; }
        public byte[] Data => Array.Empty<byte>();
        public string Text => string.Empty;
        public bool? IsBinary => true;
        public readonly IDisposableTexture Texture;

        public TextureDownloadResult(Texture2D? texture)
        {
            Texture = new DisposableTexture { Texture = texture };
            Error = null!;
            Success = false;
        }

        public IDisposableTexture GetTexture(bool forceSampleLinear) => Texture;

        public void Dispose() => Texture.Dispose();
    }

    public struct DisposableTexture : IDisposableTexture
    {
        public Texture2D? Texture { get; set; }

        public void Dispose()
        {
            // TODO: if we enable texture destruction on disposal, the external-fetched textures get destroyed before
            // the GLTF finishes loading... investigate why...
            // if (Texture != null)
            //     Object.Destroy(Texture);
        }
    }
}
