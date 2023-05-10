using Arch.Core;
using Arch.SystemGroups;
using ECS.Abstract;
using ECS.Groups;

[UpdateInGroup(typeof(CleanUpGroup))]
public partial class ClearDirtyStateSystem : BaseUnityLoopSystem
{

    protected override void Update(float t)
    {
        World.Query(in new QueryDescription().WithAll<IsDirtyState>(), (ref IsDirtyState isDirtyState) =>
        {
            isDirtyState.hasBeenCleaned = true;
        });
    }

    public ClearDirtyStateSystem(World world) : base(world)
    {
    }
}
