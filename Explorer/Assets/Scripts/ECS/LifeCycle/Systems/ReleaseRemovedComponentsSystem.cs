using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;
using ECS.LifeCycle;
using ECS.LifeCycle.Components;

[UpdateInGroup(typeof(CleanUpGroup))]
public class ReleaseRemovedComponentsSystem : BaseUnityLoopSystem, IFinalizeWorldSystem
{
    public ReleaseRemovedComponentsSystem(World world) : base(world) { }

    protected override void Update(float t) { }

    public void FinalizeComponents(in Query query)
    {
        World.Query(in new QueryDescription().WithAll<RemovedComponents>(),
            (ref RemovedComponents removedComponents) => removedComponents.Dispose());
    }
}
