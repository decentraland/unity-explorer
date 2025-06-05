using Best.HTTP;
using Best.HTTP.Response;
using Cysharp.Threading.Tasks;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Profiling;
using KtxUnity;
using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Networking;
using Utility;

namespace DCL.WebRequests
{
    /// <summary>
    ///     Representation of the created web request dedicated to download a texture
    /// </summary>
    public class GetTextureWebRequest : TypedWebRequestBase<GetTextureArguments>
    {
        private readonly bool ktxEnabled;
        private readonly IDecentralandUrlsSource urlsSource;

        internal GetTextureWebRequest(RequestEnvelope envelope, GetTextureArguments args, IWebRequestController controller, bool ktxEnabled, IDecentralandUrlsSource urlsSource)
            : base(envelope, args, controller)
        {
            this.ktxEnabled = ktxEnabled;
            this.urlsSource = urlsSource;
        }

        public override bool Http2Supported => useKtx;

        private bool useKtx => ktxEnabled && Args.UseKtx;

        public override UnityWebRequest CreateUnityWebRequest() =>
            UnityWebRequestTexture.GetTexture(GetEffectiveUrl());

        public override HTTPRequest CreateHttp2Request() =>
            new (GetEffectiveUrl());

        public override HttpRequestMessage CreateYetAnotherHttpRequest() =>
            new (HttpMethod.Get, GetEffectiveUrl());

        private Uri GetEffectiveUrl() =>
            useKtx
                ? new Uri(string.Format(urlsSource.Url(DecentralandUrl.MediaConverter).OriginalString, Uri.EscapeDataString(Envelope.CommonArguments.URL.OriginalString)))
                : Envelope.CommonArguments.URL;

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public async UniTask<IOwnedTexture2D> CreateTextureAsync(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point, CancellationToken ct = default)
        {
            using IWebRequest wr = await this.SendAsync(ct);

            // For simplicity simply switch

            await UniTask.SwitchToMainThread();

            switch (wr.nativeRequest)
            {
                case UnityWebRequest unityWebRequest:
                    if (useKtx)
                        return await ExecuteKtxAsync(unityWebRequest.downloadHandler.nativeData.AsWritableSliceUnsafe(), wrapMode, filterMode, ct);

                    return await ExecuteNoCompressionAsync(unityWebRequest, wrapMode, filterMode, ct);

                case HTTPRequest when useKtx:
                case HttpRequestMessage when useKtx:
                {
                    // Streams are non-linear memory, not much we can do about it to avoid allocations
                    using Stream? stream = await wr.Response.GetCompleteStreamAsync(ct);

                    using var nativeArray = new NativeArray<byte>((int)stream.Length, Allocator.Temp, NativeArrayOptions.UninitializedMemory);
                    stream.Read(nativeArray.AsSpan());

                    return await ExecuteKtxAsync(nativeArray, wrapMode, filterMode, ct);
                }

                default:
                    throw new NotSupportedException($"{nameof(CreateTextureAsync)} does not support {wr.GetType().Name})");
            }
        }

        private UniTask<IOwnedTexture2D> ExecuteNoCompressionAsync(UnityWebRequest webRequest, TextureWrapMode wrapMode, FilterMode filterMode, CancellationToken ct)
        {
            Texture2D? texture;

            if (webRequest.downloadHandler is DownloadHandlerTexture)
                texture = DownloadHandlerTexture.GetContent(webRequest);
            else
            {
                // If there's no DownloadHandlerTexture the texture needs to be created from scratch with the
                // downloaded tex data
                byte[]? data = webRequest.downloadHandler?.data;

                if (data == null)
                    throw new Exception("Texture content is empty");

                texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);

                if (!texture.LoadImage(data)) { throw new Exception($"Failed to load image from data: {webRequest.url}"); }
            }

            texture.wrapMode = wrapMode;
            texture.filterMode = filterMode;
            texture.SetDebugName(webRequest.url);
            ProfilingCounters.TexturesAmount.Value++;
            return UniTask.FromResult((IOwnedTexture2D)new IOwnedTexture2D.Const(texture));
        }

        private async UniTask<IOwnedTexture2D> ExecuteKtxAsync(NativeSlice<byte> data, TextureWrapMode wrapMode, FilterMode filterMode, CancellationToken ct)
        {
            var ktxTexture = new KtxTexture();

            TextureResult? result = await ktxTexture.LoadFromBytes(
                data,
                Args.TextureType != TextureType.Albedo
            );

            if (result == null)
                throw new Exception($"Failed to load ktx texture from data: {Envelope.CommonArguments.URL}");

            Texture2D? finalTex = result.texture;

            finalTex.wrapMode = wrapMode;
            finalTex.filterMode = filterMode;
            finalTex.SetDebugName(Envelope.CommonArguments.URL.OriginalString);
            ProfilingCounters.TexturesAmount.Value++;
            return new IOwnedTexture2D.Const(finalTex);
        }
    }
}
