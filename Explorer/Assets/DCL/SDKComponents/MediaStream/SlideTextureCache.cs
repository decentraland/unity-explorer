using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Text.RegularExpressions;
using System.Threading;
using UnityEngine;
using Utility;
using Utility.Networking;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     App-wide cache of presentation slide textures keyed by url, holding at most <see cref="CAPACITY" /> and
    ///     destroying the oldest first. Fetches only allowlisted urls, once per url while in flight, and backs off
    ///     after a failure. Starts at most one fetch per <see cref="MIN_FETCH_INTERVAL_SECONDS" /> per throttle key,
    ///     tracking at most <see cref="MAX_TRACKED_BOTS" /> keys, tracks at most <see cref="MAX_TRACKED_URLS" />
    ///     rejected and <see cref="MAX_TRACKED_URLS" /> failed urls, logs at most <see cref="MAX_REJECTION_REPORTS" />
    ///     rejections and <see cref="MAX_FAILURE_REPORTS" /> failures in its lifetime, and drops slides decoded larger
    ///     than <see cref="PresentationLayout.MAX_SLIDE_SIZE" /> on either side. Main-thread only.
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

        private const string CAST_PRESENTER_HOST_PREFIX = "cast-presenter-service.";

        private static readonly Regex SLIDE_PATH = new (@"^(/[A-Za-z0-9_-]+)*/presentations/[0-9a-f-]{36}/slides/[0-9a-f]{16}\.png\z", RegexOptions.Compiled);

        private readonly IWebRequestController webRequestController;
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

        public SlideTextureCache(IWebRequestController webRequestController)
            : this(webRequestController, static () => UnityEngine.Time.realtimeSinceStartup) { }

        internal SlideTextureCache(IWebRequestController webRequestController, Func<float> getRealtimeSinceStartup)
        {
            this.webRequestController = webRequestController;
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
        ///     Whether <paramref name="url" /> is a contract-shaped slide url in canonical form (equal to its own
        ///     <see cref="Uri.AbsoluteUri" />): https on the default port, an ASCII host equal to the cast presenter
        ///     service host of a Decentraland domain, no userinfo, query, fragment or backslash, and a path made of an
        ///     optional prefix of <c>[A-Za-z0-9_-]</c> segments followed by
        ///     <c>/presentations/{uuid}/slides/{16 lowercase hex}.png</c>. In the Editor, loopback http urls on any port
        ///     with the same path are allowed too.
        /// </summary>
        public static bool IsAllowedUrl(string url) =>
            TryParseAllowedUrl(url, out _);

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
            if (textures.TryGetValue(url, out Texture2D texture))
                return texture;

            if (inFlight.Contains(url) || rejected.Contains(url))
                return null;

            float now = getRealtimeSinceStartup();

            if ((nextFetchAt.TryGetValue(throttleKey, out float keyNext) && now < keyNext)
                || (failed.TryGetValue(url, out (int attempts, float retryAt) failure) && now < failure.retryAt))
                return null;

            if (!TryParseAllowedUrl(url, out Uri? uri))
            {
                if (rejected.Count >= MAX_TRACKED_URLS)
                    rejected.Clear();

                rejected.Add(url);

                if (TryConsumeReport(ref rejectionReportsLeft))
                    ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Slide url rejected, origin not allowed: {OriginOf(url)}");

                return null;
            }

            if (!nextFetchAt.ContainsKey(throttleKey) && nextFetchAt.Count >= MAX_TRACKED_BOTS)
                nextFetchAt.Clear();

            nextFetchAt[throttleKey] = now + MIN_FETCH_INTERVAL_SECONDS;
            inFlight.Add(url);
            LoadAsync(url, uri.AbsoluteUri).Forget();
            return null;
        }

        private static bool TryParseAllowedUrl(string url, [NotNullWhen(true)] out Uri? uri)
        {
            uri = null;

            if (url.Contains('\\') || !Uri.TryCreate(url, UriKind.Absolute, out Uri? parsed))
                return false;

            if (parsed.UserInfo.Length > 0 || parsed.Query.Length > 0 || parsed.Fragment.Length > 0 || !SLIDE_PATH.IsMatch(parsed.AbsolutePath))
                return false;

            if (!IsCastPresenterOrigin(parsed))
                return false;

            if (!string.Equals(parsed.AbsoluteUri, url, StringComparison.Ordinal))
                return false;

            uri = parsed;
            return true;
        }

        private static bool IsCastPresenterOrigin(Uri uri)
        {
#if UNITY_EDITOR
            if (uri.Scheme == Uri.UriSchemeHttp && LoopbackUrls.IsLoopbackHost(uri.Host))
                return true;
#endif

            if (uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort || !IsAscii(uri.Host))
                return false;

            IReadOnlyList<string> domains = IDecentralandUrlsSource.ALL_DOMAINS;

            for (var i = 0; i < domains.Count; i++)
                if (string.Equals(uri.Host, $"{CAST_PRESENTER_HOST_PREFIX}{domains[i]}", StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        private static bool IsAscii(string value)
        {
            foreach (char c in value)
                if (c >= 0x80)
                    return false;

            return true;
        }

        private async UniTaskVoid LoadAsync(string url, string fetchUrl)
        {
            try
            {
                Texture2D? texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(fetchUrl), RetryPolicy.DEFAULT),
                    new GetTextureArguments(TextureType.Albedo, useKtx: false, disableRedirects: true),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear),
                    cts.Token,
                    ReportCategory.MEDIA_STREAM,
                    suppressErrors: true);

                if (cts.IsCancellationRequested)
                {
                    Object.Destroy(texture);
                    return;
                }

                if (texture == null)
                {
                    if (RecordFailure(url) == 1 && TryConsumeReport(ref failureReportsLeft))
                        ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Slide request returned no texture: {OriginOf(url)}");

                    return;
                }

                if (texture.width > PresentationLayout.MAX_SLIDE_SIZE || texture.height > PresentationLayout.MAX_SLIDE_SIZE)
                {
                    if (RecordFailure(url) == 1 && TryConsumeReport(ref failureReportsLeft))
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
                    Object.Destroy(evicted);
            }
        }

        private static string OriginOf(string url) =>
            Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) ? $"{uri.Scheme}://{uri.Host}" : "unparseable url";
    }
}
