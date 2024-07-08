using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IAcquiredBudget : IDisposable
    {
        /// <summary>
        ///     Implementation should be safe for repetitive invocations
        /// </summary>
        void Release();
    }
}
