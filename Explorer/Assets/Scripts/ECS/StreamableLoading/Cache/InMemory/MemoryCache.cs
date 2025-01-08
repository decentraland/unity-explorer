using DCL.Optimization.PerformanceBudgeting;
using System.Collections.Generic;

namespace ECS.StreamableLoading.Cache.InMemory
{
    public class MemoryCache<T, TKey> : IMemoryCache<T, TKey> where T: class
    {
        private readonly Dictionary<TKey, T> cache = new ();

        public int Count => cache.Count;

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
