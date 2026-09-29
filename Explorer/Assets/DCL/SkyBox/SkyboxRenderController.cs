using DCL.Diagnostics;
using DCL.SkyBox;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public class SkyboxRenderController : MonoBehaviour
{
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
    private static readonly int CLOUDS_ROTATION_SPEED = Shader.PropertyToID("_CloudsRotationSpeed");
    private static readonly int SECOND_SUN_ROTATION_SPEED = Shader.PropertyToID("_Second_Sun_Rotation_Speed");
    private static readonly int TIME_PARAMETERS = Shader.PropertyToID("_TimeParameters");

    // Static look values, written once per preset.
    private static readonly int ZENIT_SPREAD = Shader.PropertyToID("_Zenit_Spread");
    private static readonly int ZENIT_BLEND = Shader.PropertyToID("_Zenit_Blend");
    private static readonly int GROUND_SPREAD = Shader.PropertyToID("_Ground_Spread");
    private static readonly int GROUND_BLEND = Shader.PropertyToID("_Ground_Blend");
    private static readonly int BLEND_TWIST = Shader.PropertyToID("_Blend_Twist");
    private static readonly int RIM_SPREAD = Shader.PropertyToID("_Rim_Spread");
    private static readonly int RIM_OPACITY = Shader.PropertyToID("_Rim_Opacity");
    private static readonly int STARS_BRIGHTNESS = Shader.PropertyToID("_Stars_Brightness");
    private static readonly int STARS_TEXTURE = Shader.PropertyToID("_Stars_Texture");
    private static readonly int CLOUDS_CUBEMAP = Shader.PropertyToID("_Clouds_Cubemap");
    private static readonly int CLOUD_OPACITY = Shader.PropertyToID("_Cloud_Opacity");
    private static readonly int MOON_MASK_POSITION = Shader.PropertyToID("_Moon_Mask_Position");
    private static readonly int SECOND_SUN_SIZE_FACTOR = Shader.PropertyToID("_Second_Sun_Size_Factor");
    private static readonly int SECOND_SUN_ORBIT_SIZE = Shader.PropertyToID("_Second_Sun_Orbit_Size");

    // Selects the stylized shader variant; written by name, both globally and on the skybox material, once per preset.
    private const string STYLIZED_KEYWORD = "_DCL_SKY_STYLIZED";

    // Sky lookup (phase x elevation). The float switch is copied to the reflection bake material along with the texture.
    private static readonly int USE_SKY_LUT = Shader.PropertyToID("_UseSkyLut");
    private static readonly int SKY_LUT = Shader.PropertyToID("_SkyLut");
    private static readonly int SKY_PHASE = Shader.PropertyToID("_SkyPhase");

    // Clouds v2: global shader values read by CloudsV2.hlsl in both the sky and the reflection bake, so no material
    // properties are needed. Textures and layer setup are written once per preset, colours and flow per frame.
    private static readonly int[] CLOUD_STRIPS = { Shader.PropertyToID("_DclCloudStrip0"), Shader.PropertyToID("_DclCloudStrip1"), Shader.PropertyToID("_DclCloudStrip2") };
    private static readonly int[] CLOUD_LAYERS = { Shader.PropertyToID("_DclCloudLayer0"), Shader.PropertyToID("_DclCloudLayer1"), Shader.PropertyToID("_DclCloudLayer2") };
    private static readonly int CLOUD_LAYER_OPACITY = Shader.PropertyToID("_DclCloudLayerOpacity");
    private static readonly int CLOUD_LAYER_FLOW = Shader.PropertyToID("_DclCloudLayerFlow");
    private static readonly int CLOUD_LAYER_TILING = Shader.PropertyToID("_DclCloudLayerTiling");
    private static readonly int CLOUD_LAYER_OFFSET_U = Shader.PropertyToID("_DclCloudLayerOffsetU");
    private static readonly int CLOUD_SHADOW_COLOR = Shader.PropertyToID("_DclCloudShadowColor");
    private static readonly int CLOUD_LIT_COLOR = Shader.PropertyToID("_DclCloudLitColor");
    private static readonly int CLOUDS_PARAMS = Shader.PropertyToID("_DclCloudsParams");
    private static readonly int CLOUDS_MODE = Shader.PropertyToID("_DclCloudsMode");
    private static readonly int CLOUDS_PARAMS2 = Shader.PropertyToID("_DclCloudsParams2");
    private static readonly int SUN_DIRECTION = Shader.PropertyToID("_DclSunDirection");
    private static readonly int HORIZON_NOISE = Shader.PropertyToID("_DclHorizonNoise");
    private static readonly int HORIZON_NOISE_PARAMS = Shader.PropertyToID("_DclHorizonNoiseParams");
    private static readonly int CELESTIAL_PARAMS = Shader.PropertyToID("_DclCelestialParams");
    private static readonly int STARS_PARAMS = Shader.PropertyToID("_DclStarsParams");
    private static readonly int STARS_PARAMS2 = Shader.PropertyToID("_DclStarsParams2");
    private static readonly int STARS_PARAMS3 = Shader.PropertyToID("_DclStarsParams3");
    private static readonly int MOON_MASK_OFFSET = Shader.PropertyToID("_DclMoonMaskOffset");
    private static readonly int SUN_HAZE_PARAMS = Shader.PropertyToID("_DclSunHazeParams");
    private static readonly int SUN_HAZE_PARAMS2 = Shader.PropertyToID("_DclSunHazeParams2");
    private static readonly int SUN_HAZE_TOP = Shader.PropertyToID("_DclSunHazeTop");
    private static readonly int SUN_HAZE_BOTTOM = Shader.PropertyToID("_DclSunHazeBottom");
    private const int MAX_CLOUD_LAYERS = 3;

    // Fraction of the light intensity removed at the middle of the sun/moon crossover, so the direction swing is invisible.
    private const float CELESTIAL_SWAP_INTENSITY_DIP = 0.99f;

    // |y| of the light direction above which world up is too close to parallel for LookRotation.
    private const float LOOK_ROTATION_POLE_THRESHOLD = 0.99f;

    [Header("Look")]
    [Tooltip("The look that ships; alternatives for the debug dropdown live on the skybox settings asset.")]
    [SerializeField] private SkyboxLookPreset preset = null!;

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private AnimationClip lightAnimation;
    private Animation lightAnimator;

    private LensFlareComponentSRP? lensFlare;
    private LensFlareDataSRP? activeLensFlareData;

    private Material skyboxMaterial;
    private bool shaderTimeDisabled;

    // x is a preset static, y the per-frame backlight weight; see SkyboxGlobals.hlsl.
    private Vector4 celestialParams = new (0f, 1f, 0f, 0f);

    // Time the palette and light were last evaluated at; MinValue until Initialize.
    private float currentTimeOfDay = float.MinValue;

    // Light intensity before the crossover dip; the fallback for an empty intensity curve on the computed path.
    private float lightIntensityBeforeDip;

    // Whether the disc currently shows the moon; written with the light, read for the disc colour.
    private bool moonActive;

    public SkyboxLookPreset Preset => preset;

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, float initialTimeOfDay, bool lensFlareEnabled = true)
    {
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

            lightIntensityBeforeDip = directionalLight.intensity;
            InitializeLensFlare(lensFlareEnabled);
        }

        ReportPresetIssues();
        ApplyPresetStatics();

        if (preset.Fog)
            RenderSettings.fog = true;

        UpdateSkybox(initialTimeOfDay);
    }

    // Flag combinations the shader variant cannot honour, once per preset rather than per frame.
    private void ReportPresetIssues()
    {
        if (!preset.UseSkyLut && (preset.UseCloudsV2 || preset.UseStarsV2 || preset.SunHaze))
            ReportHub.LogWarning(ReportCategory.SKYBOX, $"Skybox preset {preset.name}: Clouds v2, Stars v2 and Sun Haze need Use Sky Lut and are ignored");

        if (preset.UseSkyLut && preset.SkyLut == null)
            ReportHub.LogWarning(ReportCategory.SKYBOX, $"Skybox preset {preset.name}: Use Sky Lut is on but no sky LUT is baked");

        if (preset.SunHaze && !preset.ComputeCelestialPath)
            ReportHub.LogWarning(ReportCategory.SKYBOX, $"Skybox preset {preset.name}: Sun Haze needs Compute Celestial Path and is ignored");
    }

    /// <summary>
    ///     Switches the look: writes the preset's static material values once and re-evaluates the current
    ///     time of day so the change is visible immediately.
    /// </summary>
    public void ApplyPreset(SkyboxLookPreset newPreset)
    {
        preset = newPreset;

        // Drop the current flare so the new preset's entries decide it, including no flare at all.
        activeLensFlareData = null;

        if (lensFlare != null)
            lensFlare.lensFlareData = null;

        ReportPresetIssues();
        RefreshLook();
    }

    /// <summary>
    ///     Evaluates the light and the palette at the time of day. The light goes first so the disc colour can
    ///     read which body it belongs to. A repeated time (paused clock) is skipped.
    /// </summary>
    public void UpdateSkybox(float timeOfDay)
    {
        if (currentTimeOfDay == timeOfDay) return;

        currentTimeOfDay = timeOfDay;
        float phase = preset.EvaluatePhase(timeOfDay);
        UpdateDirectionalLight(timeOfDay, phase);
        UpdatePalette(timeOfDay, phase);
    }

    public void DisableSkyboxTime()
    {
        shaderTimeDisabled = true;
        skyboxMaterial.SetFloat(CLOUDS_ROTATION_SPEED, 0f);
        skyboxMaterial.SetFloat(SECOND_SUN_ROTATION_SPEED, 0f);

        // Override shader-side time per-material to disable stars rotation and sky oscillation,
        // which are driven by Unity's _TimeParameters global but have no material speed property.
        skyboxMaterial.SetVector(TIME_PARAMETERS, Vector4.one);
    }

    private void OnDestroy()
    {
        ResetGlobals();
    }

    private void OnDisable()
    {
        ResetGlobals();
    }

    // Clears every global this controller writes, so none of them outlives it.
    private static void ResetGlobals()
    {
        Shader.SetGlobalVector(CLOUDS_MODE, Vector4.zero);
        Shader.SetGlobalVector(HORIZON_NOISE_PARAMS, Vector4.zero);
        Shader.SetGlobalVector(CELESTIAL_PARAMS, Vector4.zero);
        Shader.SetGlobalVector(STARS_PARAMS, Vector4.zero);
        Shader.SetGlobalVector(SUN_HAZE_PARAMS, Vector4.zero);
        Shader.SetGlobalVector(MOON_MASK_OFFSET, Vector4.zero);
        Shader.DisableKeyword(STYLIZED_KEYWORD);
        Shader.SetGlobalTexture(HORIZON_NOISE, null);

        for (var i = 0; i < MAX_CLOUD_LAYERS; i++)
            Shader.SetGlobalTexture(CLOUD_STRIPS[i], null);
    }

    private void ApplyPresetStatics()
    {
        if (!skyboxMaterial)
            return;

        skyboxMaterial.SetFloat(ZENIT_SPREAD, preset.ZenitSpread);
        skyboxMaterial.SetFloat(ZENIT_BLEND, preset.ZenitBlend);
        skyboxMaterial.SetFloat(GROUND_SPREAD, preset.GroundSpread);
        skyboxMaterial.SetFloat(GROUND_BLEND, preset.GroundBlend);
        skyboxMaterial.SetFloat(BLEND_TWIST, preset.BlendTwist);
        skyboxMaterial.SetFloat(RIM_SPREAD, preset.RimSpread);

        // With the lookup on, the horizon band lives inside the elevation gradients, so the rim line is switched off.
        skyboxMaterial.SetFloat(RIM_OPACITY, preset.UseSkyLut ? 0f : preset.RimOpacity);
        skyboxMaterial.SetFloat(USE_SKY_LUT, preset.UseSkyLut ? 1f : 0f);

        if (preset.UseSkyLut)
        {
            Shader.EnableKeyword(STYLIZED_KEYWORD);
            skyboxMaterial.EnableKeyword(STYLIZED_KEYWORD);
        }
        else
        {
            Shader.DisableKeyword(STYLIZED_KEYWORD);
            skyboxMaterial.DisableKeyword(STYLIZED_KEYWORD);
        }

        skyboxMaterial.SetTexture(SKY_LUT, preset.SkyLut);

        // Horizon noise for the lookup path; strength 0 (or no texture) disables it in the shader.
        Shader.SetGlobalTexture(HORIZON_NOISE, preset.SkyHorizonNoise);
        float noiseStrength = preset.UseSkyLut && preset.SkyHorizonNoise != null ? preset.SkyHorizonNoiseStrength : 0f;
        Shader.SetGlobalVector(HORIZON_NOISE_PARAMS, new Vector4(noiseStrength, preset.SkyHorizonNoiseTiling.x, preset.SkyHorizonNoiseTiling.y, preset.SkyHorizonNoiseSpeed));

        // Backlight mode; the backlight weight (y) is written per frame.
        celestialParams.x = preset.ComputeCelestialPath ? 1f : 0f;
        Shader.SetGlobalVector(CELESTIAL_PARAMS, celestialParams);

        // The computed path places the crescent hole in the moon's own frame; legacy keeps the material's world nudge.
        Shader.SetGlobalVector(MOON_MASK_OFFSET, new Vector4(preset.ComputedMoonMaskOffset.x, preset.ComputedMoonMaskOffset.y, preset.ComputeCelestialPath ? 1f : 0f, 0f));

        // Stars v2 statics; the per-phase brightness is written every frame by UpdateStarsV2.
        Shader.SetGlobalVector(STARS_PARAMS2, new Vector4(preset.StarsDensity, preset.StarsSize * Mathf.Deg2Rad, preset.StarsRotationSpeed, preset.StarsTwinkleSpeed));
        Shader.SetGlobalVector(STARS_PARAMS3, new Vector4(preset.StarsHorizonFade.x, preset.StarsHorizonFade.y, 0f, 0f));

        if (!preset.UseStarsV2)
            Shader.SetGlobalVector(STARS_PARAMS, Vector4.zero);

        // Sun haze statics; the factor (x) is written every frame by UpdateCelestialPath and stays 0 otherwise.
        Shader.SetGlobalVector(SUN_HAZE_PARAMS, new Vector4(0f, preset.SunHazeSizeBoost, preset.SunHazeSquash, preset.SunHazeEdgeSoftness));
        Shader.SetGlobalVector(SUN_HAZE_PARAMS2, new Vector4(preset.SunHazeRimStrength, preset.SunHazeRimDetail, preset.SunHazeRimSpeed, preset.SunHazeGradientPower));
        Shader.SetGlobalVector(SUN_HAZE_TOP, preset.SunHazeTopColor);
        Shader.SetGlobalVector(SUN_HAZE_BOTTOM, preset.SunHazeBottomColor);
        skyboxMaterial.SetFloat(STARS_BRIGHTNESS, preset.StarsBrightness);
        skyboxMaterial.SetTexture(STARS_TEXTURE, preset.StarsTexture);
        skyboxMaterial.SetTexture(CLOUDS_CUBEMAP, preset.CloudsCubemap);
        skyboxMaterial.SetFloat(CLOUD_OPACITY, preset.CloudOpacity);
        skyboxMaterial.SetVector(MOON_MASK_POSITION, preset.MoonMaskPosition);
        skyboxMaterial.SetFloat(SECOND_SUN_SIZE_FACTOR, preset.SecondSunSizeFactor);
        skyboxMaterial.SetFloat(SECOND_SUN_ORBIT_SIZE, preset.SecondSunOrbitSize);

        // Rotation speeds stay at zero while shader time is disabled (visual tests, frozen sky).
        skyboxMaterial.SetFloat(CLOUDS_ROTATION_SPEED, shaderTimeDisabled ? 0f : preset.CloudsRotationSpeed);
        skyboxMaterial.SetFloat(SECOND_SUN_ROTATION_SPEED, shaderTimeDisabled ? 0f : preset.SecondSunRotationSpeed);

        ApplyCloudsV2Statics();

        RenderSettings.ambientMode = preset.IndirectLight ? AmbientMode.Trilight : AmbientMode.Skybox;
        RenderSettings.reflectionIntensity = preset.ReflectionIntensity;
    }

    /// <summary>
    ///     Re-applies the preset's material values and re-evaluates the current time, so an edited preset shows up
    ///     without waiting for the next time change.
    /// </summary>
    private void RefreshLook()
    {
        if (!skyboxMaterial)
            return;

        ApplyPresetStatics();

        if (currentTimeOfDay > float.MinValue)
        {
            float phase = preset.EvaluatePhase(currentTimeOfDay);
            UpdateDirectionalLight(currentTimeOfDay, phase);
            UpdatePalette(currentTimeOfDay, phase);
        }
    }

    private void ApplyCloudsV2Statics()
    {
        IReadOnlyList<SkyboxLookPreset.CloudLayer> layers = preset.CloudLayers;
        int layerCount = Mathf.Min(layers.Count, MAX_CLOUD_LAYERS);
        var opacity = Vector4.zero;
        var tiling = Vector4.one;
        var offsetU = Vector4.zero;

        for (var i = 0; i < MAX_CLOUD_LAYERS; i++)
        {
            if (i < layerCount)
            {
                SkyboxLookPreset.CloudLayer layer = layers[i];
                Shader.SetGlobalTexture(CLOUD_STRIPS[i], layer.Strip);
                Shader.SetGlobalVector(CLOUD_LAYERS[i], new Vector4(layer.StretchV, layer.OffsetV, layer.Speed, layer.Strength));
                opacity[i] = layer.Opacity;
                tiling[i] = Mathf.Max(1f, Mathf.Round(layer.TilingU));
                offsetU[i] = layer.OffsetU;
            }
            else
            {
                Shader.SetGlobalTexture(CLOUD_STRIPS[i], null);
                Shader.SetGlobalVector(CLOUD_LAYERS[i], Vector4.zero);
            }
        }

        opacity.w = layerCount;
        Shader.SetGlobalVector(CLOUD_LAYER_OPACITY, opacity);
        Shader.SetGlobalVector(CLOUD_LAYER_TILING, tiling);
        Shader.SetGlobalVector(CLOUD_LAYER_OFFSET_U, offsetU);
        Shader.SetGlobalVector(CLOUDS_PARAMS, new Vector4(preset.CloudsRampKnee, preset.CloudsHighlightStrength, preset.CloudsHighlightThreshold, preset.CloudsHighlightFalloff));
        Shader.SetGlobalVector(CLOUDS_PARAMS2, new Vector4(preset.CloudsZenithFadeStart, preset.CloudsZenithFadeEnd, preset.CloudsOcclusionStart, preset.CloudsOcclusionEnd));

        Shader.SetGlobalVector(CLOUDS_MODE, new Vector4(
            preset.CloudsV2Ramp ? 1f : 0f,
            preset.CloudsV2Backlight ? 1f : 0f,
            preset.CloudsV2Flow ? 1f : 0f,
            preset.UseCloudsV2 ? 1f : 0f));
    }

    private void UpdateCloudsV2(float phase)
    {
        if (!preset.UseCloudsV2) return;

        Shader.SetGlobalVector(CLOUD_SHADOW_COLOR, preset.CloudsShadowColorRamp.Evaluate(phase));
        Shader.SetGlobalVector(CLOUD_LIT_COLOR, preset.CloudsColorRamp.Evaluate(phase));

        IReadOnlyList<SkyboxLookPreset.CloudLayer> layers = preset.CloudLayers;
        var flow = Vector4.zero;

        for (var i = 0; i < Mathf.Min(layers.Count, MAX_CLOUD_LAYERS); i++)
            flow[i] = SkyboxLookPreset.EvaluateByPhase(layers[i].FlowByPhase, phase);

        Shader.SetGlobalVector(CLOUD_LAYER_FLOW, flow);
    }

    /// <summary>
    ///     Everything colour-like is sampled at the preset's phase, so the palette timing is authored once on the
    ///     time-to-phase curve. Sun position, disc size and halo stay on time (see <see cref="UpdateDirectionalLight" />).
    /// </summary>
    private void UpdatePalette(float timeOfDay, float phase)
    {
        UpdateIndirectLight(phase);
        UpdateSkyboxColor(phase, timeOfDay);
        UpdateFog(phase);
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors
    ///     from the defined gradients based on the phase
    /// </summary>
    private void UpdateIndirectLight(float phase)
    {
        if (!preset.IndirectLight) return;

        RenderSettings.ambientSkyColor = preset.IndirectSkyRamp.Evaluate(phase);
        RenderSettings.ambientEquatorColor = preset.IndirectEquatorRamp.Evaluate(phase);
        RenderSettings.ambientGroundColor = preset.GroundEquatorRamp.Evaluate(phase);
    }

    /// <summary>
    ///     Samples the light colour at the phase and everything tied to the physical position of the sun (rotation,
    ///     disc size and opacity, halo, moon mask, lens flare) at the time of day. The rotation comes from the clip,
    ///     or from the computed sun and moon arcs when the preset asks for them.
    /// </summary>
    private void UpdateDirectionalLight(float timeOfDay, float phase)
    {
        if (!directionalLight) return;

        directionalLight.color = preset.DirectionalColorRamp.Evaluate(phase);

        var swapDip = 0f;
        moonActive = false;

        if (preset.ComputeCelestialPath)
            swapDip = UpdateCelestialPath(timeOfDay, out moonActive);
        else
        {
            //sample the right frame of the animation
            if (lightAnimation)
            {
                lightAnimator[lightAnimation.name].time = timeOfDay * lightAnimator[lightAnimation.name].length;
                lightAnimator.Play(lightAnimation.name);
                lightAnimator.Sample();
                lightAnimator.Stop();
            }

            // Direction toward the sun for the cloud backlight; the moon is its opposite.
            Shader.SetGlobalVector(SUN_DIRECTION, -directionalLight.transform.forward);

            // The clip carries the disc opacity as localScale.y; a preset curve overrides it when authored.
            RenderSettings.skybox.SetFloat(SUN_OPACITY, EvaluateOrFallback(preset.SunOpacity, timeOfDay, directionalLight.gameObject.transform.localScale.y));
            RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, preset.MoonMaskSize.Evaluate(timeOfDay));
        }

        // The cloud backlight fades with the same dip that hides the disc, so its direction can switch bodies unseen.
        celestialParams.y = 1f - swapDip;
        Shader.SetGlobalVector(CELESTIAL_PARAMS, celestialParams);

        // The clip carries intensity and the disc size as localScale.x; a preset curve overrides each when authored.
        // On the computed path the intensity fallback is the last undipped value, so the dip is applied once.
        // The moon keeps one disc size so its crescent does not change shape through the night.
        Vector3 directionalLightLocalScale = directionalLight.gameObject.transform.localScale;
        float intensityFallback = preset.ComputeCelestialPath ? lightIntensityBeforeDip : directionalLight.intensity;
        lightIntensityBeforeDip = EvaluateOrFallback(preset.LightIntensity, timeOfDay, intensityFallback);
        directionalLight.intensity = lightIntensityBeforeDip * (1f - swapDip * CELESTIAL_SWAP_INTENSITY_DIP);
        float discSize = moonActive ? preset.ComputedMoonDiscSize : EvaluateOrFallback(preset.SunSize, timeOfDay, directionalLightLocalScale.x);
        RenderSettings.skybox.SetFloat(SUN_SIZE, discSize);

        //sampling sun radiance and intensity curves
        RenderSettings.skybox.SetFloat(SUN_RADIANCE, preset.SunRadiance.Evaluate(timeOfDay));
        RenderSettings.skybox.SetFloat(SUN_RADIANCE_INTENSITY, preset.SunRadianceIntensity.Evaluate(timeOfDay));

        UpdateLensFlare(timeOfDay, swapDip);
    }

    /// <summary>
    ///     Places the light on the sun arc by day and on the moon arc by night. The crossover happens inside the swap
    ///     window, where the disc is hidden and the light dimmed, so neither the disc nor the shadows are seen jumping.
    ///     Returns how deep into the crossover dip the time is (0 = none, 1 = mid-swap) and whether the moon is the body
    ///     the disc currently shows.
    /// </summary>
    private float UpdateCelestialPath(float timeOfDay, out bool moonActive)
    {
        Vector3 sunDirection = SkyboxCelestialMath.ArcDirection(SkyboxCelestialMath.CelestialProgress(timeOfDay, preset.SunriseTime, preset.SunsetTime), preset.SunPathAzimuth, preset.SunPathTilt);
        Vector3 moonDirection = SkyboxCelestialMath.ArcDirection(SkyboxCelestialMath.CelestialProgress(timeOfDay, preset.MoonriseTime, preset.MoonsetTime), preset.MoonPathAzimuth, preset.MoonPathTilt);

        SkyboxCelestialMath.EvaluateSwap(timeOfDay, preset.MoonriseTime, preset.MoonsetTime, preset.CelestialSwapDuration, out float moonWeight, out float dip);
        moonActive = moonWeight > SkyboxCelestialMath.MOON_ACTIVE_WEIGHT;

        Vector3 lightDirection = Vector3.Slerp(sunDirection, moonDirection, moonWeight);
        Vector3 upHint = Mathf.Abs(lightDirection.y) > LOOK_ROTATION_POLE_THRESHOLD ? Vector3.forward : Vector3.up;
        directionalLight.transform.rotation = Quaternion.LookRotation(-lightDirection, upHint);

        Shader.SetGlobalVector(SUN_DIRECTION, moonActive ? moonDirection : sunDirection);

        // The haze dresses the sun only: nothing at the top of its window, full at the horizon and below.
        float hazeFactor = preset.SunHaze && !moonActive ? 1f - SkyboxCelestialMath.Smooth01(0f, preset.SunHazeHeight, sunDirection.y) : 0f;
        Shader.SetGlobalVector(SUN_HAZE_PARAMS, new Vector4(hazeFactor, preset.SunHazeSizeBoost, preset.SunHazeSquash, preset.SunHazeEdgeSoftness));
        RenderSettings.skybox.SetFloat(SUN_OPACITY, 1f - dip);
        RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, moonActive ? preset.ComputedMoonMaskSize : 0f);

        return dip;
    }

    /// <summary>
    ///     Sun disc colour at the phase; while the moon is the active body it has its own ramp over its own
    ///     rise-to-set progress.
    /// </summary>
    private Color DiscColor(float phase, float timeOfDay)
    {
        if (moonActive)
            return preset.MoonColorRamp.Evaluate(Mathf.Clamp01(SkyboxCelestialMath.CelestialProgress(timeOfDay, preset.MoonriseTime, preset.MoonsetTime)));

        return preset.SunColorRamp.Evaluate(phase);
    }

    private void UpdateStarsV2(float phase)
    {
        if (!preset.UseStarsV2) return;

        float visibility = SkyboxLookPreset.EvaluateByPhase(preset.StarsBrightnessByPhase, phase);
        Shader.SetGlobalVector(STARS_PARAMS, new Vector4(preset.StarsV2Brightness * visibility, preset.StarsTwinkle, preset.StarsPatchStrength, preset.ShootingStarsRate * visibility));
    }

    private static float EvaluateOrFallback(AnimationCurve curve, float timeOfDay, float fallback) =>
        curve.length > 0 ? curve.Evaluate(timeOfDay) : fallback;

    private void InitializeLensFlare(bool lensFlareEnabled)
    {
        lensFlare = directionalLight.gameObject.GetComponent<LensFlareComponentSRP>()
                    ?? directionalLight.gameObject.AddComponent<LensFlareComponentSRP>();

        lensFlare.useOcclusion = true;
        lensFlare.enabled = lensFlareEnabled;
    }

    private void UpdateLensFlare(float timeOfDay, float swapDip)
    {
        if (lensFlare == null) return;

        lensFlare.intensity = preset.LensFlareIntensity.Evaluate(timeOfDay) * (1f - swapDip);

        LensFlareDataSRP? newFlareData = SkyboxLookPreset.ActiveLensFlareEntry(preset.LensFlareEntries, timeOfDay)?.FlareAsset;

        if (newFlareData != activeLensFlareData)
        {
            activeLensFlareData = newFlareData;
            lensFlare.lensFlareData = newFlareData;
        }
    }

    /// <summary>
    ///     Updates the exposed colour parameters of the material at the given phase. The time of day only picks which
    ///     body the disc colour belongs to.
    /// </summary>
    private void UpdateSkyboxColor(float phase, float timeOfDay)
    {
        RenderSettings.skybox.SetColor(ZENIT_COLOR, preset.SkyZenitColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(HORIZON_COLOR, preset.SkyHorizonColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(NADIR_COLOR, preset.SkyNadirColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(SUN_COLOR, DiscColor(phase, timeOfDay));
        RenderSettings.skybox.SetColor(RIM_COLOR, preset.RimColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(CLOUDS_COLOR, preset.CloudsColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetFloat(CLOUD_HIGHLIGHTS, preset.CloudsHighlightsIntensity.Evaluate(phase));
        RenderSettings.skybox.SetFloat(SKY_PHASE, phase);

        UpdateCloudsV2(phase);
        UpdateStarsV2(phase);
    }

    /// <summary>
    ///     Fog colour and exponential density at the phase, when the preset drives fog.
    /// </summary>
    private void UpdateFog(float phase)
    {
        if (!preset.Fog) return;

        RenderSettings.fogColor = preset.FogColorRamp.Evaluate(phase);
        RenderSettings.fogDensity = SkyboxLookPreset.EvaluateByPhase(preset.FogDensityByPhase, phase);
    }

#if UNITY_EDITOR
    public bool editMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if (editMode)
            Initialize(RenderSettings.skybox, null!, null!, 0.5f);
    }

    // Re-applies the preset every frame in edit mode so inspector changes show immediately.
    private void Update()
    {
        if (editMode && preset)
            RefreshLook();
    }

    // Lets the preset reference be swapped from the inspector while the authoring scene is playing.
    private void OnValidate()
    {
        if (Application.isPlaying && skyboxMaterial && preset)
            ApplyPreset(preset);
    }
#endif
}
