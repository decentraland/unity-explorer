using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Decorator that tracks media source type via analytics before resolving.
    ///     Follows the project's decorator-based analytics pattern (see DecoratorBased/).
    /// </summary>
    public class TrackedMediaUrlResolver
    {
        private readonly YouTubeUrlResolver youTubeResolver;
        private readonly IAnalyticsController analytics;

        public TrackedMediaUrlResolver(YouTubeUrlResolver youTubeResolver, IAnalyticsController analytics)
        {
            this.youTubeResolver = youTubeResolver;
            this.analytics = analytics;
        }

        public async UniTask<ResolvedYouTubeUrl?> ResolveYouTubeAsync(string url, CancellationToken ct)
        {
            TrackMediaSource(url);
            return await youTubeResolver.ResolveAsync(url, ct);
        }

        public void TrackMediaSource(string url)
        {
            string host;

            try { host = new System.Uri(url).Host; }
            catch { host = "unknown"; }

            analytics.Track(AnalyticsEvents.Media.MEDIA_STREAM_OPENED, new JObject
            {
                ["source_type"] = host,
                ["url"] = url,
            });
        }
    }
}
