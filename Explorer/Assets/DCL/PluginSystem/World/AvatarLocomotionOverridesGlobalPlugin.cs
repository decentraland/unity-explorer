using Arch.Core;
using Arch.SystemGroups;
using DCL.PluginSystem.Global;
using DCL.SDKComponents.AvatarLocomotion.Systems;
using ECS.SceneLifeCycle;

namespace DCL.SDKComponents.AvatarLocomotion
{
    public class AvatarLocomotionOverridesGlobalPlugin : IDCLGlobalPlugin
    {
        private readonly IScenesCache scenesCache;

        public AvatarLocomotionOverridesGlobalPlugin(IScenesCache scenesCache)
        {
            this.scenesCache = scenesCache;
        }

        public void Dispose()
        {
            // noop
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            SetupAvatarLocomotionOverridesSystem.InjectToWorld(ref builder);
            ApplyAvatarLocomotionOverridesSystem.InjectToWorld(ref builder);
            ClearAvatarLocomotionOverridesSystem.InjectToWorld(ref builder, scenesCache);
        }
    }
}
