using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace ECS.ComponentsPooling.Systems
{
    /// <summary>
    /// Called as a last step before entity destruction to return reference components to the pool
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [ThrottlingEnabled]
    public partial class ReleaseReferenceComponentsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<DeleteEntityIntention>();

        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public ReleaseReferenceComponentsSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        protected override void Update(float _)
        {
            var query = World.Query(in queryDescription);
            ReleaseComponentsToPool(in query);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseComponentsToPool(in query);
        }

        private void ReleaseComponentsToPool(in Query query)
        {
            // Profiling required, O(N^4)
            foreach (ref var chunk in query.GetChunkIterator())
            {
                // it does not allocate, it's not a copy
                var array2D = chunk.Components;

                foreach (var entityIndex in chunk)
                {
                    for (var i = 0; i < array2D.Length; i++)
                    {
                        // if it is called on a value type it will cause an allocation
                        if (array2D[i].GetType().GetElementType().IsValueType) continue;

                        var component = array2D[i].GetValue(entityIndex);
                        var type = component.GetType();

                        if (componentPoolsRegistry.TryGetPool(type, out var pool))
                            pool.Release(component);
                    }
                }
            }
        }
    }
}
