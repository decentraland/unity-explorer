using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox.Components;
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
        private readonly Entity skyboxEntity;
        private bool needsTransitionReload;

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
            this.needsTransitionReload = false;
        }

        protected override void Update(float deltaTime)
        {
            if (World.Has<PauseSkyboxTimeUpdate>(skyboxEntity))
            {
                needsTransitionReload = true;
                return;
            }

            if (needsTransitionReload)
            {
                stateMachine.CurrentState?.Enter();
                needsTransitionReload = false;
            }

            stateMachine.Update(deltaTime);
            skyboxRenderController.UpdateSkybox(skyboxSettings.TimeOfDayNormalized);
        }
    }
}
