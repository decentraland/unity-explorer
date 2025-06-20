using DCL.DebugUtilities;
using DCL.DebugUtilities.UIBindings;
using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.Ipfs;
using DCL.StylizedSkybox.Scripts;
using ECS.SceneLifeCycle;
using SceneRunner.Scene;
using UnityEngine;
using UnityEngine.Rendering;

public class SkyboxController : MonoBehaviour
{
    private const float MIN_FIXED_TIME_OF_DAY = 0;
    private const int SECONDS_IN_DAY = 86400;
    private const float DEFAULT_TIME = 0.5f; // Midday
    private const float DEFAULT_SPEED = 1 * 60f; // 1 minute per second

    private static readonly int ZENIT_COLOR = Shader.PropertyToID("_ZenitColor");
    private static readonly int HORIZON_COLOR = Shader.PropertyToID("_HorizonColor");
    private static readonly int NADIR_COLOR = Shader.PropertyToID("_NadirColor");
    private static readonly int SUN_COLOR = Shader.PropertyToID("_SunColor");
    private static readonly int RIM_COLOR = Shader.PropertyToID("_RimColor");
    private static readonly int CLOUDS_COLOR = Shader.PropertyToID("_CloudsColor");
    private static readonly int CLOUD_HIGHLIGHTS = Shader.PropertyToID("_Cloud_Highlights");
    private static readonly int SUN_SIZE = Shader.PropertyToID("_SunSize");
    private static readonly int SUN_OPACITY = Shader.PropertyToID("_SunOpacity");
    private static readonly int SUN_RADIANCE = Shader.PropertyToID("_Sun_Radiance");
    private static readonly int SUN_RADIANCE_INTENSITY = Shader.PropertyToID("_Sun_Radiance_Intensity");
    private static readonly int MOON_MASK_SIZE = Shader.PropertyToID("_Moon_Mask_Size");

    [SerializeField] private Material skyboxMaterial;

    [Header("Refresh Time")]
    [SerializeField] private float skyboxRefreshTime = 5;

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private AnimationClip lightAnimation;

    [GradientUsage(true)]
    [SerializeField] private Gradient directionalColorRamp;

    [GradientUsage(true)]
    [SerializeField] private Gradient sunColorRamp;

    [SerializeField] private AnimationCurve sunRadiance;
    [SerializeField] private AnimationCurve sunRadianceIntensity;
    [SerializeField] private AnimationCurve moonMaskSize;

    [Header("Skybox Color")]
    [GradientUsage(true)] [SerializeField] private Gradient skyZenitColorRamp;
    [GradientUsage(true)] [SerializeField] private Gradient skyHorizonColorRamp;
    [GradientUsage(true)] [SerializeField] private Gradient skyNadirColorRamp;

    [InspectorName("Rim Light Color")]
    [GradientUsage(true)] [SerializeField] private Gradient rimColorRamp;

    [Header("Indirect Lighting")]
    [InspectorName("Enabled")] [SerializeField] private bool indirectLight = true;
    [GradientUsage(true)] [SerializeField] private Gradient indirectSkyRamp;
    [GradientUsage(true)] [SerializeField] private Gradient indirectEquatorRamp;
    [GradientUsage(true)] [SerializeField] private Gradient groundEquatorRamp;

    [Header("Clouds")]
    [GradientUsage(true)] [SerializeField] private Gradient cloudsColorRamp;
    [SerializeField] private AnimationCurve cloudsHighlightsIntensity;

    [Header("Fog")]
    [InspectorName("Enabled")] [SerializeField] private bool fog = true;
    [GradientUsage(true)] [SerializeField] private Gradient fogColorRamp;

    public float SpeedMultiplier { get; set; } = DEFAULT_SPEED;
    public float GlobalTimeOfDay { get; private set; }
    public float CurrentTimeOfDay { get; private set; }

