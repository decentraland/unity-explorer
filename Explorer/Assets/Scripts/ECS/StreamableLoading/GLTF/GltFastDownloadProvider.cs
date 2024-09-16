using Arch.Core;
using Cysharp.Threading.Tasks;
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
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastDownloadProvider : IDownloadProvider, IDisposable
    {

        private string targetGltfOriginalPath = string.Empty;
        private ISceneData sceneData;
        private World world;
        private IPartitionComponent partitionComponent;
        private const int ATTEMPTS_COUNT = 6;

        public GltFastDownloadProvider(World world, ISceneData sceneData, IPartitionComponent partitionComponent, string targetGltfOriginalPath)
        {
            this.world = world;
            this.sceneData = sceneData;
            this.partitionComponent = partitionComponent;
            this.targetGltfOriginalPath = targetGltfOriginalPath;
        }

        public async Task<IDownload> RequestAsync(Uri uri)
        {
            // Debug.Log($"PRAVS - RequestAsync() "
            //           + $"-> ContentBaseUrl: {sceneData.SceneContent.ContentBaseUrl};"
            //           + $"-> URI: {uri};"
            //           + $"-> TargetGltfOriginalPath: {targetGltfOriginalPath};");

            // TODO: Replace for WebRequestController (Planned in PR #1670)
            using (UnityWebRequest webRequest = new UnityWebRequest(uri))
            {
                webRequest.downloadHandler = new DownloadHandlerBuffer();

                try { await webRequest.SendWebRequest().WithCancellation(new CancellationToken()); }
                catch { throw new Exception($"Error on GLTF download: {webRequest.downloadHandler.error} - {webRequest.downloadHandler.text}"); }

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
            string textureFileName = uri.OriginalString.Substring(uri.OriginalString.LastIndexOf('/')+1);
            string textureOriginalPath = string.Concat(targetGltfOriginalPath.Remove(targetGltfOriginalPath.LastIndexOf('/') + 1), textureFileName);

            // Debug.Log($"PRAVS - RequestTextureAsync() "
            //           + $"-> ContentBaseUrl: {sceneData.SceneContent.ContentBaseUrl};"
            //           + $"-> URI: {uri};"
            //           + $"-> TargetGltfOriginalPath: {targetGltfOriginalPath}; "
            //           + $"-> fileName: {textureFileName}; "
            //           + $"-> originalPath: {textureOriginalPath}; "
            //           );

            sceneData.SceneContent.TryGetContentUrl(textureOriginalPath, out var tryGetContentUrlResult);

            // Debug.Log($"PRAVS - RequestTextureAsync() -> final URL: {tryGetContentUrlResult}");

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
