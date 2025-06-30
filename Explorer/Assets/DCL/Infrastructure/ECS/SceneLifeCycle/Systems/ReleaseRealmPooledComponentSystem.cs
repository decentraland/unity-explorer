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
        private readonly ReleaseReferenceComponentsSystem.ReleaseComponentsToPoolOperation finalizeOperation;

        public ReleaseRealmPooledComponentSystem(IComponentPoolsRegistry componentPoolsRegistry)
        {
            finalizeOperation = new ReleaseReferenceComponentsSystem.ReleaseComponentsToPoolOperation(componentPoolsRegistry);
        }

        public void FinalizeComponents(in Query query)
        {
            finalizeOperation.ExecuteInstantly(query);
        }
    }
}
