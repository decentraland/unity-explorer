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
        private readonly FeatureFlagsCache featureFlagsCache;
        private const float DEFAULT_SPEED = 1 * 60f; // 1 minute per second
        [CanBeNull] private ISceneFacade scene;
        private bool hasActiveSceneOverride = false;
        private bool hasActiveFeatureFlagOverride = false;
        private bool needsHierarchyUpdate = true; // Force check on the first run
        private float speedMultiplier { get; set; } = DEFAULT_SPEED;

        public float GlobalTimeOfDay { get; private set; }

        public SkyboxTimeManager(StylizedSkyboxSettingsAsset skyboxSettings, IScenesCache scenesCache, ISceneRestrictionBusController sceneRestrictionBusController, FeatureFlagsCache featureFlagsCache)
        {
            this.skyboxSettings = skyboxSettings;
            this.sceneRestrictionBusController = sceneRestrictionBusController;
            this.featureFlagsCache = featureFlagsCache;
            skyboxSettings.IsDayCycleEnabled = true;
            skyboxSettings.TimeOfDayNormalized = StylizedSkyboxSettingsAsset.DEFAULT_TIME;
            GlobalTimeOfDay = skyboxSettings.TimeOfDayNormalized;

            scenesCache.OnCurrentSceneChanged += OnSceneChanged;
            skyboxSettings.SkyboxTimeSourceChanged += SkyboxSettingsOnSkyboxTimeSourceChanged;
        }

        private void SkyboxSettingsOnSkyboxTimeSourceChanged(SkyboxTimeSource newSource)
        {
            switch (newSource)
            {
                case SkyboxTimeSource.GLOBAL:
                case SkyboxTimeSource.PLAYER_FIXED:
                case SkyboxTimeSource.FEATURE_FLAG:
                    RestrictTimeControl(false);
                    break;
                case SkyboxTimeSource.SCENE_FIXED:
                    RestrictTimeControl(true);
                    break;
            }
        }

        private void RestrictTimeControl(bool restrict)
        {
            var restrictionsAction = restrict ? SceneRestrictionsAction.APPLIED : SceneRestrictionsAction.REMOVED;
            sceneRestrictionBusController.PushSceneRestriction(SceneRestriction.CreateSkyboxTimeLocked(restrictionsAction));
        }

        public void Update(float deltaTime)
        {
            UpdateGlobalTime(deltaTime);

            if (needsHierarchyUpdate)
            {
                UpdateTimeSourceHierarchy();
                needsHierarchyUpdate = false;
            }

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
            if(scene == null) return false;

            SceneMetadata sceneMetadata = scene.SceneData
                                               .SceneEntityDefinition
                                               .metadata;

            if (sceneMetadata is { worldConfiguration: { SkyboxConfig: { fixedTimeOfDay: var worldTime } } })
            {
                ApplySceneControlledFixedTime(worldTime);
                return true;
            }

            if (sceneMetadata is { skyboxConfig: { fixedTimeOfDay: var sceneTime } })
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
            bool hasOverride = featureFlagsCache != null && featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.SKYBOX_SETTINGS);

            if (hasOverride &&
                featureFlagsCache.Configuration.TryGetJsonPayload(FeatureFlagsStrings.SKYBOX_SETTINGS, FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT, out FeatureFlagSkyboxSettings ffSkyboxSettings))
            {
                skyboxSettings.IsDayCycleEnabled = ffSkyboxSettings.speed != 0;
                skyboxSettings.TimeOfDayNormalized = ffSkyboxSettings.time;
                speedMultiplier = Mathf.Abs(ffSkyboxSettings.speed);
                skyboxSettings.SkyboxTimeSource = SkyboxTimeSource.FEATURE_FLAG;
            }

            return hasOverride;
        }

        private void OnSceneChanged(ISceneFacade sceneFacade)
        {
            scene = sceneFacade;
            needsHierarchyUpdate = true;
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
