using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.Abstract;
using ECS.SceneLifeCycle;

namespace DCL.SkyBox
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SkyboxTimeUpdateSystem : BaseUnityLoopSystem
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly SkyboxRenderController skyboxRenderController;
        private readonly SkyboxStateMachine stateMachine;

        private SkyboxTimeUpdateSystem(World world,
            SkyboxSettingsAsset skyboxSettings,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            SkyboxRenderController skyboxRenderController) : base(world)
        {
            var transition = new InterpolateTimeOfDayState(skyboxSettings);

            stateMachine = new SkyboxStateMachine(new ISkyboxState[]
            {
                new SDKComponentState(skyboxSettings, sceneRestrictionController, transition),
                new SceneMetadataState(scenesCache, skyboxSettings, sceneRestrictionController, transition),
                new UIOverrideState(skyboxSettings),
                new GlobalTimeState(skyboxSettings, transition),
            });

            this.skyboxSettings = skyboxSettings;
            this.skyboxRenderController = skyboxRenderController;
        }

        protected override void Update(float deltaTime)
        {
            stateMachine.Update(deltaTime);

            skyboxRenderController.UpdateSkybox(skyboxSettings.TimeOfDayNormalized);
        }
    }
}
