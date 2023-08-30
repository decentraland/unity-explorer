using Arch.Core;
using Arch.SystemGroups;
using DCL.AvatarRendering.AvatarShape.Systems;
using DCL.PluginSystem.Global;

public class AvatarShapePlugin : IDCLGlobalPluginWithoutSettings
{
    public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
    {
        LoadAvatarSystem.InjectToWorld(ref builder);
    }
}
