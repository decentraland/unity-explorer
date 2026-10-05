using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
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
    ///     <see cref="Uri.AbsoluteUri" />): https on the default port, the host <c>cast-presenter-service.{BaseDomain}</c>,
    ///     no userinfo, query, fragment or backslash, and a path made of an optional prefix of <c>[A-Za-z0-9_-]</c>
    ///     segments followed by <c>/presentations/{uuid}/slides/{16 lowercase hex}.png</c>; in the Editor, loopback http
    ///     urls on any port with the same path too. Fetches once per url while in flight, and backs off after a failure.
    ///     Starts at most one fetch per <see cref="MIN_FETCH_INTERVAL_SECONDS" /> per throttle key, tracking at most
    ///     <see cref="MAX_TRACKED_BOTS" /> keys, tracks at most <see cref="MAX_TRACKED_URLS" /> rejected and
    ///     <see cref="MAX_TRACKED_URLS" /> failed urls, logs at most <see cref="MAX_REJECTION_REPORTS" /> rejections and
    ///     <see cref="MAX_FAILURE_REPORTS" /> failures in its lifetime, and rejects slides decoded larger than
    ///     <see cref="PresentationLayout.MAX_SLIDE_SIZE" /> on either side. Does nothing once disposed. Main-thread only.
    /// </summary>
    public sealed class SlideTextureCache : IDisposable
    {
        internal const int CAPACITY = 4;
        internal const float BASE_RETRY_COOLDOWN_SECONDS = 10f;
        internal const float MAX_RETRY_COOLDOWN_SECONDS = 60f;
        internal const float MIN_FETCH_INTERVAL_SECONDS = 0.25f;
        internal const int MAX_TRACKED_URLS = 32;
        internal const int MAX_TRACKED_BOTS = 32;
        internal const int MAX_REJECTION_REPORTS = 8;
        internal const int MAX_FAILURE_REPORTS = 32;

        private static readonly Regex SLIDE_PATH = new (@"^(/[A-Za-z0-9_-]+)*/presentations/[0-9a-f-]{36}/slides/[0-9a-f]{16}\.png\z", RegexOptions.Compiled);

        private readonly IWebRequestController webRequestController;
        private readonly string castPresenterHost;
        private readonly Func<float> getRealtimeSinceStartup;
        private readonly Dictionary<string, Texture2D> textures = new ();
        private readonly List<string> order = new ();
        private readonly HashSet<string> inFlight = new ();
        private readonly HashSet<string> rejected = new ();
        private readonly Dictionary<string, (int attempts, float retryAt)> failed = new ();
        private readonly Dictionary<string, float> nextFetchAt = new ();
        private readonly CancellationTokenSource cts = new ();

        private int rejectionReportsLeft = MAX_REJECTION_REPORTS;
        private int failureReportsLeft = MAX_FAILURE_REPORTS;
        private bool disposed;

        internal int trackedUrlCount => rejected.Count + failed.Count;

        public SlideTextureCache(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource)
            : this(webRequestController, decentralandUrlsSource, static () => UnityEngine.Time.realtimeSinceStartup) { }

        internal SlideTextureCache(IWebRequestController webRequestController, IDecentralandUrlsSource decentralandUrlsSource, Func<float> getRealtimeSinceStartup)
        {
            this.webRequestController = webRequestController;
            castPresenterHost = $"cast-presenter-service.{decentralandUrlsSource.BaseDomain}".ToLowerInvariant();
            this.getRealtimeSinceStartup = getRealtimeSinceStartup;
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;

            cts.Cancel();
            cts.Dispose();

            foreach (Texture2D texture in textures.Values)
                UnityObjectUtils.SafeDestroy(texture);

            textures.Clear();
            order.Clear();
            inFlight.Clear();
            rejected.Clear();
            failed.Clear();
            nextFetchAt.Clear();
        }

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
            {
                order.Remove(url);
                order.Add(url);
                return texture;
            }

            if (inFlight.Contains(url) || rejected.Contains(url))
                return null;

            float now = getRealtimeSinceStartup();

            if ((nextFetchAt.TryGetValue(throttleKey, out float keyNext) && now < keyNext)
                || (failed.TryGetValue(url, out (int attempts, float retryAt) failure) && now < failure.retryAt))
                return null;

            if (!IsAllowedUrl(url))
            {
                Reject(url);

                if (TryConsumeReport(ref rejectionReportsLeft))
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Slide url rejected, origin not allowed: {OriginOf(url)}");

                return null;
            }

            if (!nextFetchAt.ContainsKey(throttleKey) && nextFetchAt.Count >= MAX_TRACKED_BOTS)
                nextFetchAt.Clear();

            nextFetchAt[throttleKey] = now + MIN_FETCH_INTERVAL_SECONDS;
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
                Texture2D texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(url), RetryPolicy.DEFAULT),
                    new GetTextureArguments(TextureType.Albedo, useKtx: false, disableRedirects: true),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear),
                    cts.Token,
                    ReportCategory.MEDIA_STREAM,
                    suppressErrors: true);

                if (cts.IsCancellationRequested)
                {
                    UnityObjectUtils.SafeDestroy(texture);
                    return;
                }

                if (texture.width > PresentationLayout.MAX_SLIDE_SIZE || texture.height > PresentationLayout.MAX_SLIDE_SIZE)
                {
                    Reject(url);

                    if (TryConsumeReport(ref rejectionReportsLeft))
                        ReportHub.LogWarning(ReportCategory.MEDIA_STREAM,
                            $"Slide rejected, decoded size {texture.width}x{texture.height} exceeds {PresentationLayout.MAX_SLIDE_SIZE}: {OriginOf(url)}");

                    UnityObjectUtils.SafeDestroy(texture);
                    return;
                }

                if (texture.isReadable)
                    texture.Apply(false, true);

                failed.Remove(url);
                Insert(url, texture);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                if (RecordFailure(url) == 1 && TryConsumeReport(ref failureReportsLeft))
                    ReportHub.LogException(e, ReportCategory.MEDIA_STREAM);
            }
            finally { inFlight.Remove(url); }
        }

        private void Reject(string url)
        {
            if (rejected.Count >= MAX_TRACKED_URLS)
                rejected.Clear();

            rejected.Add(url);
        }

        private int RecordFailure(string url)
        {
            int attempts = failed.TryGetValue(url, out (int attempts, float retryAt) failure) ? failure.attempts + 1 : 1;

            if (attempts == 1 && failed.Count >= MAX_TRACKED_URLS)
                failed.Clear();

            failed[url] = (attempts, getRealtimeSinceStartup() + Mathf.Min(BASE_RETRY_COOLDOWN_SECONDS * attempts, MAX_RETRY_COOLDOWN_SECONDS));
            return attempts;
        }

        private static bool TryConsumeReport(ref int budget)
        {
            if (budget <= 0)
                return false;

            budget--;
            return true;
        }

        private void Insert(string url, Texture2D texture)
        {
            textures[url] = texture;
            order.Add(url);

            while (order.Count > CAPACITY)
            {
                string oldest = order[0];
                order.RemoveAt(0);

                if (textures.Remove(oldest, out Texture2D evicted))
                    UnityObjectUtils.SafeDestroy(evicted);
            }
        }

        private static string OriginOf(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? $"{uri.Scheme}://{uri.Host}" : "unparseable url";
    }
}
