using Arch.Core;
using Arch.System;
using ECS.Abstract;
using ECS.LifeCycle.Components;

namespace ECS.ComponentsPooling
{
    /// <summary>
    /// Called as a last step before entity destruction to return components to the pool
    /// </summary>
    public class ReleaseSDKComponentsSystem : BaseUnityLoopSystem
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
