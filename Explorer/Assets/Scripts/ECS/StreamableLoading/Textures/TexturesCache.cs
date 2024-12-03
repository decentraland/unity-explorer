using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using System.Linq;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.Textures
{
    public class TexturesCache<TIntention> : RefCountStreamableCacheBase<Texture2DData, Texture2D, TIntention>, ISizedStreamableCache<Texture2DData, TIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.TexturesInCache;

        public override bool Equals(TIntention x, TIntention y) =>
            x.Equals(y);

        public override int GetHashCode(TIntention obj) =>
            obj.GetHashCode();

        public long ByteSize => cache.Sum(e => e.Value!.ByteSize);

        public int ItemCount => cache.Count;
    }
}
