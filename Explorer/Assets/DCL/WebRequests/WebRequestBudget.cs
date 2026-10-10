using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Threading;

namespace DCL.WebRequests
{
    public class WebRequestBudget
    {
        private readonly IReleasablePerformanceBudget totalBudget;
        private readonly ElementBinding<ulong> debugBudget;

        public WebRequestBudget(int totalBudget, ElementBinding<ulong> debugBudget)
        {
            this.totalBudget = new ConcurrentLoadingPerformanceBudget(totalBudget);
            this.debugBudget = debugBudget;
        }

        public async UniTask<AcquiredBudget> AcquireAsync(CancellationToken ct)
        {
            IAcquiredBudget totalBudgetAcquired;

            while (!totalBudget.TrySpendBudget(out totalBudgetAcquired))
                await UniTask.Yield(ct);

            lock (debugBudget) { debugBudget.Value--; }

            return new AcquiredBudget(debugBudget, totalBudgetAcquired);
        }

        public readonly struct AcquiredBudget : IDisposable
        {
            private readonly ElementBinding<ulong> debugBudget;
            private readonly IAcquiredBudget acquiredBudget;

            public AcquiredBudget(ElementBinding<ulong> debugBudget, IAcquiredBudget acquiredBudget)
            {
                this.debugBudget = debugBudget;
                this.acquiredBudget = acquiredBudget;
            }

            public void Dispose()
            {
                acquiredBudget.Dispose();

                lock (debugBudget) { debugBudget.Value++; }
            }
        }
    }
}
