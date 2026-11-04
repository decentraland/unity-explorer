using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;

namespace ECS.StreamableLoading.Fonts
{
    public class FontsCache : RefCountStreamableCacheBase<FontData, SceneFontAssets, GetFontIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.FontsInCache;

        /// <summary>Applies <see cref="SceneFontAssets.LimitAtlasPages" /> to every cached font.</summary>
        public void LimitAtlasPages()
        {
            for (var i = 0; i < listedCache.Count; i++)
                listedCache[i].asset.Asset.LimitAtlasPages();
        }
    }
}
