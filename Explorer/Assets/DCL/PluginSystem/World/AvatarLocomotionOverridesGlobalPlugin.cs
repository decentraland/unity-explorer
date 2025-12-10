using Arch.Core;
using Arch.SystemGroups;
using Cysharp.Threading.Tasks;
using DCL.PluginSystem;
using DCL.PluginSystem.Global;
using DCL.SDKComponents.AvatarLocomotion.Systems;
using ECS.SceneLifeCycle;
using System.Threading;

namespace DCL.SDKComponents.AvatarLocomotion
{
    public class AvatarLocomotionOverridesGlobalPlugin : IDCLGlobalPlugin<AvatarLocomotionOverridesGlobalPlugin.Settings>
    {
        private Settings? settings;

        public UniTask InitializeAsync(Settings settings, CancellationToken ct)
        {
            this.settings = settings;

            return UniTask.CompletedTask;
        }

        public void Dispose()
        {
            // noop
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<World> builder, in GlobalPluginArguments arguments)
        {
            SetupAvatarLocomotionOverridesSystem.InjectToWorld(ref builder);
            ApplyAvatarLocomotionOverridesSystem.InjectToWorld(ref builder, settings);
        }

        public class Settings : IDCLPluginSettings
        {
            public float MaxMovementSpeed = 500;

            public float MaxJumpHeight = 200;
        }
    }
}
