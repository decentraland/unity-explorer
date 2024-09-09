using CommunicationData.URLHelpers;
using Cysharp.Threading.Tasks;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Utility;

namespace DCL.WebRequests
{
    public class BudgetedWebRequestController : IWebRequestController
    {
        private readonly ObjectPool<ConcurrentLoadingPerformanceBudget> budgetPool;

        private readonly IWebRequestController origin;
        private readonly int perDomainBudget;

        private readonly IReleasablePerformanceBudget totalBudget;
        private readonly Dictionary<ReadOnlyMemory<char>, ConcurrentLoadingPerformanceBudget> perDomainBudgets = new (50, new StringUtils.StringMemoryIgnoreCaseComparer());

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
            ReadOnlyMemory<char> baseDomain = ReadOnlyMemory<char>.Empty;

            // Try bypass total budget
            while (!totalBudget.TrySpendBudget(out totalBudgetAcquired))
                await UniTask.Yield(envelope.Ct);

            try
            {
                baseDomain = envelope.CommonArguments.URL.GetBaseDomain();

                if (baseDomain.Length > 0)
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
