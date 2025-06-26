using Arch.Core;
using DCL.Diagnostics;
using DCL.Optimization.PerformanceBudgeting;
using System;
using System.Collections;
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
        BudgetedIterator BudgetedFinalizeComponents(in Query query, IPerformanceBudget budget)
        {
            ReportHub.LogError(ReportCategory.ECS, $"{nameof(IFinalizeWorldSystem)}.{nameof(BudgetedFinalizeComponents)} called with default implementation. Performed sync in release. Behaviour should not be reachable: {GetType().FullName}");
            this.FinalizeComponents(query);
            return BudgetedIteratorExecuteResult.COMPLETE;
        }
    }

    public enum BudgetedIteratorExecuteResult : byte
    {
        PARTIAL = 0,
        COMPLETE = 1,
    }

    /// <summary>
    /// Copy Safe (Points to the same entity). Not thread-safe.
    /// </summary>
    public ref struct BudgetedIterator
    {
        private readonly Query query;
        private readonly IPerformanceBudget performanceBudget;
        private readonly BudgetedIteratorOperation operation;

        private QueryChunkEnumerator queryChunkIterator;
        private EntityEnumerator entityEnumerator;
        private bool isInitialized;

        public BudgetedIterator(Query query, IPerformanceBudget performanceBudget, BudgetedIteratorOperation operation) : this()
        {
            this.query = query;
            this.performanceBudget = performanceBudget;
            this.operation = operation;

            Reset();
        }

        public void Dispose()
        {
        }

        /// <summary>
        ///     It will return false if the budget has exceeded or the query is complete.
        /// </summary>
        /// <returns></returns>
        public bool MoveNext()
        {
            // 1. If it's the first move the queryChunkIterator should be moved first
            // 2. If entityEnumerator can be moved, it should be moved
            // 3. If entityEnumerator is not available, queryChunkIterator should be moved again

            if (!isInitialized)
            {
                isInitialized = true;

                if (!queryChunkIterator.MoveNext())
                {
                    Current = BudgetedIteratorExecuteResult.COMPLETE;
                    return false;
                }
            }

            Current = BudgetedIteratorExecuteResult.PARTIAL;

            while (performanceBudget.TrySpendBudget())
            {
                Array[] array2D = queryChunkIterator.Current.Components;

                // Move to the next entity
                if (entityEnumerator.MoveNext())
                {
                    int entityIndex = entityEnumerator.Current;

                    // Don't budget the component within the same entity
                    for (var arrayIndex = 0; arrayIndex < array2D.Length; arrayIndex++)
                        operation(array2D[arrayIndex], entityIndex);
                }

                if (queryChunkIterator.MoveNext())
                    entityEnumerator = queryChunkIterator.Current.GetEnumerator();
                else
                    break;
            }

            Current = BudgetedIteratorExecuteResult.COMPLETE;
            return false;
        }

        public void Reset()
        {
            // Un-initialized state => must move once to start
            queryChunkIterator = query.GetChunkIterator().GetEnumerator();
            entityEnumerator = default(EntityEnumerator);
        }

        public BudgetedIteratorExecuteResult Current { get; private set; }
    }

    public delegate void BudgetedIteratorOperation(Array componentsArray, int entityIndex);

    public static class BudgetedIteratorOperationExtensions
    {
        public static void ExecuteInstantly<T>(this T operation, Query query) where T: IBudgetedIteratorOperation
        {
            new BudgetedIterator<T>(query, NullPerformanceBudget.INSTANCE, operation).Execute();
        }
    }

    public readonly struct ForeachBudgetedIteratorOperation<TForeach, TComponent> where TForeach: IForEach<TComponent>
    {
        private readonly TForeach forEach;

        public ForeachBudgetedIteratorOperation(TForeach forEach)
        {
            this.forEach = forEach;
        }

        public void Execute(Array array, int entityIndex)
        {
            if (array.GetType().GetElementType() == typeof(TComponent))
            {
                var componentArray = (TComponent[])array;
                ref var component = ref componentArray[entityIndex];
                forEach.Update(ref component);
            }
        }
    }
}
