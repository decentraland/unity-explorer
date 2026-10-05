using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Translation.Service;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Networking;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     App-wide cache of presentation slide textures keyed by url, holding at most <see cref="CAPACITY" /> and
    ///     destroying the least recently used first. Fetches only contract-shaped slide urls in canonical form (equal to their own
    ///     <see cref="Uri.AbsoluteUri" />): https on the default port, the host of <see cref="DecentralandUrl.CastPresenterService" />,
    ///     no userinfo, query, fragment or backslash, and a path made of an optional prefix of <c>[A-Za-z0-9_-]</c>
    ///     segments followed by <c>/presentations/{uuid}/slides/{16 lowercase hex}.png</c>; in the Editor, loopback http
    ///     urls on any port with the same path too. Fetches once per url while in flight, and backs off after a failure.
    ///     Starts at most one fetch per <see cref="MIN_FETCH_INTERVAL_SECONDS" /> per throttle key, tracking at most
    ///     <see cref="MAX_TRACKED_BOTS" /> keys, tracks at most <see cref="MAX_TRACKED_URLS" /> rejected and
    ///     <see cref="MAX_TRACKED_URLS" /> failed urls, forgetting the least recently tracked first, logs at most
    ///     <see cref="MAX_REJECTION_REPORTS" /> rejections and <see cref="MAX_FAILURE_REPORTS" /> failures in its lifetime,
    ///     and rejects before decoding a body that isn't a PNG of at most <see cref="MAX_SLIDE_BYTES" /> bytes and
    ///     <see cref="PresentationLayout.MAX_SLIDE_SIZE" /> px on either side. Destroys every slide when the last player
    ///     composing from it stops. Does nothing once disposed. Main-thread only.
    /// </summary>
    public sealed class SlideTextureCache : IThrottledClearable, IDisposable
    {
        internal const int CAPACITY = 4;
        internal const float BASE_RETRY_COOLDOWN_SECONDS = 10f;
        internal const float MAX_RETRY_COOLDOWN_SECONDS = 60f;
        internal const float MIN_FETCH_INTERVAL_SECONDS = 0.25f;
        internal const int MAX_TRACKED_URLS = 32;
        internal const int MAX_TRACKED_BOTS = 32;
        internal const int MAX_REJECTION_REPORTS = 8;
        internal const int MAX_FAILURE_REPORTS = 32;
        internal const int MAX_SLIDE_BYTES = 32 * 1024 * 1024;

        private const int PNG_HEADER_LENGTH = 24;

        private static readonly Regex SLIDE_PATH = new (@"^(/[A-Za-z0-9_-]+)*/presentations/[0-9a-f-]{36}/slides/[0-9a-f]{16}\.png\z", RegexOptions.Compiled);
        private static readonly byte[] PNG_SIGNATURE = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0, 0, 0, 13, 0x49, 0x48, 0x44, 0x52 };

        private readonly IWebRequestController webRequestController;
        private readonly string castPresenterHost;
        private readonly Func<float> getRealtimeSinceStartup;
        private readonly LRUCache<string, Texture2D> textures = new (CAPACITY, static (_, texture) => UnityObjectUtils.SafeDestroy(texture));
        private readonly HashSet<string> inFlight = new ();
        private readonly LRUCache<string, bool> rejected = new (MAX_TRACKED_URLS);
        private readonly LRUCache<string, (int attempts, float retryAt)> failed = new (MAX_TRACKED_URLS);
        private readonly LRUCache<string, float> nextFetchAt = new (MAX_TRACKED_BOTS);
        private readonly CancellationTokenSource cts = new ();

        private int rejectionReportsLeft = MAX_REJECTION_REPORTS;
        private int failureReportsLeft = MAX_FAILURE_REPORTS;
        private int composers;
        private bool disposed;

        internal int trackedUrlCount => rejected.Count + failed.Count;

        public SlideTextureCache(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
            : this(webRequestController, decentralandUrlsSource, static () => UnityEngine.Time.realtimeSinceStartup) { }

        internal SlideTextureCache(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource, Func<float> getRealtimeSinceStartup)
        {
            this.webRequestController = webRequestController;
            castPresenterHost = new Uri(decentralandUrlsSource.Url(DecentralandUrl.CastPresenterService)).Host;
            this.getRealtimeSinceStartup = getRealtimeSinceStartup;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            cts.Cancel();
            cts.Dispose();

            Unload();
            inFlight.Clear();
            rejected.Clear();
            failed.Clear();
            nextFetchAt.Clear();
        }

        /// <summary>
        ///     Registers a player that started composing from this cache.
        /// </summary>
        public void BeginComposing() =>
            composers++;

        /// <summary>
        ///     Unregisters a player that stopped composing, and destroys every cached slide when it was the last one.
        /// </summary>
        public void EndComposing()
        {
            if (--composers == 0)
                Unload();
        }

        /// <summary>
        ///     Destroys every cached slide. The cache stays usable and fetches them again on request.
        /// </summary>
        public void Unload() =>
            textures.RemoveOldest(textures.Count);

        /// <summary>
        ///     Destroys up to <paramref name="maxUnloadAmount" /> of the least recently used slides.
        /// </summary>
        public void ClearThrottled(int maxUnloadAmount) =>
            textures.RemoveOldest(maxUnloadAmount);

        /// <summary>
        ///     The cached texture for <paramref name="url" />, or <c>null</c> while it loads, cools down after a
        ///     failure, waits for the fetch interval, or is disallowed. The first call for a fetchable url starts its
        ///     download.
        /// </summary>
        /// <param name="url">The slide url.</param>
        /// <param name="throttleKey">
        ///     Fetch starts are throttled per key; pass the identity of the bot whose metadata named the url.
        /// </param>
        public Texture2D? GetOrRequest(string url, string throttleKey)
        {
            if (disposed)
                return null;

            if (textures.TryGetValue(url, out Texture2D texture))
                return texture;

            if (inFlight.Contains(url) || rejected.ContainsKey(url))
                return null;

            float now = getRealtimeSinceStartup();

            if ((nextFetchAt.TryPeek(throttleKey, out float keyNext) && now < keyNext)
                || (failed.TryPeek(url, out (int attempts, float retryAt) failure) && now < failure.retryAt))
                return null;

            if (!IsAllowedUrl(url))
            {
                Reject(url, "Slide url rejected, origin not allowed");
                return null;
            }

            nextFetchAt.Set(throttleKey, now + MIN_FETCH_INTERVAL_SECONDS);
            inFlight.Add(url);
            LoadAsync(url).Forget();
            return null;
        }

        internal bool IsAllowedUrl(string url)
        {
            if (url.Contains('\\') || !Uri.TryCreate(url, UriKind.Absolute, out Uri? uri))
                return false;

            return uri.UserInfo.Length == 0
                   && uri.Query.Length == 0
                   && uri.Fragment.Length == 0
                   && SLIDE_PATH.IsMatch(uri.AbsolutePath)
                   && IsCastPresenterOrigin(uri)
                   && string.Equals(uri.AbsoluteUri, url, StringComparison.Ordinal);
        }

        /// <summary>
        ///     Decodes a slide body into a clamped, bilinear texture without a CPU copy.
        /// </summary>
        /// <returns>
        ///     <c>null</c>, without decoding, when <paramref name="data" /> isn't a PNG of at most <see cref="MAX_SLIDE_BYTES" />
        ///     bytes whose header declares at most <see cref="PresentationLayout.MAX_SLIDE_SIZE" /> px on either side.
        /// </returns>
        /// <exception cref="InvalidDataException">The PNG fails to decode.</exception>
        internal static Texture2D? DecodeSlide(byte[]? data)
        {
            if (data == null || data.Length > MAX_SLIDE_BYTES || !TryReadPngSize(data, out int width, out int height)
                || width is < 1 or > PresentationLayout.MAX_SLIDE_SIZE || height is < 1 or > PresentationLayout.MAX_SLIDE_SIZE)
                return null;

            var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };

            if (texture.LoadImage(data, markNonReadable: true))
                return texture;

            UnityObjectUtils.SafeDestroy(texture);
            throw new InvalidDataException("Failed to decode slide");
        }

        private static bool TryReadPngSize(byte[] data, out int width, out int height)
        {
            width = height = 0;

            if (data.Length < PNG_HEADER_LENGTH)
                return false;

            for (var i = 0; i < PNG_SIGNATURE.Length; i++)
                if (data[i] != PNG_SIGNATURE[i])
                    return false;

            width = ReadBigEndianInt(data, 16);
            height = ReadBigEndianInt(data, 20);
            return true;
        }

        private static int ReadBigEndianInt(byte[] data, int offset) =>
            (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

        private bool IsCastPresenterOrigin(Uri uri)
        {
#if UNITY_EDITOR
            if (uri.Scheme == Uri.UriSchemeHttp && LoopbackUrls.IsLoopbackHost(uri.Host))
                return true;
#endif

            return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.Equals(uri.Host, castPresenterHost, StringComparison.Ordinal);
        }

        private async UniTaskVoid LoadAsync(string url)
        {
            try
            {
                Texture2D? texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(url), RetryPolicy.DEFAULT),
                    new GetTextureArguments(TextureType.Albedo, useKtx: false, disableRedirects: true),
                    new SlideTextureOp(),
                    cts.Token,
                    ReportCategory.MEDIA_STREAM,
                    suppressErrors: true);

                if (cts.IsCancellationRequested)
                {
                    UnityObjectUtils.SafeDestroy(texture);
                    return;
                }

                if (texture == null)
                {
                    Reject(url, $"Slide rejected, not a PNG of at most {MAX_SLIDE_BYTES} bytes and {PresentationLayout.MAX_SLIDE_SIZE} px per side");
                    return;
                }

                failed.TryRemove(url, out _);
                textures.Set(url, texture);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (RecordFailure(url) == 1 && TryConsumeReport(ref failureReportsLeft))
                    ReportHub.LogException(e, ReportCategory.MEDIA_STREAM);
            }
            finally { inFlight.Remove(url); }
        }

        private void Reject(string url, string message)
        {
            rejected.Set(url, true);

            if (TryConsumeReport(ref rejectionReportsLeft))
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"{message}: {OriginOf(url)}");
        }

        private int RecordFailure(string url)
        {
            int attempts = failed.TryPeek(url, out (int attempts, float retryAt) failure) ? failure.attempts + 1 : 1;
            failed.Set(url, (attempts, getRealtimeSinceStartup() + Mathf.Min(BASE_RETRY_COOLDOWN_SECONDS * attempts, MAX_RETRY_COOLDOWN_SECONDS)));
            return attempts;
        }

        private static bool TryConsumeReport(ref int budget)
        {
            if (budget <= 0)
                return false;

            budget--;
            return true;
        }

        private static string OriginOf(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? $"{uri.Scheme}://{uri.Host}" : "unparseable url";

        internal readonly struct SlideTextureOp : IWebRequestOp<GetTextureWebRequest, Texture2D>
        {
            public UniTask<Texture2D?> ExecuteAsync(GetTextureWebRequest webRequest, CancellationToken ct) =>
                UniTask.FromResult(DecodeSlide(webRequest.UnityWebRequest.downloadHandler?.data));
        }
    }
}
