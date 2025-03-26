using DCL.Optimization.PerformanceBudgeting;

namespace ECS.StreamableLoading.Cache.InMemory
{
    public class StreamableWrapMemoryCache<T, TKey> : IMemoryCache<T, TKey>
    {
        private const int UNLOAD_CHUNK = int.MaxValue;
        private readonly IStreamableCache<T, TKey> streamableCache;

        public StreamableWrapMemoryCache(IStreamableCache<T, TKey> streamableCache)
        {
            this.streamableCache = streamableCache;
        }

        public void Put(TKey key, T value)
        {
            streamableCache.Add(key, value);
        }

        public bool TryGet(TKey key, out T valueOrDefault) =>
            streamableCache.TryGet(key, out valueOrDefault);

        public void Unload(IPerformanceBudget budgetToUse)
        {
            streamableCache.Unload(budgetToUse, UNLOAD_CHUNK);
        }
    }
}
