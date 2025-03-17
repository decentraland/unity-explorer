using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.Throttling;
using DCL.Optimization.Pools;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

namespace ECS.ComponentsPooling.Systems
{
    /// <summary>
    ///     Releases components with poolable fields in a generic non-allocating manner
    /// </summary>
    [UpdateInGroup(typeof(CleanUpGroup))]
    [ThrottlingEnabled]
    public partial class ReleasePoolableComponentSystem<T, TProvider> : BaseUnityLoopSystem, IFinalizeWorldSystem
        where TProvider: IPoolableComponentProvider<T> where T: class
    {
        private readonly QueryDescription entityDestroyQuery = new QueryDescription()
           .WithAll<DeleteEntityIntention, TProvider>();

        private readonly QueryDescription finalizeQuery = new QueryDescription()
           .WithAll<TProvider>();

        private Finalize finalize;
        private ReleaseOnEntityDestroy releaseOnEntityDestroy;

        public ReleasePoolableComponentSystem(World world, IComponentPoolsRegistry poolsRegistry) : base(world)
        {
            finalize = new Finalize(poolsRegistry);
            releaseOnEntityDestroy = new ReleaseOnEntityDestroy(poolsRegistry);
        }

        protected override void Update(float t)
        {
            World.InlineQuery<ReleaseOnEntityDestroy, TProvider, DeleteEntityIntention>(in entityDestroyQuery, ref releaseOnEntityDestroy);
        }

        public void FinalizeComponents(in Query query)
        {
            World.InlineQuery<Finalize, TProvider>(in finalizeQuery, ref finalize);
        }

        private readonly struct ReleaseOnEntityDestroy : IForEach<TProvider, DeleteEntityIntention>
        {
            private readonly IComponentPoolsRegistry poolsRegistry;

            public ReleaseOnEntityDestroy(IComponentPoolsRegistry poolsRegistry)
            {
                this.poolsRegistry = poolsRegistry;
            }

            public void Update(ref TProvider provider, ref DeleteEntityIntention deleteEntityIntention)
            {
                // If deletion was delayed so should be the release
                if (deleteEntityIntention.DeferDeletion) return;

                poolsRegistry.GetPool(provider.PoolableComponentType).Release(provider.PoolableComponent);
                provider.Dispose();
            }
        }

        private readonly struct Finalize : IForEach<TProvider>
        {
            private readonly IComponentPoolsRegistry poolsRegistry;

            public Finalize(IComponentPoolsRegistry poolsRegistry)
            {
                this.poolsRegistry = poolsRegistry;
            }

            public void Update(ref TProvider provider)
            {
                poolsRegistry.GetPool(provider.PoolableComponentType).Release(provider.PoolableComponent);
                provider.Dispose();
            }
        }
    }
}
