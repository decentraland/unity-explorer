using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;

namespace DCL.WebRequests
{
    public class BudgetedWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly IReleasablePerformanceBudget totalBudget;

        public BudgetedWebRequestController(IWebRequestController origin, int totalBudget)
        {
            this.origin = origin;
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
                return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
            }
            finally
            {
                totalBudgetAcquired.Dispose();
            }
        }
    }
}
