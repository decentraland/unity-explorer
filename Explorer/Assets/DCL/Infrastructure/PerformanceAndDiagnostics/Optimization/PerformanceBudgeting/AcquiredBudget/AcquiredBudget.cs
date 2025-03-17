using DCL.Optimization.ThreadSafePool;
using UnityEngine.Pool;

namespace DCL.Optimization.PerformanceBudgeting
{
    /// <summary>
    ///     Identifies budget that was acquired, it must be a reference type so
    ///     it can be passed to the async flow and be released from there
    /// </summary>
    public class AcquiredBudget : IAcquiredBudget
    {
        private static readonly IObjectPool<AcquiredBudget> POOL = new ThreadSafeObjectPool<AcquiredBudget>(
            () => new AcquiredBudget(), defaultCapacity: 1000, maxSize: 1_000_000);

        private IReleasablePerformanceBudget provider;
        private bool released;
        private bool disposed;

        private AcquiredBudget() { }

        public void Dispose()
        {
            if (disposed) return;

            Release();
            POOL.Release(this);

            disposed = true;
        }

        /// <summary>
        ///     Must be called from the main thread
        /// </summary>
        public void Release()
        {
            if (!released)
                provider.ReleaseBudget();

            released = true;
        }

        public static IAcquiredBudget Create(IReleasablePerformanceBudget releasablePerformanceBudget)
        {
            AcquiredBudget b = POOL.Get();
            b.provider = releasablePerformanceBudget;
            b.released = false;
            b.disposed = false;
            return b;
        }
    }
}
