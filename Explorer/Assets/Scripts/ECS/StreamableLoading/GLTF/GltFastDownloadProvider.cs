using Arch.Core;
using Cysharp.Threading.Tasks;
using ECS.Prioritization.Components;
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
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastDownloadProvider : IDownloadProvider, IDisposable
    {
        public string TargetGltfOriginalPath = string.Empty;

        private ISceneData sceneData;
        private World world;
        private IPartitionComponent partitionComponent;

        public GltFastDownloadProvider(World world, ISceneData sceneData, IPartitionComponent partitionComponent)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
        }

        public async Task<IDownload> Request(Uri uri)
        {
            // TODO: Replace for WebRequestController ???
            using (UnityWebRequest webRequest = new UnityWebRequest(uri))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();

                await webRequest.SendWebRequest().WithCancellation(new CancellationToken());

                if (!string.IsNullOrEmpty(webRequest.downloadHandler.error))
                    throw new Exception($"Error on GLTF download: {webRequest.downloadHandler.error}");

                return new GltfDownloadResult()
                {
                    Data = webRequest.downloadHandler.data,
                    Error = webRequest.downloadHandler.error,
                    Text = webRequest.downloadHandler.text,
                    Success = webRequest.result == UnityWebRequest.Result.Success
                };
            }
        }

        public async Task<ITextureDownload> RequestTexture(Uri uri, bool nonReadable, bool forceLinear)
        {
            string textureFileName = uri.OriginalString.Substring(uri.OriginalString.LastIndexOf('/')+1);
            string textureOriginalPath = string.Concat(TargetGltfOriginalPath.Remove(TargetGltfOriginalPath.LastIndexOf('/') + 1), textureFileName);

            sceneData.SceneContent.TryGetContentUrl(textureOriginalPath, out var tryGetContentUrlResult);

            var texturePromise = Promise.Create(world, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(tryGetContentUrlResult, attempts: 6),
                // WrapMode = textureComponentValue.WrapMode,
                // FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);

            // The textures fetching need to finish before the GLTF loading can continue its flow...
            StreamableLoadingResult<Texture2D> promiseResult;
            //TODO: Could this get stuck in an infinite loop if the texturePromise fails?
            while (!texturePromise.TryGetResult(world, out promiseResult))
            {
                await UniTask.Yield();
            }

            // TODO: Check if we need to avoid this throwing here depending on how it affects the GLTF loading flow...
            if (!promiseResult.Succeeded)
                throw new Exception($"Error on GLTF Texture download: {texturePromise.Result?.Exception?.Message}");

            return new TextureDownloadResult(promiseResult.Asset)
            {
                Error = promiseResult.Exception?.Message,
                Success = promiseResult.Succeeded
            };
        }

        public void Dispose()
        {
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
            get {
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
            Texture = new DisposableTexture() { Texture = texture };
            Error = null!;
            Success = false;
        }

        public IDisposableTexture GetTexture(bool forceSampleLinear) =>
            Texture;

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
