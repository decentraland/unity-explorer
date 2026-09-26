using System;

namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IAcquiredBudget : IDisposable
    {
        /// <summary>
        ///     Releases budget preemptively without waiting for the full resolution of the flow <br/>
        ///     Implementation should be safe for repetitive invocations
        /// </summary>
        void Release();
    }
}
