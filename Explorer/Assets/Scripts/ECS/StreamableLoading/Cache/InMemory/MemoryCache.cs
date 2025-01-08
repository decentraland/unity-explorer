using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache.InMemory
{
    public class MemoryCache<T, TKey> : IMemoryCache<T, TKey> where T: class
    {
        private readonly Dictionary<TKey, T> cache;

        public MemoryCache(Dictionary<TKey, T>? cache = null)
        {
            this.cache = cache ?? new Dictionary<TKey, T>();
        }

        public void Put(TKey key, T value)
        {
            cache[key] = value;
        }

        public bool TryGet(TKey key, out T valueOrDefault) =>
            cache.TryGetValue(key, out valueOrDefault);

        public void Unload(IPerformanceBudget budgetToUse) =>
            cache.Clear();
    }
}
