using DCL.PerformanceAndDiagnostics.Analytics;
using Newtonsoft.Json.Linq;
using System.Collections.Generic;

namespace Plugins.RustSegment.SegmentServerWrap.ContextSources
{
    public class ContextSource : IContextSource
    {
        private readonly List<IAnalyticsPlugin> plugins = new ();
        private readonly JObject trackEvent = new ();

        public string ContextJson()
        {
            lock (this)
            {
                trackEvent.RemoveAll();
                foreach (var plugin in plugins) plugin.Track(trackEvent);
                return trackEvent.ToString();
            }
        }

        public void Register(IAnalyticsPlugin plugin)
        {
            lock (this) { plugins.Add(plugin); }
        }
    }
}
