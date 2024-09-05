using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using System;

namespace DCL.WebRequests
{
    public class BudgetedWebRequestController : IWebRequestController
    {
        private readonly IWebRequestController origin;
        private readonly IReleasablePerformanceBudget webRequestBudget;

        public BudgetedWebRequestController(IWebRequestController origin, IReleasablePerformanceBudget webRequestBudget)
        {
            this.origin = origin;
            this.webRequestBudget = webRequestBudget;
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op) where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            IAcquiredBudget acquiredBudget;

            while (!webRequestBudget.TrySpendBudget(out acquiredBudget))
                await UniTask.Yield(envelope.Ct);

            using IAcquiredBudget? _ = acquiredBudget;
            return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
        }
    }
}
