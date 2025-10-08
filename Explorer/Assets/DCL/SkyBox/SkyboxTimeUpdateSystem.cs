using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.SkyBox.Components;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using UnityEngine;

namespace DCL.SkyBox
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SkyboxTimeUpdateSystem : BaseUnityLoopSystem
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly SkyboxRenderController skyboxRenderController;
        private readonly SkyboxStateMachine stateMachine;
        private readonly Entity skyboxEntity;

        private ISkyboxState globalTimeState;

        private SkyboxTimeUpdateSystem(World world,
            SkyboxSettingsAsset skyboxSettings,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            SkyboxRenderController skyboxRenderController,
            Entity skyboxEntity) : base(world)
        {
            InterpolateTimeOfDayState transition = new InterpolateTimeOfDayState(skyboxSettings);
            SkyboxTimeProgressionService timeProgressionService = new SkyboxTimeProgressionService(skyboxSettings, transition);

            globalTimeState = new GlobalTimeState(skyboxSettings, transition, timeProgressionService);

            stateMachine = new SkyboxStateMachine(new ISkyboxState[]
            {
                new SDKComponentState(skyboxSettings, sceneRestrictionController, transition, scenesCache),
                new SceneMetadataState(scenesCache, skyboxSettings, sceneRestrictionController, transition, timeProgressionService),
                new UIOverrideState(skyboxSettings, transition),
                globalTimeState,
            });

            this.skyboxSettings = skyboxSettings;
            this.skyboxRenderController = skyboxRenderController;
            this.skyboxEntity = skyboxEntity;
        }

        protected override void Update(float deltaTime)
        {
            if (World.Has<PauseSkyboxTimeUpdate>(skyboxEntity))
            {
                // In case a transition is needed after the time update is resumed,
                // we set the global time state
                stateMachine.CurrentState = globalTimeState;
                return;
            }

            stateMachine.Update(deltaTime);
            skyboxRenderController.UpdateSkybox(skyboxSettings.TimeOfDayNormalized);
        }
    }
}
