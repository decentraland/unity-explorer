using DCL.Optimization.PerformanceBudgeting;

namespace ECS.StreamableLoading.Cache.InMemory
{
    public interface IMemoryCache<T, in TKey>
    {
        void Put(TKey key, T value);

        bool TryGet(TKey key, out T valueOrDefault);

        void Unload(IPerformanceBudget budgetToUse);
    }
}
