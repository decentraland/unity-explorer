using Arch.Core;
using Arch.SystemGroups;
using DCL.DebugUtilities;
using DCL.PluginSystem.Global;
using ECS.SceneLifeCycle.Debug;

namespace DCL.RealmNavigation
{
    public class RealmNavigationDebugPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly DebugWidgetBuilder? widgetBuilder;

        public RealmNavigationDebugPlugin(DebugWidgetBuilder? widgetBuilder)
        {
            this.widgetBuilder = widgetBuilder;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            if (widgetBuilder != null)
                AbortSceneLoadingDebugSystem.InjectToWorld(ref builder, widgetBuilder);
        }
    }
}
