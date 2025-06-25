using Arch.Core;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections.Generic;
using UnityEngine.Pool;
using Utility.Ownership;

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
        ///     Executes certain clean-up logic on SDK Components in budgeted manner preventing spikes.
        ///     Operation must be consumed until Complete when called.
        ///     Otherwise, it's undefined behaviour because implementation may save intermediate state.
        /// </summary>
        /// <param name="query">All = typeof(CRDTEntity)</param>
        /// <param name="budget">Budget the implementing system must consider to avoid spikes</param>
        /// <param name="cleanUpMarker">System marks the marker if it didn't succeed to fully clean its resources due budget limitation</param>
        BudgetedIteratorExecuteResult BudgetedFinalizeComponents(in Query query, IPerformanceBudget budget)
        {
            ReportHub.LogError(ReportCategory.ECS, $"IFinalizeWorldSystem.BudgetedFinalizeComponents called with default implementation. Performed sync in release. Behaviour should not be reachable: {this.GetType().FullName}");
            this.FinalizeComponents(query);
            return BudgetedIteratorExecuteResult.COMPLETE;
        }
    }

    public enum BudgetedIteratorExecuteResult
    {
        PARTIAL = 0,
        COMPLETE = 1,
    }

    /// <summary>
    /// Copy Safe (Points to the same entity). Not thread-safe.
    /// </summary>
    public readonly struct BudgetedIterator<TOperation> : IDisposable where TOperation: IBudgetedIteratorOperation
    {
        private static readonly ObjectPool<Box<BudgetedIteratorExecuteResult>> POOL = new
        (
            createFunc: static () => new Box<BudgetedIteratorExecuteResult>(BudgetedIteratorExecuteResult.PARTIAL),
            actionOnRelease: static box => box.Value = BudgetedIteratorExecuteResult.PARTIAL
        );

        private readonly Query query;
        private readonly HashSet<(int entityIndex, int arrayIndex)> handledEntities;
        private readonly IPerformanceBudget performanceBudget;
        private readonly TOperation operation;
        private readonly Box<BudgetedIteratorExecuteResult> executeResult;

        public BudgetedIterator(Query query, IPerformanceBudget performanceBudget, TOperation operation) : this()
        {
            this.query = query;
            this.performanceBudget = performanceBudget;
            this.operation = operation;
            handledEntities = HashSetPool<(int entityIndex, int arrayIndex)>.Get()!;
            executeResult = POOL.Get()!;
        }

        public void Dispose()
        {
            HashSetPool<(int entityIndex, int arrayIndex)>.Release(handledEntities);
            POOL.Release(executeResult);
        }

        public BudgetedIteratorExecuteResult Execute()
        {
            if (executeResult.Value is BudgetedIteratorExecuteResult.COMPLETE)
                return BudgetedIteratorExecuteResult.COMPLETE;

            // Profiling required, O(N^4)
            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                // it does not allocate, it's not a copy
                Array[] array2D = chunk.Components;

                foreach (int entityIndex in chunk)
                    for (var i = 0; i < array2D.Length; i++)
                    {
                        if (performanceBudget.TrySpendBudget() == false)
                            return executeResult.Value = BudgetedIteratorExecuteResult.PARTIAL;

                        (int entityIndex, int arrayIndex) key = (entityIndex, i);

                        if (handledEntities.Contains(key))
                            continue;

                        operation.Execute(array2D, entityIndex, i);
                        handledEntities.Add(key);
                    }
            }

            return executeResult.Value = BudgetedIteratorExecuteResult.COMPLETE;
        }
    }

    public interface IBudgetedIteratorOperation
    {
        /// <summary>
        /// Method guarantees to be exception free.
        /// </summary>
        void Execute(Array[] array2D, int entityIndex, int arrayIndex);
    }

    public static class BudgetedIteratorOperationExtensions
    {
        public static void ExecuteInstantly<T>(this T operation, Query query) where T : IBudgetedIteratorOperation
        {
            new BudgetedIterator<T>(query, NullPerformanceBudget.INSTANCE, operation).Execute();
        }

    }
}
