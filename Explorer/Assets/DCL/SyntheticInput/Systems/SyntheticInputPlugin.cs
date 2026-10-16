using Arch.SystemGroups;
using DCL.Interaction.Utility;
using DCL.PluginSystem.Global;
using DCL.SyntheticInput.UiSimulation;
using ECS.SceneLifeCycle;

namespace DCL.SyntheticInput.Systems
{
    /// <summary>Registers the synthetic input systems and owns the UI-automation session, virtual devices included.</summary>
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

        public void Dispose()
        {
#if ALTTESTER
            DCL.SyntheticInput.AltTester.WorldAutomationProbe.Uninstall();
            DCL.SyntheticInput.AltTester.NavigationAutomationProbe.Uninstall();
            DCL.SyntheticInput.AltTester.UiAutomationProbe.Uninstall();
#endif
            uiAutomation.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SyntheticMovementInputSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            SyntheticPointerEventSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity, uiAutomation.TryFindUiCoverAt);
            SyntheticCameraLookSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            UiVirtualDeviceGestureSystem.InjectToWorld(ref builder, arguments.PlayerEntity, uiAutomation.Devices);
        }
    }
}
