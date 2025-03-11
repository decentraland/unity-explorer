using Cysharp.Threading.Tasks;
using DCL.DebugUtilities.UIBindings;
using DCL.Optimization.PerformanceBudgeting;
using DCL.WebRequests.RequestsHub;

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

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op) where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            IAcquiredBudget totalBudgetAcquired;

            // Try bypass total budget
            while (!totalBudget.TrySpendBudget(out totalBudgetAcquired))
                await UniTask.Yield(envelope.Ct);

            try
            {
                lock (debugBudget) { debugBudget.Value--; }

                return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
            }
            finally
            {
                lock (debugBudget) { debugBudget.Value++; }

                totalBudgetAcquired.Dispose();
            }
        }

        IRequestHub IWebRequestController.requestHub => origin.requestHub;
    }
}
