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

        public UniTask<ResolvedMediaUrl> ResolveAsync(string url, ReportData reportData, CancellationToken ct)
        {
            string host;

            try { host = new System.Uri(url).Host; }
            catch { host = "unknown"; }

            analytics.Track(AnalyticsEvents.Media.MEDIA_STREAM_OPENED, new JObject
            {
                ["source_type"] = host,
                ["url"] = url,
            });

            return inner.ResolveAsync(url, reportData, ct);
        }
    }
}
