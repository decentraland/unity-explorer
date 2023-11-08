using System;

namespace DCL.PerformanceBudgeting.AcquiredBudget
{
    public interface IAcquiredBudget : IDisposable
    {
        void Release();
    }
}
