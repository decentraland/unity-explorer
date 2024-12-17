using DCL.Diagnostics;
using DCL.FeatureFlags;
using System;
using UnityEngine;
using TMPro;
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

    public float SpeedMultiplier { get; set; } = DEFAULT_SPEED;
    public bool UseDynamicTime { get; set; } = true;
    public float DynamicTimeNormalized { get; private set; }
    public float CurrentTimeNormalized { get; private set; }

    public event Action OnSkyboxUpdated;

    [SerializeField] private Material skyboxMaterial;

    [Header("Refresh Time")]
    public float RefreshTime = 5;

    [Header("Directional Light")]
    public Light DirectionalLight;
    public AnimationClip LightAnimation;

    [GradientUsage(true)]
    public Gradient DirectionalColorRamp;
    [GradientUsage(true)]
    public Gradient SunColorRamp;

    public AnimationCurve sunRadiance;
    public AnimationCurve sunRadianceIntensity;
    public AnimationCurve moonMaskSize;

    [Header("Skybox Color")]
    [GradientUsage(true)] public Gradient SkyZenitColorRamp;
    [GradientUsage(true)] public Gradient SkyHorizonColorRamp;
    [GradientUsage(true)] public Gradient SkyNadirColorRamp;

    [InspectorName("Rim Light Color")]
    [GradientUsage(true)] public Gradient RimColorRamp;

    [Header("Indirect Lighting")]
    [InspectorName("Enabled")] public bool IndirectLight = true;
    [GradientUsage(true)] public Gradient IndirectSkyRamp;
    [GradientUsage(true)] public Gradient IndirectEquatorRamp;
    [GradientUsage(true)] public Gradient GroundEquatorRamp;

    [Header("Clouds")]
    [GradientUsage(true)] public Gradient CloudsColorRamp;
    public AnimationCurve CloudsHighlightsIntensity;

    [Header("Fog")]
    [InspectorName("Enabled")] public bool Fog = true;
    [GradientUsage(true)] public Gradient FogColorRamp;

    [Header("UI")]
    public TextMeshProUGUI textUI;

    private bool isInitialized;
    private Animation lightAnimator;
    private float sinceLastRefresh = 5;

    private void Update()
    {
        if (!isInitialized)
            return;

        // We always track dynamic time so we can switch back to using it
        DynamicTimeNormalized += (Time.deltaTime * SpeedMultiplier) / SECONDS_IN_DAY;

        // Loop around at EOD
        if (DynamicTimeNormalized >= 1f) { DynamicTimeNormalized = 0f; }

        // Auto refresh the skybox when using dynamic time
        if (UseDynamicTime)
        {
            sinceLastRefresh += Time.deltaTime;

            if (sinceLastRefresh >= RefreshTime)
            {
                CurrentTimeNormalized = DynamicTimeNormalized;
                UpdateSkybox();
                sinceLastRefresh = 0;
            }
        }

        // Always update UI
        UpdateTimeUI();
    }

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, FeatureFlagsCache featureFlagsCache)
    {
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
            DirectionalLight = dirLight;

        if (skyboxAnimationClip != null)
            LightAnimation = skyboxAnimationClip;

        //setup skybox material
        if (!skyboxMaterial) { ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: No skybox material assigned"); }
        else
        {
            //assign skybox to render settings
            RenderSettings.skybox = skyboxMaterial;
        }

        //setup directional light
        if (DirectionalLight == null) { ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light has not been assigned"); }
        else
        {
            //assign light to render settings
            RenderSettings.sun = DirectionalLight;

            //create animation component in runtime and assign animation clip
            if (DirectionalLight.gameObject.GetComponent<Animation>() == null) { lightAnimator = DirectionalLight.gameObject.AddComponent<Animation>(); }
            else { lightAnimator = DirectionalLight.gameObject.GetComponent<Animation>(); }

            if (LightAnimation == null) { ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light animation has not been assigned"); }
            else { lightAnimator.AddClip(LightAnimation, LightAnimation.name); }
        }

        //setup indirect light
        if (IndirectLight) { RenderSettings.ambientMode = AmbientMode.Trilight; }

        //setup fog
        if (Fog) { RenderSettings.fog = true; }

        bool useRemoteSkyboxSettings = featureFlagsCache != null && featureFlagsCache.Configuration.IsEnabled(FeatureFlagsStrings.SKYBOX_SETTINGS);

        if (useRemoteSkyboxSettings &&
            featureFlagsCache.Configuration.TryGetJsonPayload(FeatureFlagsStrings.SKYBOX_SETTINGS, FeatureFlagsStrings.SKYBOX_SETTINGS_VARIANT, out SkyboxSettings skyboxSettings))
        {
            SpeedMultiplier = skyboxSettings.speed;
            DynamicTimeNormalized = (float)skyboxSettings.time / SECONDS_IN_DAY;
        }
        else
        {
            DynamicTimeNormalized = DEFAULT_TIME;
        }

        isInitialized = true;
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
    ///     Auxiliary function to returnt the normalized time in HH:MM:SS
    /// </summary>
    public string GetFormatedTime()
    {
        var totalSec = (int)(CurrentTimeNormalized * SECONDS_IN_DAY);

        int hours = totalSec / 3600;
        int minutes = totalSec % 3600 / 60;
        int seconds = totalSec % 60;
        return $"{hours:00}:{minutes:00}:{seconds:00} - {CurrentTimeNormalized:0.000}";
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

        OnSkyboxUpdated?.Invoke();
    }

    /// <summary>
    ///     Updates the exposed parameters of the material to update gradient colors
    ///     and sun size
    /// </summary>
    private void UpdateSkyboxColor()
    {
        RenderSettings.skybox.SetColor(ZENIT_COLOR, SkyZenitColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(HORIZON_COLOR, SkyHorizonColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(NADIR_COLOR, SkyNadirColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(SUN_COLOR, SunColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(RIM_COLOR, RimColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetColor(CLOUDS_COLOR, CloudsColorRamp.Evaluate(CurrentTimeNormalized));
        RenderSettings.skybox.SetFloat(CLOUD_HIGHLIGHTS, CloudsHighlightsIntensity.Evaluate(CurrentTimeNormalized));
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors
    ///     from the defined gradients based on the normalized time
    /// </summary>
    private void UpdateIndirectLight()
    {
        if (IndirectLight)
        {
            RenderSettings.ambientSkyColor = IndirectSkyRamp.Evaluate(CurrentTimeNormalized);
            RenderSettings.ambientEquatorColor = IndirectEquatorRamp.Evaluate(CurrentTimeNormalized);
            RenderSettings.ambientGroundColor = GroundEquatorRamp.Evaluate(CurrentTimeNormalized);
        }
    }

    /// <summary>
    ///     Updates the directional light color by sampling the colors
    ///     from the defined gradient an plays the correspoding animation frame
    /// </summary>
    private void UpdateDirectionalLight()
    {
        if (!DirectionalLight) return;

        //change the color of the light based on the color ramp
        DirectionalLight.color = DirectionalColorRamp.Evaluate(CurrentTimeNormalized);

        //sample the right frame of the animation
        if (LightAnimation)
        {
            lightAnimator[LightAnimation.name].time = CurrentTimeNormalized * lightAnimator[LightAnimation.name].length;
            lightAnimator.Play(LightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        RenderSettings.skybox.SetFloat(SUN_SIZE, DirectionalLight.gameObject.transform.localScale.x);
        RenderSettings.skybox.SetFloat(SUN_OPACITY, DirectionalLight.gameObject.transform.localScale.y);

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
        if (Fog) { RenderSettings.fogColor = FogColorRamp.Evaluate(CurrentTimeNormalized); }
    }

    /// <summary>
    ///     Auxiliary function to render the time in the UI
    /// </summary>
    private void UpdateTimeUI()
    {
        if (textUI) { textUI.text = GetFormatedTime(); }
    }

#if UNITY_EDITOR

    public bool editMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if (editMode)
            Initialize(null, null, null, null);
    }
#endif

    private struct SkyboxSettings
    {
        public int time;
        public int speed;
    }
}
