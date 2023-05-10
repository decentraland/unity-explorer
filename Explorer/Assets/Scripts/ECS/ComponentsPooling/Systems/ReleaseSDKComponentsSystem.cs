using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;
using System;

namespace ECS.ComponentsPooling
{
    /// <summary>
    /// Called as a last step before entity destruction to return components to the pool
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    public partial class ReleaseSDKComponentsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
    {
        private readonly QueryDescription queryDescription = new QueryDescription().WithAll<DeleteEntityIntention>();

        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public ReleaseSDKComponentsSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        protected override void Update(float _)
        {
            var query = World.Query(in queryDescription);
            ReleaseComponentsToPool(in query);
        }

        public void FinalizeSDKComponents(in Query query)
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
