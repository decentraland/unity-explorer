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
        private struct FeatureFlagSkyboxSettings
        {
            public int time;
            public int speed;
        }

        private readonly FeatureFlagSkyboxSettings? ffSkyboxSettings = null;
        private readonly SkyboxSettingsAsset skyboxSettings;
        private readonly IScenesCache scenesCache;

        private float timeSinceLastUpdate;
        private float globalTimeOfDay { get; set; }
        private ISceneFacade scene;
        private bool hasCheckedCurrentScene = false;
        private bool hasSceneOverride = false;

        private float skyboxSpeedMultiplier;
        private readonly ISceneRestrictionBusController sceneRestrictionController;
        private bool wasUIControlledLastFrame;
        private bool wasSDKControlledLastFrame;

        private SkyboxTimeUpdateSystem([NotNull] World world, SkyboxSettingsAsset skyboxSettings, IScenesCache scenesCache, ISceneRestrictionBusController sceneRestrictionController) : base(world)
        {
            this.skyboxSettings = skyboxSettings;
            this.scenesCache = scenesCache;
            this.sceneRestrictionController = sceneRestrictionController;
            globalTimeOfDay = skyboxSettings.initialTimeOfDay;
            skyboxSpeedMultiplier = skyboxSettings.SpeedMultiplier;

            if (FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.SKYBOX_SETTINGS))
            {
                FeatureFlagsConfiguration.Instance.TryGetJsonPayload(
                    FeatureFlagsStrings.SKYBOX_SETTINGS,
                    FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT,
                    out ffSkyboxSettings);
            }

            scenesCache.OnCurrentSceneChanged += OnSceneChanged;
        }

        private void OnSceneChanged(ISceneFacade sceneFacade)
        {
            scene = sceneFacade;
            hasSceneOverride = false;
            hasCheckedCurrentScene = false;

            if (sceneFacade == null && !ffSkyboxSettings.HasValue && !skyboxSettings.IsUIControlled)
                TransitionToGlobalTime();
        }

        private void TransitionToGlobalTime()
        {
            skyboxSpeedMultiplier = skyboxSettings.SpeedMultiplier;
            skyboxSettings.IsDayCycleEnabled = true;
            skyboxSettings.TransitionMode = TransitionMode.FORWARD;
            skyboxSettings.IsTransitioning = true;
            skyboxSettings.TargetTransitionTimeOfDay = globalTimeOfDay;
        }

        protected override void Update(float deltaTime)
        {
            UpdateGlobalTime(deltaTime);

            if(TryHandleTransition(deltaTime)) return;

            // Check hierarchy from highest to lowest priority
            if (TryHandleSDKComponent()) return;
            if (TryHandleSceneOverride()) return;
            if (TryHandleUIOverride()) return;
            if (TryHandleFeatureFlag()) return;

            // Fallback to global time progression
            HandleGlobalTimeProgression(deltaTime);
        }

        private bool TryHandleTransition(float deltaTime)
        {
            if (!skyboxSettings.IsTransitioning) return false;
            UpdateTransition(deltaTime);
            return true;
        }

        private bool TryHandleSDKComponent()
        {
            if(wasSDKControlledLastFrame && !skyboxSettings.IsSDKControlled && skyboxSettings.CanUIControl)
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

        private bool TryHandleFeatureFlag()
        {
            if (!ffSkyboxSettings.HasValue) return false;

            skyboxSettings.CanUIControl = true;

            int speed = ffSkyboxSettings.Value.speed;
            skyboxSpeedMultiplier = Mathf.Abs(speed);

            skyboxSettings.IsDayCycleEnabled = speed != 0;
            skyboxSettings.TargetTransitionTimeOfDay = ffSkyboxSettings.Value.time;
            skyboxSettings.IsTransitioning = true;
            skyboxSettings.ShouldUpdateSkybox = true;

            return true;
        }

        private void UpdateTransition(float deltaTime)
        {
            float current = skyboxSettings.TimeOfDayNormalized;
            float target = skyboxSettings.TargetTransitionTimeOfDay;
            float speed = skyboxSettings.TransitionSpeed;

            if (Mathf.Approximately(current, target))
            {
                skyboxSettings.TimeOfDayNormalized = target;
                skyboxSettings.IsTransitioning = false;
                return;
            }

            float step = deltaTime * speed;

            if (skyboxSettings.TransitionMode == TransitionMode.FORWARD)
            {
                float distance = (target - current + 1f) % 1f;

                if (step >= distance) { skyboxSettings.TimeOfDayNormalized = target; }
                else { skyboxSettings.TimeOfDayNormalized = (current + step) % 1f; }
            }
            else // TransitionMode.BACKWARD
            {
                float distance = (current - target + 1f) % 1f;

                if (step >= distance) { skyboxSettings.TimeOfDayNormalized = target; }
                else { skyboxSettings.TimeOfDayNormalized = (current - step + 1f) % 1f; }
            }

            skyboxSettings.ShouldUpdateSkybox = true;
        }

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

                if (scene?.SceneData?.SceneEntityDefinition?.metadata == null)
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
            skyboxSettings.IsTransitioning = true;
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
    }
}
