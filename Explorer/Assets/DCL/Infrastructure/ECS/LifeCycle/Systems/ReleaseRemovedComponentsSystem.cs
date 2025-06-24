using Arch.Core;
using Arch.SystemGroups;
using DCL.Optimization.PerformanceBudgeting;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle.Components;

namespace ECS.LifeCycle.Systems
{
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseRemovedComponentsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<RemovedComponents>();

        // A hack to avoid lambda capture and allocations
        private static (IPerformanceBudget budget, CleanUpMarker cleanUpMarker) context;

        public ReleaseRemovedComponentsSystem(World world) : base(world) { }

        protected override void Update(float t) { }

        public void FinalizeComponents(in Query query, IPerformanceBudget budget, CleanUpMarker cleanUpMarker)
        {
            context = (budget, cleanUpMarker);

            World.Query(
                in queryDescription,
                static (ref RemovedComponents removedComponents) =>
                {
                    if (context.cleanUpMarker.TryProceedWithBudget(context.budget))
                        removedComponents.Dispose();
                }
            );
        }
    }
}
