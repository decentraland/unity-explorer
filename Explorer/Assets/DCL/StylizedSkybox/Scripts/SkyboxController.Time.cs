using DCL.FeatureFlags;
using DCL.SceneRestrictionBusController.SceneRestrictionBus;
using DCL.StylizedSkybox.Scripts;
using ECS.SceneLifeCycle;

public partial class SkyboxController
{
    private SkyboxTimeManager skyboxTimeManager;
    public float CurrentTimeOfDay { get; private set; }

    private void InitializeSkyboxTimeHandling(IScenesCache scenesCache, ISceneRestrictionBusController sceneRestrictionBusController, FeatureFlagsCache featureFlagsCache)
    {
        skyboxTimeManager = new SkyboxTimeManager(skyboxSettings, scenesCache, sceneRestrictionBusController, featureFlagsCache);
        skyboxSettings.TimeOfDayChanged += OnTimeOfDayChanged;
    }

    private void OnTimeOfDayChanged(float time)
    {
        CurrentTimeOfDay = time;
        UpdateSkybox();
    }

#region DEBUG METHODS
    public void ForceSetDayCycleEnabled(bool cycleEnabled, SkyboxTimeSource newSource)
    {
        if (skyboxSettings.IsDayCycleEnabled == cycleEnabled) return;

        skyboxTimeManager.ForceSetDayCycleEnabled(cycleEnabled, newSource);

        if (cycleEnabled)
            ForceSkyboxGraphicsRefresh();
    }

    public void ForceSetTimeOfDay(float timeOfDay, SkyboxTimeSource newSource)
    {
        skyboxTimeManager.ForceSetTimeOfDay(timeOfDay, newSource);
        CurrentTimeOfDay = skyboxSettings.TimeOfDayNormalized;
        UpdateSkybox();
    }

    private void ForceSkyboxGraphicsRefresh()
    {
        sinceLastSkyboxRefresh = skyboxRefreshTime; // Force refresh
    }
#endregion
}
