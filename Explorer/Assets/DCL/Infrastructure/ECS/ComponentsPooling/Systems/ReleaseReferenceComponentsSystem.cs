using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Optimization.PerformanceBudgeting;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using System;

namespace ECS.ComponentsPooling.Systems
{
    /// <summary>
    ///     Called as a last step before entity destruction to return reference components to the pool
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [ThrottlingEnabled]
    public partial class ReleaseReferenceComponentsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<DeleteEntityIntention>();

        private readonly ReleaseComponentsToPoolOperation finalizeOperation;
        private BudgetedIterator<ReleaseComponentsToPoolOperation>? finalizeIterator;

        public bool IsBudgetedFinalizeSupported => true;

        public ReleaseReferenceComponentsSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            finalizeOperation = new ReleaseComponentsToPoolOperation(componentPoolsRegistry);
        }

        protected override void OnDispose()
        {
            finalizeIterator?.Dispose();
            finalizeIterator = null;
            base.OnDispose();
        }

        protected override void Update(float _)
        {
            Query query = World.Query(in queryDescription);
            finalizeOperation.ExecuteInstantly(query);
        }

        public void FinalizeComponents(in Query query)
        {
            finalizeOperation.ExecuteInstantly(query);
        }

        public BudgetedIteratorExecuteResult BudgetedFinalizeComponents(in Query query, IPerformanceBudget budget)
        {
            finalizeIterator ??= new BudgetedIterator<ReleaseComponentsToPoolOperation>(
                query,
                budget,
                finalizeOperation
            );

            var result = finalizeIterator.Value.Execute();

            if (result == BudgetedIteratorExecuteResult.COMPLETE)
            {
                finalizeIterator.Value.Dispose();
                finalizeIterator = null;
            }

            return result;
        }

        public readonly struct ReleaseComponentsToPoolOperation : IBudgetedIteratorOperation
        {
            private readonly IComponentPoolsRegistry componentPoolsRegistry;

            public ReleaseComponentsToPoolOperation(IComponentPoolsRegistry componentPoolsRegistry)
            {
                this.componentPoolsRegistry = componentPoolsRegistry;
            }

            public void Execute(Array[] array2D, int entityIndex, int i)
            {
                // if it is called on a value type it will cause an allocation
                if (array2D[i].GetType().GetElementType()!.IsValueType) return;

                object component = array2D[i].GetValue(entityIndex)!;
                Type type = component.GetType();

                if (componentPoolsRegistry.TryGetPool(type, out IComponentPool pool))
                    pool.Release(component);
            }
        }
    }
}
