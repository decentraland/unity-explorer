using Arch.Core;
using DCL.Optimization.Pools;
using ECS.ComponentsPooling.Systems;
using ECS.LifeCycle;

namespace ECS.SceneLifeCycle.Systems
{
    /// <summary>
    ///     Release Pooled components when Realms clears
    /// </summary>
    public class ReleaseRealmPooledComponentSystem : IFinalizeWorldSystem
    {
        private readonly IComponentPoolsRegistry componentPoolsRegistry;

        public ReleaseRealmPooledComponentSystem(IComponentPoolsRegistry componentPoolsRegistry)
        {
            this.componentPoolsRegistry = componentPoolsRegistry;
        }

        public void FinalizeComponents(in Query query)
        {
            ReleaseReferenceComponentsSystem.ReleaseComponentsToPool(in query, componentPoolsRegistry);
        }
    }
}
