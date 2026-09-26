using ECS.StreamableLoading.Cache.Disk;
using ECS.StreamableLoading.Cache.Disk.Cacheables;

namespace ECS.StreamableLoading.Common.Systems
{
    public readonly struct DiskCacheOptions<T, TIntention>
    {
        public readonly IDiskCache<T> DiskCache;
        public readonly IDiskHashCompute<TIntention> DiskHashCompute;
        public readonly string Extension;

        public DiskCacheOptions(IDiskCache<T> diskCache, IDiskHashCompute<TIntention> diskHashCompute, string extension = "dat")
        {
            DiskCache = diskCache;
            DiskHashCompute = diskHashCompute;
            Extension = extension;
        }
    }
}
