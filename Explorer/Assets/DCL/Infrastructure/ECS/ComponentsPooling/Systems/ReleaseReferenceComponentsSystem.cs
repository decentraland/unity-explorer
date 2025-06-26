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
        private readonly IComponentPoolsRegistry componentPoolsRegistry;
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<DeleteEntityIntention>();

        public bool IsBudgetedFinalizeSupported => true;

        private readonly BudgetedIteratorOperation operation;

        public ReleaseReferenceComponentsSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
            operation = Execute;
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

        public BudgetedIterator BudgetedFinalizeComponents(in Query query, IPerformanceBudget budget) =>
            new (query, budget, operation);

        private void Execute(Array array, int entityIndex)
        {
            // if it is called on a value type it will cause an allocation
            if (array.GetType().GetElementType()!.IsValueType) return;

            object component = array.GetValue(entityIndex)!;
            Type type = component.GetType();

            if (componentPoolsRegistry.TryGetPool(type, out IComponentPool pool))
                pool.Release(component);
        }
    }
}
