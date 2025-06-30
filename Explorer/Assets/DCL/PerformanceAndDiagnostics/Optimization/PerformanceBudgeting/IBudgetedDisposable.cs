using Cysharp.Threading.Tasks;
using System;
using System.Collections.Generic;

namespace DCL.Optimization.PerformanceBudgeting
{
    public interface IBudgetedDisposable
    {
        /// <summary>
        /// Iterative dispose method. It allows to spread dispose load over frames to avoid spikes.
        /// Each next call triggers internal dispose logic.
        /// Synchronous replacement for IUniTaskAsyncDisposable when it's clear the dispose logic can be performed by chunks.
        /// </summary>
        IEnumerator<Unit> BudgetedDispose();
    }

    /// <summary>
    /// Type represents empty value.
    /// </summary>
    public readonly struct Unit { }

    public static class BudgetedDisposableExtensions
    {
        public static ImmediateDisposableWrap AsDisposable(this IBudgetedDisposable budgetedDisposable) =>
            new (budgetedDisposable);

        public static AsyncDisposableWrap AsAsyncDisposable(this IBudgetedDisposable budgetedDisposable, IPerformanceBudget performanceBudget) =>
            new (budgetedDisposable, performanceBudget);
    }

    public class ImmediateDisposableWrap : IDisposable
    {
        private readonly IBudgetedDisposable budgetedDisposable;

        public ImmediateDisposableWrap(IBudgetedDisposable budgetedDisposable)
        {
            this.budgetedDisposable = budgetedDisposable;
        }

        public void Dispose()
        {
            var enumerator = budgetedDisposable.BudgetedDispose();

            while (enumerator.MoveNext())
            {
                // Just consume the iterator immediately
            }
        }
    }

    public class AsyncDisposableWrap : IUniTaskAsyncDisposable
    {
        private readonly IBudgetedDisposable budgetedDisposable;
        private readonly IPerformanceBudget performanceBudget;

        public AsyncDisposableWrap(IBudgetedDisposable budgetedDisposable, IPerformanceBudget performanceBudget)
        {
            this.budgetedDisposable = budgetedDisposable;
            this.performanceBudget = performanceBudget;
        }

        public async UniTask DisposeAsync()
        {
            var enumerator = budgetedDisposable.BudgetedDispose();

            while (enumerator.MoveNext())
                if (performanceBudget.TrySpendBudget() == false)
                    await UniTask.NextFrame();
        }
    }
}
