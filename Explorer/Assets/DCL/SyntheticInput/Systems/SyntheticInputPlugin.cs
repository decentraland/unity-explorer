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
        private readonly SyntheticInputAgent agent;
        private readonly UiAutomationServices uiAutomation;

        public SyntheticInputPlugin(IScenesCache scenesCache, IEntityCollidersGlobalCache entityCollidersGlobalCache, SyntheticInputAgent agent, UiAutomationServices uiAutomation)
        {
            this.scenesCache = scenesCache;
            this.entityCollidersGlobalCache = entityCollidersGlobalCache;
            this.agent = agent;
            this.uiAutomation = uiAutomation;
        }

        public void Dispose()
        {
#if ALTTESTER
            AltTester.WorldAutomationProbe.Uninstall();
            AltTester.NavigationAutomationProbe.Uninstall();
            AltTester.UiAutomationProbe.Uninstall();
#endif
            uiAutomation.Dispose();
        }

        public void InjectToWorld(ref ArchSystemsWorldBuilder<Arch.Core.World> builder, in GlobalPluginArguments arguments)
        {
            SyntheticMovementInputSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            SyntheticPointerEventSystem.InjectToWorld(ref builder, scenesCache, entityCollidersGlobalCache, arguments.PlayerEntity, uiAutomation.TryFindUiCoverAt);
            SyntheticCameraLookSystem.InjectToWorld(ref builder, arguments.PlayerEntity);
            UiVirtualDeviceGestureSystem.InjectToWorld(ref builder, arguments.PlayerEntity, uiAutomation.Devices);

            // The facade cannot see the interaction pipeline's components, so the reader is handed to it from here.
            agent.InstallReticleProbe(new SyntheticReticleReader(builder.World, scenesCache).Read);
        }
    }
}
