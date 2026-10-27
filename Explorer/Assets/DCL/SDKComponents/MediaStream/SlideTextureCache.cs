using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.Optimization.Pools;
using DCL.Translation.Service;
using DCL.Utility.Types;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;
using Utility;
using Utility.Networking;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Why <see cref="SlideTextureCache.GetOrRequest" /> has no texture for a url.
    /// </summary>
    public enum SlideState
    {
        /// <summary>The cache is disposed.</summary>
        Disposed,

        /// <summary>The slide is downloading; the first request for a fetchable url starts the download.</summary>
        Loading,

        /// <summary>The throttle key already started a fetch within <see cref="SlideTextureCache.MIN_FETCH_INTERVAL_SECONDS" />.</summary>
        Throttled,

        /// <summary>The last fetch failed and its retry cooldown hasn't elapsed.</summary>
        CoolingDown,

        /// <summary>The url is disallowed, its body was out of limits, or it failed <see cref="SlideTextureCache.MAX_FETCH_ATTEMPTS" /> fetches.</summary>
        Rejected,
    }

    /// <summary>
    ///     App-wide cache of presentation slide textures keyed by url, holding at most <see cref="CAPACITY" /> and
    ///     destroying the least recently used first. Fetches only contract-shaped slide urls in canonical form (equal to their own
    ///     <see cref="Uri.AbsoluteUri" />): https on the default port, the host of <see cref="DecentralandUrl.CastPresenterService" />,
    ///     no userinfo, query, fragment or backslash, and a path made of an optional prefix of <c>[A-Za-z0-9_-]</c>
    ///     segments followed by <c>/presentations/{uuid}/slides/{16 lowercase hex}.png</c>; in the Editor, loopback http
    ///     urls on any port with the same path too. Fetches once per url while in flight, backs off after a failure, and
    ///     rejects a url after <see cref="MAX_FETCH_ATTEMPTS" /> failed fetches.
    ///     Starts at most one fetch per <see cref="MIN_FETCH_INTERVAL_SECONDS" /> per throttle key, tracking at most
    ///     <see cref="MAX_TRACKED_BOTS" /> keys, tracks at most <see cref="MAX_TRACKED_URLS" /> rejected and
    ///     <see cref="MAX_TRACKED_URLS" /> failed urls, forgetting the least recently tracked first, and logs at most
    ///     <see cref="MAX_REJECTION_REPORTS" /> rejections and <see cref="MAX_FAILURE_REPORTS" /> failures in its lifetime.
    ///     Slides decode on a worker thread into non-readable textures; a decoded slide whose body is larger than
    ///     <see cref="MAX_SLIDE_BYTES" /> bytes or whose size exceeds <see cref="PresentationBotMetadata.MAX_SLIDE_SIZE" />
    ///     px on either side is destroyed and its url rejected. Destroys every slide when the last composing lease is
    ///     disposed. Does nothing once disposed. Main-thread only.
    /// </summary>
    public sealed class SlideTextureCache : IThrottledClearable, IDisposable
    {
        internal const int CAPACITY = 4;
        internal const float BASE_RETRY_COOLDOWN_SECONDS = 10f;
        internal const float MAX_RETRY_COOLDOWN_SECONDS = 60f;
        internal const float MIN_FETCH_INTERVAL_SECONDS = 0.25f;
        internal const int MAX_FETCH_ATTEMPTS = 5;
        internal const int MAX_TRACKED_URLS = 32;
        internal const int MAX_TRACKED_BOTS = 32;
        internal const int MAX_REJECTION_REPORTS = 8;
        internal const int MAX_FAILURE_REPORTS = 32;
        internal const int MAX_SLIDE_BYTES = 32 * 1024 * 1024;
        internal const int REQUEST_TIMEOUT_SECONDS = 15;

        private static readonly Regex SLIDE_PATH = new (@"^(/[A-Za-z0-9_-]+)*/presentations/[0-9a-f-]{36}/slides/[0-9a-f]{16}\.png\z", RegexOptions.Compiled);

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
        private int openLeases;
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
        ///     Opens a composing lease. While any lease is open, <see cref="ClearThrottled" /> keeps one most recently used
        ///     slide per lease and completed fetches are cached; disposing the last open lease destroys every cached slide.
        /// </summary>
        /// <returns>The lease; disposing it more than once has no further effect.</returns>
        public IDisposable BeginComposing()
        {
            openLeases++;
            return new ComposingLease(this);
        }

        /// <summary>
        ///     Destroys every cached slide. The cache stays usable and fetches them again on request.
        /// </summary>
        public void Unload() =>
            textures.RemoveOldest(textures.Count);

        /// <summary>
        ///     Marks <paramref name="url" /> as most recently used, so <see cref="ClearThrottled" /> keeps it.
        /// </summary>
        public void KeepAlive(string url) =>
            textures.TryGetValue(url, out _);

        /// <summary>
        ///     Destroys up to <paramref name="maxUnloadAmount" /> of the least recently used slides, keeping one most
        ///     recently used slide per open composing lease.
        /// </summary>
        public void ClearThrottled(int maxUnloadAmount) =>
            textures.RemoveOldest(Math.Min(maxUnloadAmount, Math.Max(0, textures.Count - openLeases)));

        /// <summary>
        ///     The cached texture for <paramref name="url" />, or the <see cref="SlideState" /> explaining why there is none.
        ///     The first request for a fetchable url starts its download.
        /// </summary>
        /// <param name="url">The slide url.</param>
        /// <param name="throttleKey">
        ///     Fetch starts are throttled per key; pass the identity of the bot whose metadata named the url.
        /// </param>
        public EnumResult<Texture2D, SlideState> GetOrRequest(string url, string throttleKey)
        {
            if (disposed)
                return Missing(SlideState.Disposed);

            if (textures.TryGetValue(url, out Texture2D texture))
                return EnumResult<Texture2D, SlideState>.SuccessResult(texture);

            if (rejected.ContainsKey(url))
                return Missing(SlideState.Rejected);

            if (inFlight.Contains(url))
                return Missing(SlideState.Loading);

            float now = getRealtimeSinceStartup();

            if (failed.TryPeek(url, out (int attempts, float retryAt) failure) && now < failure.retryAt)
                return Missing(SlideState.CoolingDown);

            if (nextFetchAt.TryPeek(throttleKey, out float keyNext) && now < keyNext)
                return Missing(SlideState.Throttled);

            if (!IsAllowedUrl(url))
            {
                Reject(url, "Slide url rejected, origin not allowed");
                return Missing(SlideState.Rejected);
            }

            nextFetchAt.Set(throttleKey, now + MIN_FETCH_INTERVAL_SECONDS);
            inFlight.Add(url);
            LoadAsync(url).Forget();
            return Missing(SlideState.Loading);
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
        ///     Keeps a decoded slide as a clamped texture when its body has at most <see cref="MAX_SLIDE_BYTES" /> bytes and
        ///     its size is at most <see cref="PresentationBotMetadata.MAX_SLIDE_SIZE" /> px on either side.
        /// </summary>
        /// <returns><paramref name="slide" />, or <c>null</c> after destroying it when it is out of limits.</returns>
        internal static Texture2D? AcceptSlide(Texture2D? slide, ulong bodyBytes)
        {
            if (slide == null)
                return null;

            if (bodyBytes > MAX_SLIDE_BYTES || slide.width > PresentationBotMetadata.MAX_SLIDE_SIZE || slide.height > PresentationBotMetadata.MAX_SLIDE_SIZE)
            {
                UnityObjectUtils.SafeDestroy(slide);
                return null;
            }

            slide.wrapMode = TextureWrapMode.Clamp;
            return slide;
        }

        private static EnumResult<Texture2D, SlideState> Missing(SlideState state) =>
            EnumResult<Texture2D, SlideState>.ErrorResult(state);

        private bool IsCastPresenterOrigin(Uri uri)
        {
#if UNITY_EDITOR
            if (uri.Scheme == Uri.UriSchemeHttp && LoopbackUrls.IsLoopbackHost(uri.Host))
                return true;
#endif

            return uri.Scheme == Uri.UriSchemeHttps && uri.IsDefaultPort && string.Equals(uri.Host, castPresenterHost, StringComparison.Ordinal);
        }

        private void EndComposing()
        {
            if (--openLeases == 0)
                Unload();
        }

        private async UniTaskVoid LoadAsync(string url)
        {
            try
            {
                Texture2D? texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(url), RetryPolicy.DEFAULT, REQUEST_TIMEOUT_SECONDS),
                    new GetTextureArguments(TextureType.Albedo, useKtx: false, disableRedirects: true, decodeOffMainThread: true),
                    new SlideTextureOp(),
                    cts.Token,
                    ReportCategory.MEDIA_STREAM,
                    suppressErrors: true);

                if (cts.IsCancellationRequested || openLeases == 0)
                {
                    UnityObjectUtils.SafeDestroy(texture);
                    return;
                }

                if (texture == null)
                {
                    Reject(url, $"Slide rejected, not an image of at most {MAX_SLIDE_BYTES} bytes and {PresentationBotMetadata.MAX_SLIDE_SIZE} px per side");
                    return;
                }

                failed.TryRemove(url, out _);
                textures.Set(url, texture);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                int attempts = RecordFailure(url);

                if (attempts == 1 && TryConsumeReport(ref failureReportsLeft))
                    ReportHub.LogException(e, ReportCategory.MEDIA_STREAM);

                if (attempts >= MAX_FETCH_ATTEMPTS)
                {
                    failed.TryRemove(url, out _);
                    Reject(url, $"Slide rejected after {MAX_FETCH_ATTEMPTS} failed fetches");
                }
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
                UniTask.FromResult(AcceptSlide(DownloadHandlerTexture.GetContent(webRequest.UnityWebRequest), webRequest.UnityWebRequest.downloadedBytes));
        }

        private sealed class ComposingLease : IDisposable
        {
            private SlideTextureCache? cache;

            public ComposingLease(SlideTextureCache cache)
            {
                this.cache = cache;
            }

            public void Dispose()
            {
                cache?.EndComposing();
                cache = null;
            }
        }
    }
}
