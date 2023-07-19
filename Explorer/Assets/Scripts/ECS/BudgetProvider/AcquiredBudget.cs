using System;
using UnityEngine.Pool;
using Utility.ThreadSafePool;

namespace ECS.BudgetProvider
{
    public interface IAcquiredBudget : IDisposable
    {
        void Release();
    }

    public class NoAcquiredBudget : IAcquiredBudget
    {
        public static readonly NoAcquiredBudget INSTANCE = new ();

        private NoAcquiredBudget() { }

        public void Dispose() { }

        public void Release() { }
    }

    /// <summary>
    ///     Identifies budget that was acquired, it must be a reference type so
    ///     it can be passed to the async flow and be released from there
    /// </summary>
    public class AcquiredBudget : IAcquiredBudget
    {
        private static readonly IObjectPool<AcquiredBudget> POOL = new ThreadSafeObjectPool<AcquiredBudget>(
            () => new AcquiredBudget(), defaultCapacity: 1000, maxSize: 1_000_000);

        private IConcurrentBudgetProvider provider;
        private bool released;
        private int budgetCost;

        private AcquiredBudget() { }

        public void Dispose()
        {
            Release();
            POOL.Release(this);
        }

        /// <summary>
        ///     Must be called from the main thread
        /// </summary>
        public void Release()
        {
            if (!released)
                provider.ReleaseBudget(budgetCost);

            released = true;
        }

        public static IAcquiredBudget Create(IConcurrentBudgetProvider concurrentBudgetProvider, int budgetCost = 1)
        {
            AcquiredBudget b = POOL.Get();
            b.provider = concurrentBudgetProvider;
            b.released = false;
            b.budgetCost = budgetCost;
            return b;
        }
    }
}
