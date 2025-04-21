using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests.RequestsHub;
using System.Threading;

namespace DCL.WebRequests
{
    public class BudgetedWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly IReleasablePerformanceBudget totalBudget;
        private readonly ElementBinding<ulong> debugBudget;

        public BudgetedWebRequestController(IWebRequestController origin, int totalBudget, ElementBinding<ulong> debugBudget)
        {
            this.origin = origin;
            this.debugBudget = debugBudget;
            this.totalBudget = new ConcurrentLoadingPerformanceBudget(totalBudget);
        }

        IRequestHub IWebRequestController.requestHub => origin.requestHub;

        public async UniTask<IWebRequest> SendAsync(ITypedWebRequest requestWrap, bool detachDownloadHandler, CancellationToken ct)
        {
            IAcquiredBudget totalBudgetAcquired;

            // Try bypass total budget
            while (!totalBudget.TrySpendBudget(out totalBudgetAcquired))
                await UniTask.Yield(ct);

            try
            {
                lock (debugBudget) { debugBudget.Value--; }

                return await origin.SendAsync(requestWrap, detachDownloadHandler, ct);
            }
            finally
            {
                lock (debugBudget) { debugBudget.Value++; }

                totalBudgetAcquired.Dispose();
            }
        }
    }
}
