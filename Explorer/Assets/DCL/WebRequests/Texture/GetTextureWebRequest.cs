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

        public override bool Http2Supported => useKtx;

        private bool useKtx => ktxEnabled && Args.UseKtx;

        internal GetTextureWebRequest(RequestEnvelope envelope, GetTextureArguments args, IWebRequestController controller, bool ktxEnabled, IDecentralandUrlsSource urlsSource)
            : base(envelope, args, controller)
        {
            this.ktxEnabled = ktxEnabled;
            this.urlsSource = urlsSource;
        }

        public override UnityWebRequest CreateUnityWebRequest() =>
            UnityWebRequestTexture.GetTexture(GetEffectiveUrl());

        public override HTTPRequest CreateHttp2Request() =>
            new (GetEffectiveUrl());

        public override (HttpRequestMessage, ulong uploadPayloadSize) CreateYetAnotherHttpRequest() =>
            new (new HttpRequestMessage(HttpMethod.Get, GetEffectiveUrl()), 0);

        public static Uri GetEffectiveUrl(IDecentralandUrlsSource urlsSource, Uri imageUrl, bool ktxEnabled) =>
            ktxEnabled
                ? new Uri(string.Format(urlsSource.Url(DecentralandUrl.MediaConverter).OriginalString, Uri.EscapeDataString(imageUrl.OriginalString)))
                : imageUrl;

        private Uri GetEffectiveUrl() =>
            GetEffectiveUrl(urlsSource, Envelope.CommonArguments.URL, useKtx);

        /// <summary>
        ///     Creates the texture
        /// </summary>
        public async UniTask<IOwnedTexture2D> CreateTextureAsync(TextureWrapMode wrapMode, FilterMode filterMode = FilterMode.Point, CancellationToken ct = default)
        {
            using IWebRequest wr = await this.SendAsync(ct);

            string? contentType = wr.Response.GetHeader(WebRequestHeaders.CONTENT_TYPE_HEADER);
            bool isKtx = contentType == "image/ktx2";

            // For simplicity simply switch

            await UniTask.SwitchToMainThread();

            switch (wr.nativeRequest)
            {
                case UnityWebRequest unityWebRequest:
                    if (useKtx)
                        return await ExecuteKtxAsync(unityWebRequest.downloadHandler.nativeData.AsWritableSliceUnsafe(), wrapMode, filterMode, ct);

                    return ExecuteNoCompression(unityWebRequest, wrapMode, filterMode);

                case HTTPRequest:
                case HttpRequestMessage:
                {
                    // Streams are non-linear memory, not much we can do about it to avoid allocations
                    using Stream? stream = await wr.Response.GetCompleteStreamAsync(ct);

                    using var nativeArray = new NativeArray<byte>((int)stream.Length, Allocator.Persistent, NativeArrayOptions.UninitializedMemory);
                    stream.Read(nativeArray.AsSpan());

                    if (isKtx)
                        return await ExecuteKtxAsync(nativeArray, wrapMode, filterMode, ct);

                    // Very inefficient fallback path
                    Texture2D finalTex = Decode(nativeArray.AsReadOnly());
                    ApplyTextureSettings(finalTex, wrapMode, filterMode);
                    return new IOwnedTexture2D.Const(finalTex);
                }

                default:
                    throw new NotSupportedException($"{nameof(CreateTextureAsync)} does not support {wr.GetType().Name})");
            }
        }

        private Texture2D Decode(NativeArray<byte>.ReadOnly data)
        {
            if (data.Length == 0)
                throw new ArgumentException("Texture content is empty", nameof(data));

            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);

            if (!tex.LoadImage(data))
                throw new Exception($"Failed to load image from data: {Envelope.CommonArguments.URL}");

            return tex;
        }

        private IOwnedTexture2D ExecuteNoCompression(UnityWebRequest webRequest, TextureWrapMode wrapMode, FilterMode filterMode)
        {
            Texture2D? texture;

            if (webRequest.downloadHandler is DownloadHandlerTexture)
                texture = DownloadHandlerTexture.GetContent(webRequest);
            else
            {
                // If there's no DownloadHandlerTexture the texture needs to be created from scratch with the
                // downloaded tex data
                texture = Decode(webRequest.downloadHandler.nativeData);
            }

            ApplyTextureSettings(texture, wrapMode, filterMode);
            return new IOwnedTexture2D.Const(texture);
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

            if (finalTex)
                ApplyTextureSettings(finalTex, wrapMode, filterMode);

            return new IOwnedTexture2D.Const(finalTex!);
        }

        private void ApplyTextureSettings(Texture2D texture, TextureWrapMode wrapMode, FilterMode filterMode)
        {
            texture.wrapMode = wrapMode;
            texture.filterMode = filterMode;
            texture.SetDebugName(Envelope.CommonArguments.URL.OriginalString);
            ProfilingCounters.TexturesAmount.Value++;
        }
    }
}
