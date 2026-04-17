using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using System.Threading;

namespace DCL.SDKComponents.MediaStream
{
    /// <summary>
    ///     Analytics decorator for <see cref="IUrlResolverService"/>.
    ///     Follows the project's decorator-based analytics pattern (see DecoratorBased/).
    /// </summary>
    public class UrlResolverServiceAnalyticsDecorator : IUrlResolverService
    {
        private readonly IUrlResolverService inner;
        private readonly IAnalyticsController analytics;

        public UrlResolverServiceAnalyticsDecorator(IUrlResolverService inner, IAnalyticsController analytics)
        {
            this.inner = inner;
            this.analytics = analytics;
        }

        public async UniTask<ResolvedMediaUrl> ResolveAsync(string url, ReportData reportData, CancellationToken ct)
        {
            ResolvedMediaUrl result = await inner.ResolveAsync(url, reportData, ct);

            // Only track successfully resolved streams — avoids inflating metrics with
            // failed resolutions or duplicate re-resolutions on URL expiry.
            if (result.IsReachable)
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

            return result;
        }
    }
}
