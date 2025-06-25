using DCL.Diagnostics;
using DCL.FeatureFlags;
using DCL.StylizedSkybox.Scripts;
using System;
using UnityEngine;
using UnityEngine.Rendering;

public class SkyboxController : MonoBehaviour
{
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
    [SerializeField] private float refreshTime = 5;

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
    public bool UseDynamicTime { get; set; } = true;
    public float DynamicTimeNormalized { get; private set; }
    public float CurrentTimeNormalized { get; private set; }

    private StylizedSkyboxSettingsAsset settingsAsset;
    private bool isInitialized;
    private Animation lightAnimator;
    private float sinceLastRefresh = 5;

    private void Update()
    {
        if (!isInitialized)
            return;

        // We always track dynamic time so we can switch back to using it
        DynamicTimeNormalized += Time.deltaTime * SpeedMultiplier / SECONDS_IN_DAY;

        // Loop around at EOD
        if (DynamicTimeNormalized >= 1f) { DynamicTimeNormalized = 0f; }

        // Auto refresh the skybox when using dynamic time
        if (UseDynamicTime)
        {
            sinceLastRefresh += Time.deltaTime;

            if (sinceLastRefresh >= refreshTime)
            {
                CurrentTimeNormalized = DynamicTimeNormalized;
                UpdateSkybox();
                sinceLastRefresh = 0;
            }
        }
    }

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, StylizedSkyboxSettingsAsset settingsAsset, FeatureFlagsConfiguration featureFlags)
    {
        this.settingsAsset = settingsAsset;

        if (skyboxMat != null)
        {
#if UNITY_EDITOR

            // Create a copy so that the original asset does not get modified by UpdateSkyboxColor. Else
            // we will get annoying mystery changes in Git.
            skyboxMat = new Material(skyboxMat);
#endif
            skyboxMaterial = skyboxMat;
        }

        if (dirLight != null)
            directionalLight = dirLight;

        if (skyboxAnimationClip != null)
            lightAnimation = skyboxAnimationClip;

        //setup skybox material
        if (!skyboxMaterial) { ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: No skybox material assigned"); }
        else
        {
            //assign skybox to render settings
            RenderSettings.skybox = skyboxMaterial;
        }

        //setup directional light
        if (directionalLight == null) { ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light has not been assigned"); }
        else
        {
            //assign light to render settings
            RenderSettings.sun = directionalLight;

            //create animation component in runtime and assign animation clip
            if (directionalLight.gameObject.GetComponent<Animation>() == null) { lightAnimator = directionalLight.gameObject.AddComponent<Animation>(); }
            else { lightAnimator = directionalLight.gameObject.GetComponent<Animation>(); }

            if (lightAnimation == null) { ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light animation has not been assigned"); }
            else { lightAnimator.AddClip(lightAnimation, lightAnimation.name); }
        }

        //setup indirect light
        if (indirectLight) { RenderSettings.ambientMode = AmbientMode.Trilight; }

        //setup fog
        if (fog) { RenderSettings.fog = true; }

        bool useRemoteSkyboxSettings = featureFlags.IsEnabled(FeatureFlagsStrings.SKYBOX_SETTINGS);

        if (useRemoteSkyboxSettings &&
            featureFlags.TryGetJsonPayload(FeatureFlagsStrings.SKYBOX_SETTINGS, FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT, out SkyboxSettings skyboxSettings))
        {
            SpeedMultiplier = skyboxSettings.speed;
            DynamicTimeNormalized = (float)skyboxSettings.time / SECONDS_IN_DAY;
        }
        else { DynamicTimeNormalized = DEFAULT_TIME; }

        settingsAsset.NormalizedTime =DynamicTimeNormalized;
        settingsAsset.NormalizedTimeChanged += OnNormalizedTimeChanged;
        settingsAsset.UseDynamicTime = UseDynamicTime;
        settingsAsset.UseDynamicTimeChanged += OnUseDynamicTimeChanged;

        isInitialized = true;
    }

    private void OnSkyboxUpdated()
    {
        // When skybox gets dynamically updated we refresh the
        // settings value so it reflects the current state

        if (UseDynamicTime)
        {
            settingsAsset!.NormalizedTime = DynamicTimeNormalized;
        }
    }

    private void OnUseDynamicTimeChanged(bool dynamic)
    {
        UseDynamicTime = dynamic;

        if (dynamic)
            settingsAsset!.NormalizedTime = DynamicTimeNormalized;
    }

    private void OnNormalizedTimeChanged(float tod)
    {
        if (!UseDynamicTime) // Ignore updates to the value when they come from the skybox
        {
            SetTimeOverride(tod);
        }
    }

    /// <summary>
    ///     Sets the time of the skybox to an specific amount (normalized).
    /// </summary>
    public void SetTimeOverride(float time)
    {
        UseDynamicTime = false;
        CurrentTimeNormalized = time;
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
        RenderSettings.skybox.SetColor(ZENIT_COLOR, skyZenitColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(HORIZON_COLOR, skyHorizonColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(NADIR_COLOR, skyNadirColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(SUN_COLOR, sunColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(RIM_COLOR, rimColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(CLOUDS_COLOR, cloudsColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetFloat(CLOUD_HIGHLIGHTS, cloudsHighlightsIntensity.Evaluate(CurrentTimeNormalized));
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors
    ///     from the defined gradients based on the normalized time
    /// </summary>
    private void UpdateIndirectLight()
    {
        if (indirectLight)
        {
            RenderSettings.ambientSkyColor = indirectSkyRamp.Evaluate(CurrentTimeNormalized);
            RenderSettings.ambientEquatorColor = indirectEquatorRamp.Evaluate(CurrentTimeNormalized);
            RenderSettings.ambientGroundColor = groundEquatorRamp.Evaluate(CurrentTimeNormalized);
        }
    }

    /// <summary>
    ///     Updates the directional light color by sampling the colors
    ///     from the defined gradient an plays the correspoding animation frame
    /// </summary>
    private void UpdateDirectionalLight()
    {
        if (!directionalLight) return;

        //change the color of the light based on the color ramp
        directionalLight.color = directionalColorRamp.Evaluate(CurrentTimeNormalized);

        //sample the right frame of the animation
        if (lightAnimation)
        {
            lightAnimator[lightAnimation.name].time = CurrentTimeNormalized * lightAnimator[lightAnimation.name].length;
            lightAnimator.Play(lightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        RenderSettings.skybox.SetFloat(SUN_SIZE, directionalLight.gameObject.transform.localScale.x);
        RenderSettings.skybox.SetFloat(SUN_OPACITY, directionalLight.gameObject.transform.localScale.y);

        //sampling sun randiance and intensity curves
        RenderSettings.skybox.SetFloat(SUN_RADIANCE, sunRadiance.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetFloat(SUN_RADIANCE_INTENSITY, sunRadianceIntensity.Evaluate(CurrentTimeNormalized));

        //change size of moon mask
        RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, moonMaskSize.Evaluate(CurrentTimeNormalized));
    }

    /// <summary>
    ///     Updates the fog color of the RenderSettings if enabled
    /// </summary>
    private void UpdateFog()
    {
        if (fog) { RenderSettings.fogColor = fogColorRamp.Evaluate(CurrentTimeNormalized); }
    }

#if UNITY_EDITOR

    public bool editMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if (editMode)
            Initialize(null, null, null, null, new FeatureFlagsConfiguration(FeatureFlagsResultDto.Empty));
    }
#endif

    private struct SkyboxSettings
    {
        public int time;
        public int speed;
    }
}
