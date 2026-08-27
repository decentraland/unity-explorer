using Arch.SystemGroups;
using DCL.Interaction.Utility;
using DCL.PluginSystem.Global;
using ECS.SceneLifeCycle;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Registers the synthetic input simulation systems that deliver automation-driver requests
    ///     (installed through <see cref="SyntheticInputAgent" />) via the production input pipelines.
    ///     Registered only when an automation driver is enabled.
    /// </summary>
    public class SyntheticInputPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;

        public SyntheticInputPlugin(IScenesCache scenesCache, IEntityCollidersGlobalCache entityCollidersGlobalCache)
        {
            this.scenesCache = scenesCache;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SyntheticMovementInputSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            SyntheticPointerEventSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity);
            SyntheticCameraLookSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
        }
    }
}
