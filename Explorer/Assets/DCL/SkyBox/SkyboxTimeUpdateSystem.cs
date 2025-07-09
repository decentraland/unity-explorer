using Arch.Core;
using Arch.SystemGroups;
using Arch.SystemGroups.DefaultSystemGroups;
using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.Abstract;
using ECS.SceneLifeCycle;
using JetBrains.Annotations;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.SkyBox
{
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial class SkyboxTimeUpdateSystem : BaseUnityLoopSystem
    {
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private readonly SkyboxRenderController skyboxRenderController;
        private readonly SkyboxStateMachine stateMachine;

        private float globalTimeOfDay;
        private float timeSinceLastUpdate;
        private ISceneFacade? scene;
        private bool hasCheckedCurrentScene;
        private bool hasSceneOverride;
        private float skyboxSpeedMultiplier;
        private bool wasUIControlledLastFrame;
        private bool wasSDKControlledLastFrame;

        private SkyboxTimeUpdateSystem(World world,
            SkyboxSettingsAsset skyboxSettings,
            IScenesCache scenesCache,
            ISceneRestrictionBusController sceneRestrictionController,
            SkyboxRenderController skyboxRenderController) : base(world)
        {
            stateMachine = new SkyboxStateMachine(new ISkyboxState[]
            {
                new SDKComponentState(skyboxSettings),
                new SceneMetadataState(scenesCache, skyboxSettings, sceneRestrictionController),
                new UIOverrideState(skyboxSettings),
                new GlobalTimeState(skyboxSettings)
            }, new InterpolateTimeOfDayState(skyboxSettings));

            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionController = sceneRestrictionController;
            this.skyboxRenderController = skyboxRenderController;
            globalTimeOfDay = skyboxSettings.initialTimeOfDay;
            skyboxSpeedMultiplier = skyboxSettings.SpeedMultiplier;

            // scenesCache.OnCurrentSceneChanged += OnSceneChanged;
        }

        protected override void Update(float deltaTime)
        {
            // UpdateGlobalTime(deltaTime);

            stateMachine.Update(deltaTime);

            if (skyboxSettings.ShouldUpdateSkybox)
            {
                skyboxRenderController.UpdateSkybox(skyboxSettings.TimeOfDayNormalized);
                skyboxSettings.ShouldUpdateSkybox = false;
            }

            // Check hierarchy from highest to lowest priority
            // if (TryHandleSDKComponent()) return;
            // if (TryHandleSceneOverride()) return;
            // if (TryHandleUIOverride()) return;
            // if (TryHandleFeatureFlag()) return;

            // Fallback to global time progression
            // HandleGlobalTimeProgression(deltaTime);
        }

        private bool TryHandleSDKComponent()
        {
            if (wasSDKControlledLastFrame && !skyboxSettings.IsSDKControlled && skyboxSettings.CanUIControl)
                TransitionToGlobalTime();

            wasSDKControlledLastFrame = skyboxSettings.IsSDKControlled;

            return wasSDKControlledLastFrame;
        }

        private bool TryHandleUIOverride()
        {
            // Check for UI control release
            if (wasUIControlledLastFrame && !skyboxSettings.IsUIControlled && skyboxSettings.CanUIControl)
                TransitionToGlobalTime();

            wasUIControlledLastFrame = skyboxSettings.IsUIControlled;

            return wasUIControlledLastFrame;
        }

        // private bool TryHandleFeatureFlag()
        // {
        //     if (!ffSkyboxSettings.HasValue) return false;
        //
        //     skyboxSettings.CanUIControl = true;
        //
        //     int speed = ffSkyboxSettings.Value.speed;
        //     skyboxSpeedMultiplier = Mathf.Abs(speed);
        //
        //     skyboxSettings.IsDayCycleEnabled = speed != 0;
        //     skyboxSettings.TargetTransitionTimeOfDay = ffSkyboxSettings.Value.time;
        //     skyboxSettings.IsTransitioning = true;
        //     skyboxSettings.ShouldUpdateSkybox = true;
        //
        //     return true;
        // }

        private void UpdateGlobalTime(float deltaTime)
        {
            // We always track the time of day so we can switch back to using it
            globalTimeOfDay += deltaTime * skyboxSpeedMultiplier / SkyboxSettingsAsset.SECONDS_IN_DAY;

            // Loop around at EOD
            if (globalTimeOfDay >= 1f)
                globalTimeOfDay = 0f;
        }

        private bool TryHandleSceneOverride()
        {
            if (!hasCheckedCurrentScene)
            {
                hasCheckedCurrentScene = true;

                if (scene?.SceneData.SceneEntityDefinition.metadata == null)
                {
                    hasSceneOverride = false;
                    return hasSceneOverride;
                }

                SceneMetadata sceneMetadata = scene.SceneData.SceneEntityDefinition.metadata;

                if (sceneMetadata is { worldConfiguration: { SkyboxConfig: { fixedTime: var worldTime } } })
                {
                    ApplySceneControlledFixedTime(worldTime);
                    hasSceneOverride = true;
                    return hasSceneOverride;
                }

                if (sceneMetadata is { skyboxConfig: { fixedTime: var sceneTime } })
                {
                    ApplySceneControlledFixedTime(sceneTime);
                    hasSceneOverride = true;
                    return hasSceneOverride;
                }
            }

            return hasSceneOverride;
        }

        private void ApplySceneControlledFixedTime(float sceneTime)
        {
            skyboxSettings.IsDayCycleEnabled = false;

            skyboxSettings.TransitionMode = TransitionMode.FORWARD;
            // skyboxSettings.IsTransitioning = true;
            skyboxSettings.TargetTransitionTimeOfDay = sceneTime;

            skyboxSettings.CanUIControl = false;
            sceneRestrictionController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeUILocked(SceneRestrictionsAction.APPLIED));
        }

        private void HandleGlobalTimeProgression(float deltaTime)
        {
            skyboxSettings.CanUIControl = true;

            timeSinceLastUpdate += deltaTime;

            if (timeSinceLastUpdate >= skyboxSettings.refreshInterval)
            {
                skyboxSettings.TimeOfDayNormalized = globalTimeOfDay;
                skyboxSettings.ShouldUpdateSkybox = true;
                timeSinceLastUpdate = 0f;
            }
        }

        private void OnSceneChanged(ISceneFacade? sceneFacade)
        {
            // scene = sceneFacade;
            // hasSceneOverride = false;
            // hasCheckedCurrentScene = false;
            //
            // if (sceneFacade == null && !ffSkyboxSettings.HasValue && !skyboxSettings.IsUIControlled)
            //     TransitionToGlobalTime();
        }

        private void TransitionToGlobalTime()
        {
            skyboxSpeedMultiplier = skyboxSettings.SpeedMultiplier;
            skyboxSettings.IsDayCycleEnabled = true;
            skyboxSettings.TransitionMode = TransitionMode.FORWARD;
            // skyboxSettings.IsTransitioning = true;
            skyboxSettings.TargetTransitionTimeOfDay = globalTimeOfDay;
        }
    }
}
