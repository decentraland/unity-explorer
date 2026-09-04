using Arch.SystemGroups;
using DCL.Interaction.Utility;
using DCL.PluginSystem.Global;
using DCL.SyntheticInput.UiSimulation;
using ECS.SceneLifeCycle;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>
    ///     Registers the synthetic input simulation systems that deliver automation-driver requests
    ///     (installed through <see cref="SyntheticInputAgent" /> and <see cref="UiAutomationServices" />) via the
    ///     production input pipelines, and owns the UI-automation session (virtual devices included).
    ///     Registered only when an automation driver is enabled.
    /// </summary>
    public class SyntheticInputPlugin : IDCLGlobalPluginWithoutSettings
    {
        private readonly IScenesCache scenesCache;
        private readonly IEntityCollidersGlobalCache entityCollidersGlobalCache;
        private readonly UiAutomationServices uiAutomation;

        public SyntheticInputPlugin(IScenesCache scenesCache, IEntityCollidersGlobalCache entityCollidersGlobalCache, UiAutomationServices uiAutomation)
        {
            this.scenesCache = scenesCache;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.uiAutomation = uiAutomation;
        }

        public void Dispose() =>
            uiAutomation.Dispose();

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SyntheticMovementInputSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            SyntheticPointerEventSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity, uiAutomation.TryFindUiCoverAt);
            SyntheticCameraLookSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            UiVirtualDeviceGestureSystem.InjectToWorld(ref builder, arguments.PlayerEntity, uiAutomation.Devices);
        }
    }
}
