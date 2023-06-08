using Arch.Core;
using ECS.Abstract;
using ECS.StreamableLoading.Cache;
using ECS.StreamableLoading.Common.Components;

namespace ECS.StreamableLoading.Common.Systems
{
    /// <summary>
    ///     Create a <see cref="StreamableLoadingResult{T}" /> if the asset is present in the cache
    ///     <para> Must be executed before <see cref="StartLoadingSystemBase{TIntention,TAsset}" /> for the given pair of asset/intention</para>
    /// </summary>
    public abstract class LoadFromCacheSystemBase<TIntention, TAsset> : BaseUnityLoopSystem where TIntention: struct, ILoadingIntention
    {
        private readonly QueryDescription query = new QueryDescription()
                                                 .WithAll<TIntention>()
                                                 .WithNone<LoadingRequest, ForgetLoadingIntent, StreamableLoadingResult<TAsset>>();

        private TryLoadFromCacheQuery tryLoadFromCacheQuery;

        protected LoadFromCacheSystemBase(World world, IStreamableCache<TAsset, TIntention> cache) : base(world)
        {
            tryLoadFromCacheQuery = new TryLoadFromCacheQuery(cache, world);
        }

        protected override void Update(float t)
        {
            World.InlineEntityQuery<TryLoadFromCacheQuery, TIntention>(in query, ref tryLoadFromCacheQuery);
        }

        private readonly struct TryLoadFromCacheQuery : IForEachWithEntity<TIntention>
        {
            private readonly IStreamableCache<TAsset, TIntention> cache;
            private readonly World world;

            public TryLoadFromCacheQuery(IStreamableCache<TAsset, TIntention> cache, World world)
            {
                this.cache = cache;
                this.world = world;
            }

            public void Update(in Entity entity, ref TIntention intention)
            {
                if (cache.TryGet(in intention, out TAsset asset))
                    world.Add(entity, new StreamableLoadingResult<TAsset>(asset));
            }
        }
    }
}
