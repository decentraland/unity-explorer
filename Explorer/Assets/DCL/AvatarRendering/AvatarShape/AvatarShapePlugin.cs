using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.PluginSystem.Global;
using ECS.StreamableLoading.DeferredLoading.BudgetProvider;

public class AvatarShapePlugin : IDCLGlobalPluginWithoutSettings
{
    public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
    {
        LoadAvatarWearableSystem.InjectToWorld(ref builder);
        InstantiateAvatarSystem.InjectToWorld(ref builder, new NullBudgetProvider());
    }
}
