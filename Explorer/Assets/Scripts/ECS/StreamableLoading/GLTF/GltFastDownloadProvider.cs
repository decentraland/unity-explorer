using Cysharp.Threading.Tasks;
using DCL.WebRequests;
using GLTFast;
using GLTFast.Loading;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Networking;

namespace ECS.StreamableLoading.GLTF
{
    internal class GltFastDownloadProvider : IDownloadProvider, IDisposable
    {
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
        public string Error { get; set; }
        public byte[] Data => Array.Empty<byte>();
        public string Text => string.Empty;
        public bool? IsBinary => true;

        public IDisposableTexture GetTexture(bool forceSampleLinear)
        {
            // TODO...

            throw new NotImplementedException();
        }

        public void Dispose()
        {

        }
    }
}
