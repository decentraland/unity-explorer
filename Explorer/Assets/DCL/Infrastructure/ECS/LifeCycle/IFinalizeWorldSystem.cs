using Arch.Core;
using DCL.Optimization.PerformanceBudgeting;

namespace ECS.LifeCycle
{
    /// <summary>
    ///     Executes clean-up logic right before World destruction
    /// </summary>
    public interface IFinalizeWorldSystem
    {
        /// <summary>
        ///     Executes certain clean-up logic on SDK Components
        /// </summary>
        /// <param name="query">All = typeof(CRDTEntity)</param>
        void FinalizeComponents(in Query query);

        bool IsBudgetedFinalizeSupported => false;

        /// <summary>
        ///     Executes certain clean-up logic on SDK Components in budgeted manner preventing spikes
        /// </summary>
        /// <param name="query">All = typeof(CRDTEntity)</param>
        /// <param name="budget">Budget the implementing system must consider to avoid spikes</param>
        /// <param name="cleanUpMarker">System marks the marker if it didn't succeed to fully clean its resources due budget limitation</param>
        void BudgetedFinalizeComponents(in Query query, IPerformanceBudget budget, CleanUpMarker cleanUpMarker) { }
    }

    public class CleanUpMarker
    {
        public bool IsFullyCleaned { get; private set; } = true;

        public void Purify()
        {
            IsFullyCleaned = true;
        }

        private void MarkNotFullyClean()
        {
            IsFullyCleaned = false;
        }

        public bool TryProceedWithBudget(IPerformanceBudget budget)
        {
            if (budget.TrySpendBudget())
                return true;

            MarkNotFullyClean();
            return false;
        }
    }

    public readonly struct BudgetedFinalize<TFinalize, TProvider> : IForEach<TProvider> where TFinalize: IForEach<TProvider>
    {
        private readonly TFinalize finalize;
        private readonly IPerformanceBudget performanceBudget;
        private readonly CleanUpMarker cleanUpMarker;

        public BudgetedFinalize(TFinalize finalize, IPerformanceBudget performanceBudget, CleanUpMarker cleanUpMarker)
        {
            this.finalize = finalize;
            this.performanceBudget = performanceBudget;
            this.cleanUpMarker = cleanUpMarker;
        }

        public void Update(ref TProvider provider)
        {
            if (cleanUpMarker.TryProceedWithBudget(performanceBudget))
                finalize.Update(ref provider);
        }
    }
}
