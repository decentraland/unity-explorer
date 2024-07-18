using Arch.Core;
using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using ECS.Prioritization.Components;
using ECS.StreamableLoading.Common.Components;
using ECS.StreamableLoading.Textures;
using GLTFast;
using GLTFast.Loading;
using SceneRunner.Scene;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;
using Object = UnityEngine.Object;
using Promise = ECS.StreamableLoading.Common.AssetPromise<UnityEngine.Texture2D, ECS.StreamableLoading.Textures.GetTextureIntention>;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastDownloadProvider : IDownloadProvider, IDisposable
    {
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
            // var asyncOp = await webRequestController.GetAsync(
            //     url: uri,
            //     downloadHandler: new DownloadHandlerBuffer(),
            //     timeout: 30,
            //     requestAttemps: 3);

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
            Debug.Log($"PRAVS - GltFastDownloadProvider.RequestTexture() -> \n"
                      + $"uri.AbsoluteUri: {uri.AbsoluteUri};\n"
                      + $"uri.Host: {uri.Host};\n"
                      + $"uri.AbsolutePath: {uri.AbsolutePath};\n"
                      + $"uri.OriginalString: {uri.OriginalString};\n"
                      + $"uri.LocalPath: {uri.LocalPath};\n"
                      + $"uri.Fragment: {uri.Fragment};\n"
                      + $"uri.Query: {uri.Query};\n"
                      + $"uri.PathAndQuery: {uri.PathAndQuery}");

            var texturePromise = Promise.Create(world, new GetTextureIntention
            {
                CommonArguments = new CommonLoadingArguments(uri.AbsoluteUri, attempts: 6),
                // WrapMode = textureComponentValue.WrapMode,
                // FilterMode = textureComponentValue.FilterMode,
            }, partitionComponent);

            // The textures fetching need to finish before the GLTF loading can continue its flow...
            while (texturePromise.Result.Value is {Succeeded: false, Exception: null })
            {
                await UniTask.Yield();
            }

            // TODO: Check if we need to avoid this throwing here depending on how it affects the GLTF loading flow...
            if (!texturePromise.Result.Value.Succeeded)
                throw new Exception($"Error on GLTF Texture download: {texturePromise.Result.Value.Exception?.Message}");

            return new TextureDownloadResult(texturePromise.Result.Value.Asset)
            {
                Error = texturePromise.Result.Value.Exception?.Message,
                Success = texturePromise.Result.Value.Succeeded
            };

//             if (isDisposed)
//                 return null;
//
//             string finalUrl = GetFinalUrl(uri, true);
//
//             var promise = new AssetPromise_Texture(
//                 finalUrl,
//                 storeTexAsNonReadable: nonReadable,
//                 overrideMaxTextureSize: DataStore.i.textureConfig.gltfMaxSize.Get(),
//                 overrideCompression:
// #if UNITY_WEBGL
//                 true
// #else
//                 false
// #endif
//               , linear: forceLinear
//             );
//
//             var wrapper = new GLTFastTexturePromiseWrapper(texturePromiseKeeper, promise);
//             disposables.Add(wrapper);
//
//             Exception promiseException = null;
//             promise.OnFailEvent += (_,e) => promiseException = e;
//
//             texturePromiseKeeper.Keep(promise);
//             await promise;
//
//             if (!wrapper.Success)
//             {
//                 string errorMessage = promiseException != null ? promiseException.Message : wrapper.Error;
//                 Debug.LogError($"[GLTFast Texture WebRequest Failed] Error: {errorMessage} | url: " + finalUrl);
//             }
//
//             return wrapper;
            return default;
        }

        public void Dispose()
        {
            // foreach (IDisposable disposable in disposables) { disposable.Dispose(); }
            // disposables.Clear();
            // isDisposed = true;
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
        public IDisposableTexture Texture;

        public TextureDownloadResult(Texture2D? texture)
        {
            Texture = new DisposableTexture() { Texture = texture};
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
            if (Texture != null)
                Object.Destroy(Texture);
        }
    }
}
