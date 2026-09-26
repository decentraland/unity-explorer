using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
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

        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public ReleaseReferenceComponentsSystem(World world, IComponentPoolsRegistry componentPoolsRegistry) : base(world)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        protected override void Update(float _)
        {
            Query query = World.Query(in queryDescription);
            ReleaseComponentsToPool(in query);
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseComponentsToPool(in query);
        }

        private void ReleaseComponentsToPool(in Query query)
        {
            ReleaseComponentsToPool(query, componentPoolsRegistry);
        }

        public static void ReleaseComponentsToPool(in Query query, IComponentPoolsRegistry componentPoolsRegistry)
        {
            // Profiling required, O(N^4)
            foreach (ref Chunk chunk in query.GetChunkIterator())
            {
                // it does not allocate, it's not a copy
                Array[] array2D = chunk.Components;

                foreach (int entityIndex in chunk)
                {
                    for (var i = 0; i < array2D.Length; i++)
                    {
                        // if it is called on a value type it will cause an allocation
                        if (array2D[i].GetType().GetElementType().IsValueType) continue;

                        object component = array2D[i].GetValue(entityIndex);
                        Type type = component.GetType();

                        if (componentPoolsRegistry.TryGetPool(type, out IComponentPool pool))
                        {
                            pool.Release(component);
                        }
                    }
                }
            }
        }
    }
}
