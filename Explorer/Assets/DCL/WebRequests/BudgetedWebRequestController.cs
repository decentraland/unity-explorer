using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;

namespace DCL.WebRequests
{
    public class BudgetedWebRequestController : IWebRequestController
    {
        private readonly ObjectPool<ConcurrentLoadingPerformanceBudget> budgetPool;

        private readonly IWebRequestController origin;
        private readonly int perDomainBudget;

        private readonly IReleasablePerformanceBudget totalBudget;
        private readonly Dictionary<string, ConcurrentLoadingPerformanceBudget> perDomainBudgets = new (50, StringComparer.OrdinalIgnoreCase);

        public BudgetedWebRequestController(IWebRequestController origin, int totalBudget, int perDomainBudget)
        {
            this.origin = origin;
            this.perDomainBudget = perDomainBudget;

            this.totalBudget = new ConcurrentLoadingPerformanceBudget(totalBudget);
            budgetPool = new ObjectPool<ConcurrentLoadingPerformanceBudget>(() => new ConcurrentLoadingPerformanceBudget(perDomainBudget), defaultCapacity: 50);
        }

        public async UniTask<TResult?> SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(RequestEnvelope<TWebRequest, TWebRequestArgs> envelope, TWebRequestOp op) where TWebRequest: struct, ITypedWebRequest where TWebRequestArgs: struct where TWebRequestOp: IWebRequestOp<TWebRequest, TResult>
        {
            IAcquiredBudget totalBudgetAcquired;
            IAcquiredBudget? domainBudgetAcquired = null;
            ConcurrentLoadingPerformanceBudget? domainBudget = null;
            string baseDomain = string.Empty;

            // Try bypass total budget
            while (!totalBudget.TrySpendBudget(out totalBudgetAcquired))
                await UniTask.Yield(envelope.Ct);

            try
            {
                baseDomain = new string(envelope.CommonArguments.URL.GetBaseDomain()); // TODO handle allocations

                if (!string.IsNullOrEmpty(baseDomain))
                {
                    lock (perDomainBudgets)
                    {
                        if (!perDomainBudgets.TryGetValue(baseDomain, out domainBudget))
                        {
                            domainBudget = budgetPool.Get();
                            perDomainBudgets.Add(baseDomain, domainBudget);
                        }
                    }

                    // Try bypass domain budget
                    while (!domainBudget.TrySpendBudget(out domainBudgetAcquired))
                        await UniTask.Yield(envelope.Ct);
                }

                return await origin.SendAsync<TWebRequest, TWebRequestArgs, TWebRequestOp, TResult>(envelope, op);
            }
            finally
            {
                totalBudgetAcquired.Dispose();

                if (domainBudgetAcquired != null)
                {
                    domainBudgetAcquired.Dispose();

                    if (domainBudget!.CurrentBudget == perDomainBudget)
                    {
                        lock (perDomainBudgets)
                        {
                            perDomainBudgets.Remove(baseDomain);
                            budgetPool.Release(domainBudget);
                        }
                    }
                }
            }
        }
    }
}
