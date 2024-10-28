using Segment.Analytics;

namespace Plugins.RustSegment.SegmentServerWrap.ContextSources
{
    public interface IContextSource
    {
        string ContextJson();

        void Register(EventPlugin plugin);
    }
}