    private StylizedSkyboxSettingsAsset settingsAsset;
    private bool isInitialized;
    private Animation lightAnimator;
    private float sinceLastSkyboxRefresh = 5;
    private FeatureFlagsCache featureFlagsCache;
    private IDebugContainerBuilder debugContainerBuilder;
    private bool useFeatureFlagSkyboxSettings;

    private ElementBinding<float> debugTimeOfDay;
    private ElementBinding<string> debugTimeSource;

    private void Update()
    {
        if (!isInitialized)
            return;

        float deltaTime = Time.deltaTime;

        // We always track the time of day so we can switch back to using it
        GlobalTimeOfDay += deltaTime * SpeedMultiplier / SECONDS_IN_DAY;

        // Loop around at EOD
        if (GlobalTimeOfDay >= 1f)
            GlobalTimeOfDay = 0f;

        // Auto refresh the skybox when using Day Night Cycle
        if (settingsAsset.IsDayNightCycleEnabled)
        {
            sinceLastSkyboxRefresh += deltaTime;

            if (sinceLastSkyboxRefresh >= skyboxRefreshTime)
            {
                sinceLastSkyboxRefresh = 0;
                CurrentTimeOfDay = GlobalTimeOfDay;
                UpdateSkybox();
            }
        }
    }

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, FeatureFlagsCache featureFlagsCache, StylizedSkyboxSettingsAsset settingsAsset,
        IScenesCache scenesCache, IDebugContainerBuilder debugContainerBuilder)
    {
        this.featureFlagsCache = featureFlagsCache;
        this.settingsAsset = settingsAsset;
        this.debugContainerBuilder = debugContainerBuilder;

        if (skyboxMat)
        {
#if UNITY_EDITOR

            // Create a copy so that the original asset does not get modified by UpdateSkyboxColor. Else
            // we will get annoying mystery changes in Git.
            skyboxMat = new Material(skyboxMat);
#endif
            skyboxMaterial = skyboxMat;
        }

        if (dirLight)
            directionalLight = dirLight;

        if (skyboxAnimationClip)
            lightAnimation = skyboxAnimationClip;

        //setup skybox material
        if (!skyboxMaterial)
            ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: No skybox material assigned");
        else
            RenderSettings.skybox = skyboxMaterial; //assign skybox to render settings

        //setup directional light
        if (!directionalLight)
            ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light has not been assigned");
        else
        {
            //assign light to render settings
            RenderSettings.sun = directionalLight;

            //create animation component in runtime and assign animation clip
            lightAnimator = directionalLight.gameObject.GetComponent<Animation>();

            if (!lightAnimator)
                lightAnimator = directionalLight.gameObject.AddComponent<Animation>();

            if (!lightAnimation)
                ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light animation has not been assigned");
            else
                lightAnimator.AddClip(lightAnimation, lightAnimation.name);
        }

        //setup indirect light
        if (indirectLight)
            RenderSettings.ambientMode = AmbientMode.Trilight;

        //setup fog
        if (fog)
            RenderSettings.fog = true;

        scenesCache.OnCurrentSceneChanged += HandleSceneChanged;

        GlobalTimeOfDay = GetInitialTimeOfDay();

        settingsAsset.TimeOfDayNormalized = GlobalTimeOfDay;
        ResetToGlobalTime();

        settingsAsset.TimeOfDayChanged += OnTimeOfDayChanged;
        settingsAsset.DayNightCycleEnabledChanged += OnDayNightCycleEnabledChanged;
        settingsAsset.SkyboxTimeSourceChanged += OnSkyboxTimeSourceChanged;

        SetupDebugPanel();

        isInitialized = true;
    }

    private void SetupDebugPanel()
    {
        debugTimeOfDay = new ElementBinding<float>(0);
        debugTimeSource = new ElementBinding<string>(string.Empty);

        debugContainerBuilder.TryAddWidget("Skybox")
                            ?.AddSingleButton("Play", ()=> SetDayNightCycleEnabled(true, SkyboxTimeSource.GLOBAL))
                             .AddSingleButton("Pause", () =>
                              {
                                  SetDayNightCycleEnabled(false, SkyboxTimeSource.PLAYER_FIXED);
                              })
                             .AddFloatSliderField("Time", debugTimeOfDay, 0, 1)
                             .AddSingleButton("SetTime", () =>
                              {
                                  SetTimeOfDay(debugTimeOfDay.Value, SkyboxTimeSource.PLAYER_FIXED);
                              }) //TODO: replace this by a system to update the value
                             .AddCustomMarker("TimeSource", debugTimeSource);
    }

    private void HandleSceneChanged(ISceneFacade scene)
    {
        if (scene == null || !settingsAsset)
        {
            ResetToGlobalTime();
            return;
        }

        SceneMetadata sceneMetadata = scene.SceneData
                                           .SceneEntityDefinition
                                           .metadata;

        if (sceneMetadata is { worldConfiguration: { SkyboxConfig: { fixedTimeOfDay: var worldTime } } })
        {
            ApplySceneControlledFixedTime(worldTime);
            return;
        }

        if (sceneMetadata is { skyboxConfig: { fixedTimeOfDay: var sceneTime } })
        {
            ApplySceneControlledFixedTime(sceneTime);
            return;
        }

        ResetToGlobalTime();
    }

    private void ApplySceneControlledFixedTime(float sceneTime)
    {
        if (!settingsAsset) return;

        settingsAsset.IsDayNightCycleEnabled = false;
        settingsAsset.SkyboxTimeSource = SkyboxTimeSource.SCENE_FIXED;
        settingsAsset.TimeOfDayNormalized = Mathf.Clamp(sceneTime, MIN_FIXED_TIME_OF_DAY, SECONDS_IN_DAY) / (float)SECONDS_IN_DAY;

        debugTimeOfDay.Value = sceneTime;
    }

    private void OnSkyboxTimeSourceChanged(SkyboxTimeSource newSkyboxTimeSource)
    {
        debugTimeSource.Value = newSkyboxTimeSource.ToString();
    }

    private void ResetToGlobalTime()
    {
        //triggers immediate skybox graphics refresh
        sinceLastSkyboxRefresh = skyboxRefreshTime;

        settingsAsset.SkyboxTimeSource = useFeatureFlagSkyboxSettings ? SkyboxTimeSource.FEATURE_FLAG : SkyboxTimeSource.GLOBAL;
        settingsAsset.IsDayNightCycleEnabled = true;
    }

    public void SetDayNightCycleEnabled(bool cycleEnabled, SkyboxTimeSource newSource)
    {
        if (!settingsAsset) return;

        settingsAsset.SkyboxTimeSource = newSource;
        settingsAsset.IsDayNightCycleEnabled = cycleEnabled;
    }

    private float GetInitialTimeOfDay()
    {
        // Fetch feature flag settings to see if there are settings to override from there
        useFeatureFlagSkyboxSettings = featureFlagsCache != null && featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.SKYBOX_SETTINGS);

        if (useFeatureFlagSkyboxSettings && featureFlagsCache!.Configuration.TryGetJsonPayload(FeatureFlagsStrings.SKYBOX_SETTINGS, FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT, out SkyboxSettings skyboxSettings))
        {
            settingsAsset.SkyboxTimeSource = SkyboxTimeSource.FEATURE_FLAG;
            SpeedMultiplier = skyboxSettings.speed;
            return (float)skyboxSettings.time / SECONDS_IN_DAY;
        }

        return DEFAULT_TIME;
    }

    private void OnSkyboxUpdated()
    {
        // When skybox gets dynamically updated, we refresh the
        // settings value so it reflects the current state

        if (settingsAsset.IsDayNightCycleEnabled)
            settingsAsset!.TimeOfDayNormalized = GlobalTimeOfDay;
    }

    private void OnDayNightCycleEnabledChanged(bool isProgressing)
    {
        settingsAsset.IsDayNightCycleEnabled = isProgressing;

        if (isProgressing)
            settingsAsset!.TimeOfDayNormalized = GlobalTimeOfDay;
    }

    private void OnTimeOfDayChanged(float timeOfDay)
    {
        if (!settingsAsset.IsDayNightCycleEnabled) // Ignore updates to the value when they come from the skybox
            SetTimeOfDay(timeOfDay, SkyboxTimeSource.GLOBAL);
    }

    /// <summary>
    ///     Sets the time of the skybox to a specific amount (normalized).
    /// </summary>
    public void SetTimeOfDay(float timeOfDay, SkyboxTimeSource source)
    {
        CurrentTimeOfDay = timeOfDay;
        SetDayNightCycleEnabled(false, source);
        UpdateSkybox();
    }

    /// <summary>
    ///     Calls all the necessary methods to update the skybox and environment
    /// </summary>
    private void UpdateSkybox()
    {
        UpdateIndirectLight();
        UpdateDirectionalLight();
        UpdateSkyboxColor();
        UpdateFog();

        OnSkyboxUpdated();
    }

    /// <summary>
    ///     Updates the exposed parameters of the material to update gradient colors
    ///     and sun size
    /// </summary>
    private void UpdateSkyboxColor()
    {
        RenderSettings.skybox.SetColor(ZENIT_COLOR, skyZenitColorRamp.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetColor(HORIZON_COLOR, skyHorizonColorRamp.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetColor(NADIR_COLOR, skyNadirColorRamp.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetColor(SUN_COLOR, sunColorRamp.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetColor(RIM_COLOR, rimColorRamp.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetColor(CLOUDS_COLOR, cloudsColorRamp.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetFloat(CLOUD_HIGHLIGHTS, cloudsHighlightsIntensity.Evaluate(CurrentTimeOfDay));
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors
    ///     from the defined gradients based on the normalized time
    /// </summary>
    private void UpdateIndirectLight()
    {
        if (!indirectLight) return;

        RenderSettings.ambientSkyColor = indirectSkyRamp.Evaluate(CurrentTimeOfDay);
        RenderSettings.ambientEquatorColor = indirectEquatorRamp.Evaluate(CurrentTimeOfDay);
        RenderSettings.ambientGroundColor = groundEquatorRamp.Evaluate(CurrentTimeOfDay);
    }

    /// <summary>
    ///     Updates the directional light color by sampling the colors
    ///     from the defined gradient and plays the corresponding animation frame
    /// </summary>
    private void UpdateDirectionalLight()
    {
        if (!directionalLight) return;

        //change the color of the light based on the color ramp
        directionalLight.color = directionalColorRamp.Evaluate(CurrentTimeOfDay);

        //sample the right frame of the animation
        if (lightAnimation)
        {
            lightAnimator[lightAnimation.name].time = CurrentTimeOfDay * lightAnimator[lightAnimation.name].length;
            lightAnimator.Play(lightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        var directionalLightLocalScale = directionalLight.gameObject.transform.localScale;
        RenderSettings.skybox.SetFloat(SUN_SIZE, directionalLightLocalScale.x);
        RenderSettings.skybox.SetFloat(SUN_OPACITY, directionalLightLocalScale.y);

        //sampling sun radiance and intensity curves
        RenderSettings.skybox.SetFloat(SUN_RADIANCE, sunRadiance.Evaluate(CurrentTimeOfDay));
        RenderSettings.skybox.SetFloat(SUN_RADIANCE_INTENSITY, sunRadianceIntensity.Evaluate(CurrentTimeOfDay));

        //change size of moon mask
        RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, moonMaskSize.Evaluate(CurrentTimeOfDay));
    }

    /// <summary>
    ///     Updates the fog color of the RenderSettings if enabled
    /// </summary>
    private void UpdateFog()
    {
        if (fog)
            RenderSettings.fogColor = fogColorRamp.Evaluate(CurrentTimeOfDay);
    }

#if UNITY_EDITOR
    public bool editMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if (editMode)
            Initialize(null, null, null, null, null, null, null);
    }
#endif

    private struct SkyboxSettings
    {
        public int time;
        public int speed;
    }
}
