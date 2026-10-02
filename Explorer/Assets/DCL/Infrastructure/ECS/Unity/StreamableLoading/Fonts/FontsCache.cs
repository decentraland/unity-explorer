using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontsCache : RefCountStreamableCacheBase<FontData, SceneFontAssets, GetFontIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.FontsInCache;
    }
}
