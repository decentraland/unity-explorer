using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.SceneRestrictionBusController.SceneRestriction;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using ECS.SceneLifeCycle;
using JetBrains.Annotations;
using SceneRunner.Scene;
using UnityEngine;

namespace DCL.StylizedSkybox.Scripts
{
    public class SkyboxTimeManager
    {
        private struct FeatureFlagSkyboxSettings
        {
            public int time;
            public int speed;
        }

        private readonly StylizedSkyboxSettingsAsset skyboxSettings;
        private readonly ISceneRestrictionBusController sceneRestrictionBusController;
        private const float DEFAULT_SPEED = 1 * 60f; // 1 minute per second
        [CanBeNull] private ISceneFacade scene;
        private bool hasActiveSceneOverride = false;
        private bool hasActiveFeatureFlagOverride = false;
        private readonly bool hasSkyboxSettingFeatureFlag;
        private float speedMultiplier { get; set; } = DEFAULT_SPEED;

        public float GlobalTimeOfDay { get; private set; }

        public SkyboxTimeManager(StylizedSkyboxSettingsAsset skyboxSettings, IScenesCache scenesCache, ISceneRestrictionBusController sceneRestrictionBusController)
        {
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;

            skyboxSettings.IsDayCycleEnabled = true;
            skyboxSettings.TimeOfDayNormalized = StylizedSkyboxSettingsAsset.DEFAULT_TIME;
            GlobalTimeOfDay = skyboxSettings.TimeOfDayNormalized;

            scenesCache.OnCurrentSceneChanged += OnSceneChanged;
            skyboxSettings.SkyboxTimeSourceChanged += SkyboxSettingsOnSkyboxTimeSourceChanged;

            hasSkyboxSettingFeatureFlag = FeatureFlagsConfiguration.Instance.IsEnabled(FeatureFlagsStrings.SKYBOX_SETTINGS);
            UpdateTimeSourceHierarchy();
        }

        private void SkyboxSettingsOnSkyboxTimeSourceChanged(SkyboxTimeSource newSource)
        {
            switch (newSource)
            {
                case SkyboxTimeSource.GLOBAL:
                case SkyboxTimeSource.PLAYER_FIXED:
                case SkyboxTimeSource.FEATURE_FLAG:
                    RestrictTimeControl(SceneRestrictionsAction.REMOVED);
                    break;
                case SkyboxTimeSource.SCENE_FIXED:
                    RestrictTimeControl(SceneRestrictionsAction.APPLIED);
                    break;
            }
        }

        private void RestrictTimeControl(SceneRestrictionsAction action)
        {
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeLocked(action));
        }

        public void Update(float deltaTime)
        {
            UpdateGlobalTime(deltaTime);
            ApplyTimeProgression();
        }

        private void ApplyTimeProgression()
        {
            if (skyboxSettings.IsDayCycleEnabled)
                skyboxSettings.TimeOfDayNormalized = GlobalTimeOfDay;
        }

        private void UpdateTimeSourceHierarchy()
        {
            hasActiveSceneOverride = TryApplySceneTimeOverride();
            if (hasActiveSceneOverride) return;

            hasActiveFeatureFlagOverride = TryApplyFeatureFlagTimeOverride();
            if (hasActiveFeatureFlagOverride) return;

            ApplyGlobalTimeSource();
        }

        private void ApplyGlobalTimeSource()
        {
            hasActiveSceneOverride = false;
            hasActiveFeatureFlagOverride = false;

            skyboxSettings.SkyboxTimeSource = SkyboxTimeSource.GLOBAL;
            skyboxSettings.IsDayCycleEnabled = true;
        }

        private void UpdateGlobalTime(float deltaTime)
        {
            // We always track the time of day so we can switch back to using it
            GlobalTimeOfDay += deltaTime * speedMultiplier / StylizedSkyboxSettingsAsset.SECONDS_IN_DAY;

            // Loop around at EOD
            if (GlobalTimeOfDay >= 1f)
                GlobalTimeOfDay = 0f;
        }

        private bool TryApplySceneTimeOverride()
        {
            if (scene?.SceneData?.SceneEntityDefinition?.metadata == null) return false;

            SceneMetadata sceneMetadata = scene.SceneData.SceneEntityDefinition.metadata;

            if (sceneMetadata is { worldConfiguration: { SkyboxConfig: { fixedTime: var worldTime } } })
            {
                ApplySceneControlledFixedTime(worldTime);
                return true;
            }

            if (sceneMetadata is { skyboxConfig: { fixedTime: var sceneTime } })
            {
                ApplySceneControlledFixedTime(sceneTime);
                return true;
            }

            return false;
        }

        private void ApplySceneControlledFixedTime(float sceneTime)
        {
            skyboxSettings.IsDayCycleEnabled = false;
            skyboxSettings.TimeOfDayNormalized = sceneTime;
            skyboxSettings.SkyboxTimeSource = SkyboxTimeSource.SCENE_FIXED;
        }

        private bool TryApplyFeatureFlagTimeOverride()
        {
            if (!hasSkyboxSettingFeatureFlag)
                return false;

            if (!FeatureFlagsConfiguration.Instance.TryGetJsonPayload(FeatureFlagsStrings.SKYBOX_SETTINGS, FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT, out FeatureFlagSkyboxSettings ffSkyboxSettings))
                return false;

            skyboxSettings.IsDayCycleEnabled = ffSkyboxSettings.speed != 0;
            skyboxSettings.TimeOfDayNormalized = ffSkyboxSettings.time;
            speedMultiplier = Mathf.Abs(ffSkyboxSettings.speed);
            skyboxSettings.SkyboxTimeSource = SkyboxTimeSource.FEATURE_FLAG;

            return true;

        }

        private void OnSceneChanged(ISceneFacade sceneFacade)
        {
            scene = sceneFacade;
            UpdateTimeSourceHierarchy();
        }

#region DEBUG METHODS
        public void ForceSetDayCycleEnabled(bool cycleEnabled, SkyboxTimeSource newSource)
        {
            skyboxSettings.IsDayCycleEnabled = cycleEnabled;
            skyboxSettings.SkyboxTimeSource = newSource;
        }

        public void ForceSetTimeOfDay(float timeOfDay, SkyboxTimeSource newSource)
        {
            skyboxSettings.TimeOfDayNormalized = timeOfDay;
            ForceSetDayCycleEnabled(false, newSource);
        }
#endregion
    }
}
