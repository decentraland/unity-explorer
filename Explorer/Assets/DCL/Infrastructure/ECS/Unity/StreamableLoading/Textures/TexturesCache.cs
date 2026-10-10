using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using System;
using System.Linq;
using Unity.Profiling;

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache<TIntention> : RefCountStreamableCacheBase<TextureData, AnyTexture, TIntention>,
        ISizedStreamableCache<TextureData, TIntention> where TIntention: IEquatable<TIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.TexturesInCache;

        public long ByteSize => cache.Sum(e => e.Value!.ByteSize);

        public int ItemCount => cache.Count;
    }
}
