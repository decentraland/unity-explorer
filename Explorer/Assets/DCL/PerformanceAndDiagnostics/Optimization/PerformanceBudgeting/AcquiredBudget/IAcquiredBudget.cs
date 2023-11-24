using System;

namespace DCL.PerformanceAndDiagnostics.Optimization.PerformanceBudgeting
{
    public interface IAcquiredBudget : IDisposable
    {
        void Release();
    }
}
