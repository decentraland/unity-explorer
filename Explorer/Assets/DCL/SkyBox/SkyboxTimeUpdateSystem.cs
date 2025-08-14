using Arch.Core;
using Arch.System;
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
        private Entity skyboxEntity;
        private bool enabled = true;

        private SkyboxTimeUpdateSystem(World world,
            SkyboxSettingsAsset skyboxSettings,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            SkyboxRenderController skyboxRenderController,
            Entity skyboxEntity) : base(world)
        {
            var transition = new InterpolateTimeOfDayState(skyboxSettings);

            stateMachine = new SkyboxStateMachine(new ISkyboxState[]
            {
                new SDKComponentState(skyboxSettings, sceneRestrictionController, transition, scenesCache),
                new SceneMetadataState(scenesCache, skyboxSettings, sceneRestrictionController, transition),
                new UIOverrideState(skyboxSettings, transition),
                new GlobalTimeState(skyboxSettings, transition),
            });

            this.skyboxSettings = skyboxSettings;
            this.skyboxRenderController = skyboxRenderController;
            this.skyboxEntity = skyboxEntity;
        }

        protected override void Update(float deltaTime)
        {
            // World.Has<PauseSkyboxTimeUpdate>(skyboxEntity)
            if (!enabled) return;

            stateMachine.Update(deltaTime);
            skyboxRenderController.UpdateSkybox(skyboxSettings.TimeOfDayNormalized);

            HandleSkyboxTimeUpdatePauseQuery(World);
            HandleSkyboxTimeUpdateUnpauseQuery(World);
        }

        [Query]
        [All(typeof(SkyboxComponent), typeof(PauseSkyboxTimeUpdate))]
        private void HandleSkyboxTimeUpdatePause()
        {
            enabled = false;
        }

        [Query]
        [All(typeof(SkyboxComponent))]
        [None(typeof(PauseSkyboxTimeUpdate))]
        private void HandleSkyboxTimeUpdateUnpause()
        {
            enabled = true;
        }
    }
}
