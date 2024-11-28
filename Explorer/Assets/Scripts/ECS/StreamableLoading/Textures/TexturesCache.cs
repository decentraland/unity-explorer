using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache : RefCountStreamableCacheBase<Texture2DData, Texture2D, GetTextureIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.TexturesInCache;

        public override bool Equals(GetTextureIntention x, GetTextureIntention y) =>
            x.Equals(y);

        public override int GetHashCode(GetTextureIntention obj) =>
            obj.GetHashCode();
    }
}
