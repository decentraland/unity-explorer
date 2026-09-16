using DCL.Diagnostics;
using DCL.SkyBox;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using Cysharp.Threading.Tasks;
using System.Threading;
using Utility;

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
    private const int MAX_CLOUD_LAYERS = 3;

    // Fraction of the light intensity removed at the middle of the sun/moon crossover, so the direction swing is invisible.
    private const float CELESTIAL_SWAP_INTENSITY_DIP = 0.99f;

    [Header("Look")]
    [SerializeField] private SkyboxLookPreset preset = null!;

    [Tooltip("Presets offered by the debug panel dropdown for runtime comparison.")]
    [SerializeField] private SkyboxLookPreset[] availablePresets = Array.Empty<SkyboxLookPreset>();

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private AnimationClip lightAnimation;
    private Animation lightAnimator;

    private LensFlareComponentSRP? lensFlare;
    private LensFlareDataSRP? activeLensFlareData;

    private Material skyboxMaterial;
    private bool shaderTimeDisabled;

    // x/y/z are preset statics, w is the per-frame backlight weight; see SkyboxGlobals.hlsl.
    private Vector4 celestialParams = new (0f, 0f, 0f, 1f);

    private float directionalLightTimeOfDay = float.MinValue;
    private float targetTimeOfDay = float.MinValue;
    private CancellationTokenSource transitionCancellationTokenSource;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration;

    public SkyboxLookPreset Preset => preset;

    public IReadOnlyList<SkyboxLookPreset> AvailablePresets => availablePresets;

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, float initialTimeOfDay, bool lensFlareEnabled = true, bool freezeTime = false)
    {
        // Pre-seed transition target so UpdateSkybox below short-circuits and skips the directional-light
        // coroutine, whose first sync tick would Lerp from float.MinValue and clamp the gradient samplers.
        if (freezeTime)
        {
            directionalLightTimeOfDay = initialTimeOfDay;
            targetTimeOfDay = initialTimeOfDay;
        }

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

            InitializeLensFlare(lensFlareEnabled);
        }

        ApplyPresetStatics();

        UpdateSkybox(initialTimeOfDay);

        directionalLightTimeOfDay = initialTimeOfDay;
        UpdateDirectionalLight(initialTimeOfDay);
    }

    /// <summary>
    ///     Switches the look: writes the preset's static material values once and re-evaluates the current
    ///     time of day so the change is visible immediately.
    /// </summary>
    public void ApplyPreset(SkyboxLookPreset newPreset)
    {
        preset = newPreset;

        // Drop the cached flare so the new preset's entries are picked up on the next evaluation.
        activeLensFlareData = null;
        RefreshLook();
    }

    /// <summary>,
    ///     Calls all the necessary methods to update the skybox and environment
    /// </summary>
    public void UpdateSkybox(float timeOfDay)
    {
        UpdatePalette(timeOfDay);

        // we update light in intervals to hide visible artifacts for moving shadows (anti-aliasing related, espescially on the benches)
        if (!Mathf.Approximately(targetTimeOfDay, timeOfDay))
        {
            targetTimeOfDay = timeOfDay;
            StartDirectionalLightTransitionAsync(timeOfDay, transitionDuration).Forget();
        }
    }

    public void DisableSkyboxTime()
    {
        shaderTimeDisabled = true;
        skyboxMaterial.SetFloat(CLOUDS_ROTATION_SPEED, 0f);
        skyboxMaterial.SetFloat(SECOND_SUN_ROTATION_SPEED, 0f);

        // Override shader-side time per-material to disable stars rotation and sky oscillation,
        // which are driven by Unity's _TimeParameters global but have no material speed property.
        skyboxMaterial.SetVector(TIME_PARAMETERS, Vector4.one);

        // Cancel the pending directional light transition started during Initialize(),
        // which would lerp from float.MinValue and corrupt the light's position.
        transitionCancellationTokenSource?.Cancel();
    }

    private void OnDestroy()
    {
        transitionCancellationTokenSource.SafeCancelAndDispose();
        ResetGlobals();
    }

    private void OnDisable()
    {
        transitionCancellationTokenSource.SafeCancelAndDispose();
        ResetGlobals();
    }

    // Global shader values outlive Play mode in the Editor; without this the Scene view keeps rendering v2 clouds and
    // stars after a session on a v2 preset, whatever the preset field says.
    private static void ResetGlobals()
    {
        Shader.SetGlobalVector(CLOUDS_MODE, Vector4.zero);
        Shader.SetGlobalVector(HORIZON_NOISE_PARAMS, Vector4.zero);
        Shader.SetGlobalVector(CELESTIAL_PARAMS, Vector4.zero);
        Shader.SetGlobalVector(STARS_PARAMS, Vector4.zero);

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
        skyboxMaterial.SetTexture(SKY_LUT, preset.SkyLut);

        // Horizon noise for the lookup path; strength 0 (or no texture) disables it in the shader.
        Shader.SetGlobalTexture(HORIZON_NOISE, preset.SkyHorizonNoise);
        float noiseStrength = preset.UseSkyLut && preset.SkyHorizonNoise != null ? preset.SkyHorizonNoiseStrength : 0f;
        Shader.SetGlobalVector(HORIZON_NOISE_PARAMS, new Vector4(noiseStrength, preset.SkyHorizonNoiseTiling.x, preset.SkyHorizonNoiseTiling.y, preset.SkyHorizonNoiseSpeed));

        // Disc darkening (height 0 = off) and the backlight mode; the backlight weight (w) is written per frame.
        celestialParams = new Vector4(preset.DiscHorizonDarkening ? preset.DiscHorizonDarkeningHeight : 0f, preset.DiscHorizonDarkeningFloor, preset.ComputeCelestialPath ? 1f : 0f, celestialParams.w);
        Shader.SetGlobalVector(CELESTIAL_PARAMS, celestialParams);

        // Stars v2 statics; the per-phase brightness is written every frame by UpdateStarsV2.
        Shader.SetGlobalVector(STARS_PARAMS2, new Vector4(preset.StarsDensity, preset.StarsSize * Mathf.Deg2Rad, preset.StarsRotationSpeed, preset.StarsTwinkleSpeed));
        Shader.SetGlobalVector(STARS_PARAMS3, new Vector4(preset.StarsHorizonFade.x, preset.StarsHorizonFade.y, 0f, 0f));
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

        if (preset.IndirectLight)
            RenderSettings.ambientMode = AmbientMode.Trilight;

        if (preset.Fog)
            RenderSettings.fog = true;
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

        if (targetTimeOfDay > float.MinValue)
            UpdatePalette(targetTimeOfDay);

        if (directionalLightTimeOfDay > float.MinValue)
            UpdateDirectionalLight(directionalLightTimeOfDay);
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
                tiling[i] = layer.TilingU;
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

    private async UniTaskVoid StartDirectionalLightTransitionAsync(float newTimeOfDay, float duration)
    {
        transitionCancellationTokenSource = transitionCancellationTokenSource.SafeRestart();

        CancellationToken token = transitionCancellationTokenSource.Token;
        float startTime = directionalLightTimeOfDay;
        float startTimeStamp = UnityEngine.Time.time;

        while (UnityEngine.Time.time - startTimeStamp < duration && !token.IsCancellationRequested)
        {
            float progress = (UnityEngine.Time.time - startTimeStamp) / duration;
            float lerpedTimeOfDay = Mathf.Lerp(startTime, newTimeOfDay, progress);

            directionalLightTimeOfDay = lerpedTimeOfDay;
            UpdateDirectionalLight(lerpedTimeOfDay);

            await UniTask.Yield(token);
        }

        directionalLightTimeOfDay = newTimeOfDay;
        UpdateDirectionalLight(newTimeOfDay);
    }

    /// <summary>
    ///     Everything colour-like is sampled at the preset's phase, so the palette timing is authored once on the
    ///     time-to-phase curve. Sun position, disc size and halo stay on time (see <see cref="UpdateDirectionalLight" />).
    /// </summary>
    private void UpdatePalette(float timeOfDay)
    {
        float phase = preset.EvaluatePhase(timeOfDay);

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
    private void UpdateDirectionalLight(float timeOfDay)
    {
        if (!directionalLight) return;

        //change the color of the light based on the color ramp
        directionalLight.color = preset.DirectionalColorRamp.Evaluate(preset.EvaluatePhase(timeOfDay));

        var swapDip = 0f;
        var moonActive = false;

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
        celestialParams.w = 1f - swapDip;
        Shader.SetGlobalVector(CELESTIAL_PARAMS, celestialParams);

        // The clip carries intensity and the disc size as localScale.x; a preset curve overrides each when authored.
        // The moon keeps one disc size so its crescent does not change shape through the night.
        Vector3 directionalLightLocalScale = directionalLight.gameObject.transform.localScale;
        directionalLight.intensity = EvaluateOrFallback(preset.LightIntensity, timeOfDay, directionalLight.intensity) * (1f - swapDip * CELESTIAL_SWAP_INTENSITY_DIP);
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
        Vector3 sunDirection = ArcDirection(CelestialProgress(timeOfDay, preset.SunriseTime, preset.SunsetTime), preset.SunPathAzimuth, preset.SunPathTilt);
        Vector3 moonDirection = ArcDirection(CelestialProgress(timeOfDay, preset.MoonriseTime, preset.MoonsetTime), preset.MoonPathAzimuth, preset.MoonPathTilt);

        EvaluateCelestialSwap(timeOfDay, out float moonWeight, out float dip);
        moonActive = moonWeight > 0.5f;

        Vector3 lightDirection = Vector3.Slerp(sunDirection, moonDirection, moonWeight);
        Vector3 upHint = Mathf.Abs(lightDirection.y) > 0.99f ? Vector3.forward : Vector3.up;
        directionalLight.transform.rotation = Quaternion.LookRotation(-lightDirection, upHint);

        Shader.SetGlobalVector(SUN_DIRECTION, moonActive ? moonDirection : sunDirection);
        RenderSettings.skybox.SetFloat(SUN_OPACITY, 1f - dip);
        RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, moonActive ? preset.ComputedMoonMaskSize : 0f);

        if (moonActive)
            RenderSettings.skybox.SetVector(MOON_MASK_POSITION, MoonMaskNudge(moonDirection));

        return dip;
    }

    /// <summary>
    ///     The shader centres the crescent hole on normalize(lightDirection + (x, y, 0)), a world-space nudge. This
    ///     returns the nudge that lands the hole exactly on the wanted direction, the moon rotated by the preset
    ///     offset in its own frame, so the crescent keeps one shape wherever the moon is. The nudge must be
    ///     nudge = s * wanted - moon with nudge.z = 0, which fixes s; it has no solution for the few minutes the moon
    ///     crosses the depth plane, where the nudge falls back to the offset with its depth dropped.
    /// </summary>
    private Vector4 MoonMaskNudge(Vector3 moonDirection)
    {
        Vector3 right = Vector3.Cross(Vector3.up, moonDirection).normalized;
        Vector3 up = Vector3.Cross(moonDirection, right);
        Vector3 across = right * preset.ComputedMoonMaskOffset.x + up * preset.ComputedMoonMaskOffset.y;
        Vector3 wanted = (moonDirection + across).normalized;

        if (Mathf.Abs(wanted.z) < 1e-4f || Mathf.Sign(wanted.z) != Mathf.Sign(moonDirection.z))
            return new Vector4(across.x, across.y, 0f, 0f);

        Vector3 nudge = wanted * (moonDirection.z / wanted.z) - moonDirection;
        return new Vector4(nudge.x, nudge.y, 0f, 0f);
    }

    /// <summary>
    ///     Weight of the moon in the light direction, eased so the swing happens while the dip is deepest, and the
    ///     crossover dip itself, from the two swap windows: the one ending at moonrise and the one starting at moonset.
    /// </summary>
    private void EvaluateCelestialSwap(float timeOfDay, out float moonWeight, out float dip)
    {
        float swap = preset.CelestialSwapDuration;
        float evening = Wrap01(timeOfDay - Wrap01(preset.MoonriseTime - swap)) / swap;
        float morning = Wrap01(timeOfDay - preset.MoonsetTime) / swap;

        if (evening < 1f)
        {
            moonWeight = Smooth01(0.3f, 0.7f, evening);
            dip = SwapDip(evening);
            return;
        }

        if (morning < 1f)
        {
            moonWeight = 1f - Smooth01(0.3f, 0.7f, morning);
            dip = SwapDip(morning);
            return;
        }

        bool moonUp = Wrap01(timeOfDay - preset.MoonriseTime) < Wrap01(preset.MoonsetTime - preset.MoonriseTime);
        moonWeight = moonUp ? 1f : 0f;
        dip = 0f;
    }

    // 0 at the window edges, 1 across its middle.
    private static float SwapDip(float progress) =>
        Smooth01(0f, 0.25f, progress) * (1f - Smooth01(0.75f, 1f, progress));

    /// <summary>
    ///     Body progress on its circle: 0..1 from rise to set above the horizon, 1..2 below it until the next rise.
    /// </summary>
    private static float CelestialProgress(float timeOfDay, float rise, float set)
    {
        float above = Mathf.Max(Wrap01(set - rise), 1e-4f);
        float sinceRise = Wrap01(timeOfDay - rise);

        if (sinceRise < above)
            return sinceRise / above;

        return 1f + (sinceRise - above) / Mathf.Max(1f - above, 1e-4f);
    }

    /// <summary>
    ///     Direction to a body on a great-circle arc from the rise point on the horizon at the azimuth to the opposite
    ///     point, leaning sideways by the tilt. Progress 0..2 walks the full circle.
    /// </summary>
    private static Vector3 ArcDirection(float progress, float azimuthDeg, float tiltDeg)
    {
        float azimuth = azimuthDeg * Mathf.Deg2Rad;
        float tilt = tiltDeg * Mathf.Deg2Rad;
        var rise = new Vector3(Mathf.Sin(azimuth), 0f, Mathf.Cos(azimuth));
        var side = new Vector3(Mathf.Cos(azimuth), 0f, -Mathf.Sin(azimuth));
        Vector3 peak = Mathf.Cos(tilt) * Vector3.up + Mathf.Sin(tilt) * side;
        float angle = progress * Mathf.PI;
        return Mathf.Cos(angle) * rise + Mathf.Sin(angle) * peak;
    }

    private static float Wrap01(float value) =>
        value - Mathf.Floor(value);

    // HLSL-style smoothstep: 0 below edge0, 1 above edge1, eased in between.
    private static float Smooth01(float edge0, float edge1, float value)
    {
        float t = Mathf.Clamp01((value - edge0) / (edge1 - edge0));
        return t * t * (3f - 2f * t);
    }

    /// <summary>
    ///     Sun disc colour at the phase; with the computed path the moon has its own ramp over its own rise-to-set progress.
    /// </summary>
    private Color DiscColor(float phase, float timeOfDay)
    {
        if (preset.ComputeCelestialPath)
        {
            EvaluateCelestialSwap(timeOfDay, out float moonWeight, out _);

            if (moonWeight > 0.5f)
                return preset.MoonColorRamp.Evaluate(Mathf.Clamp01(CelestialProgress(timeOfDay, preset.MoonriseTime, preset.MoonsetTime)));
        }

        return preset.SunColorRamp.Evaluate(phase);
    }

    private void UpdateStarsV2(float phase)
    {
        if (!preset.UseStarsV2)
        {
            Shader.SetGlobalVector(STARS_PARAMS, Vector4.zero);
            return;
        }

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

        LensFlareDataSRP? newFlareData = GetActiveLensFlareData(timeOfDay);

        if (newFlareData != activeLensFlareData)
        {
            activeLensFlareData = newFlareData;
            lensFlare.lensFlareData = newFlareData;
        }
    }

    /// <summary>
    ///     Picks the entry with the latest StartTime at or before the given time, wrapping to the latest entry of the
    ///     day when the time is before every StartTime. Entry order is irrelevant so the preset list is never sorted.
    /// </summary>
    private LensFlareDataSRP? GetActiveLensFlareData(float timeOfDay)
    {
        IReadOnlyList<SkyboxLookPreset.LensFlareTimeEntry> entries = preset.LensFlareEntries;

        if (entries.Count == 0)
            return null;

        SkyboxLookPreset.LensFlareTimeEntry? active = null;
        SkyboxLookPreset.LensFlareTimeEntry latest = entries[0];

        for (var i = 0; i < entries.Count; i++)
        {
            SkyboxLookPreset.LensFlareTimeEntry entry = entries[i];

            if (entry.StartTime <= timeOfDay && (active == null || entry.StartTime > active.StartTime))
                active = entry;

            if (entry.StartTime > latest.StartTime)
                latest = entry;
        }

        return (active ?? latest).FlareAsset;
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
    ///     Updates the fog color of the RenderSettings if enabled
    /// </summary>
    private void UpdateFog(float phase)
    {
        if (preset.Fog)
            RenderSettings.fogColor = preset.FogColorRamp.Evaluate(phase);
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

    // The authoring scene has no system driving UpdateSkybox, so a preset edited during Play would only show after
    // the time changed. Re-applying every frame keeps gradient and layer tweaks live while authoring.
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
