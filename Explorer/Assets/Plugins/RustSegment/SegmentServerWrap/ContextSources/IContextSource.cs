using DCL.PerformanceAndDiagnostics.Analytics;

namespace Plugins.RustSegment.SegmentServerWrap.ContextSources
{
    public interface IContextSource
    {
        string ContextJson();

        void Register(IAnalyticsPlugin plugin);
    }
}
