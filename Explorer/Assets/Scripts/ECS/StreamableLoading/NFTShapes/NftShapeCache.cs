using DCL.Profiling;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Textures;
using Unity.Profiling;
using UnityEngine;

namespace ECS.StreamableLoading.NFTShapes
{
    public class NftShapeCache : RefCountStreamableCacheBase<Texture2DData, Texture2D, GetNFTShapeIntention>, IStreamableCache<Texture2DData, GetNFTShapeIntention>
    {
        protected override ref ProfilerCounterValue<int> inCacheCount => ref ProfilingCounters.NFTsInCache;

        public override bool Equals(GetNFTShapeIntention x, GetNFTShapeIntention y) =>
            x.Equals(y);

        public override int GetHashCode(GetNFTShapeIntention obj) =>
            obj.GetHashCode();
    }
}
