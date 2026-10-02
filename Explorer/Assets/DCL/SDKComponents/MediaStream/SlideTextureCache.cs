using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Multiplayer.Connections.DecentralandUrls;
using DCL.WebRequests;
using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;
using Utility.Networking;
using Object = UnityEngine.Object;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     App-wide cache of presentation slide textures keyed by url, holding at most <see cref="CAPACITY" /> and
    ///     destroying the oldest first. Fetches only allowlisted urls, once per url while in flight, and backs off
    ///     after a failure. Main-thread only.
    /// </summary>
    public sealed class SlideTextureCache : IDisposable
    {
        internal const int CAPACITY = 4;
        internal const float BASE_RETRY_COOLDOWN_SECONDS = 10f;
        internal const float MAX_RETRY_COOLDOWN_SECONDS = 60f;
        internal const float MIN_FETCH_INTERVAL_SECONDS = 0.25f;
        internal const int MAX_TRACKED_URLS = 32;
        internal const int MAX_REPORTS = 32;

        private const string CAST_PRESENTER_HOST_PREFIX = "cast-presenter-service.";

        private readonly IWebRequestController webRequestController;
        private readonly Dictionary<string, Texture2D> textures = new ();
        private readonly List<string> order = new ();
        private readonly HashSet<string> inFlight = new ();
        private readonly HashSet<string> rejected = new ();
        private readonly Dictionary<string, (int attempts, float retryAt)> failed = new ();
        private readonly CancellationTokenSource cts = new ();

        public SlideTextureCache(IWebRequestController webRequestController)
        {
            this.webRequestController = webRequestController;
        }

        internal SlideTextureCache(IWebRequestController webRequestController, Func<float> getRealtimeSinceStartup)
        {
            this.webRequestController = webRequestController;
        }

        internal int trackedUrlCount => throw new NotImplementedException();

        public void Dispose()
        {
            cts.Cancel();
            cts.Dispose();

            foreach (Texture2D texture in textures.Values)
                Object.Destroy(texture);

            textures.Clear();
            order.Clear();
            inFlight.Clear();
            rejected.Clear();
            failed.Clear();
        }

        /// <summary>
        ///     Whether <paramref name="url" /> is an https url on the cast presenter service host of a Decentraland
        ///     domain. In the Editor, loopback http urls are allowed too.
        /// </summary>
        public static bool IsAllowedUrl(string url)
        {
#if UNITY_EDITOR
            if (LoopbackUrls.IsLoopbackHttpUrl(url))
                return true;
#endif

            if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? uri) || uri.Scheme != Uri.UriSchemeHttps)
                return false;

            IReadOnlyList<string> domains = IDecentralandUrlsSource.ALL_DOMAINS;

            for (var i = 0; i < domains.Count; i++)
                if (string.Equals(uri.Host, $"{CAST_PRESENTER_HOST_PREFIX}{domains[i]}", StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        /// <summary>
        ///     The cached texture for <paramref name="url" />, or <c>null</c> while it loads, cools down after a
        ///     failure, or is disallowed. The first call for a fetchable url starts the download.
        /// </summary>
        public Texture2D? GetOrRequest(string url)
        {
            if (textures.TryGetValue(url, out Texture2D texture))
                return texture;

            if (inFlight.Contains(url) || rejected.Contains(url))
                return null;

            if (failed.TryGetValue(url, out (int attempts, float retryAt) failure) && UnityEngine.Time.realtimeSinceStartup < failure.retryAt)
                return null;

            if (!IsAllowedUrl(url))
            {
                rejected.Add(url);
                ReportHub.LogWarning(ReportCategory.MEDIA_STREAM, $"Slide url rejected, origin not allowed: {OriginOf(url)}");
                return null;
            }

            inFlight.Add(url);
            LoadAsync(url).Forget();
            return null;
        }

        private async UniTaskVoid LoadAsync(string url)
        {
            try
            {
                Texture2D texture = await webRequestController.GetTextureAsync(
                    new CommonArguments(URLAddress.FromString(url), RetryPolicy.DEFAULT),
                    new GetTextureArguments(TextureType.Albedo, useKtx: false),
                    GetTextureWebRequest.CreateTexture(TextureWrapMode.Clamp, FilterMode.Bilinear),
                    cts.Token,
                    ReportCategory.MEDIA_STREAM,
                    suppressErrors: true);

                if (cts.IsCancellationRequested)
                {
                    Object.Destroy(texture);
                    return;
                }

                failed.Remove(url);
                Insert(url, texture);
            }
            catch (OperationCanceledException) { }
            catch (Exception e)
            {
                int attempts = failed.TryGetValue(url, out (int attempts, float retryAt) failure) ? failure.attempts + 1 : 1;
                failed[url] = (attempts, UnityEngine.Time.realtimeSinceStartup +Mathf.Min(BASE_RETRY_COOLDOWN_SECONDS * attempts, MAX_RETRY_COOLDOWN_SECONDS));
                ReportHub.LogException(e, ReportCategory.MEDIA_STREAM);
            }
            finally { inFlight.Remove(url); }
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
