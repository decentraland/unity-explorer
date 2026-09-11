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
        activeLensFlareData = null;

        if (!skyboxMaterial)
            return;

        ApplyPresetStatics();

        if (targetTimeOfDay > float.MinValue)
            UpdatePalette(targetTimeOfDay);

        if (directionalLightTimeOfDay > float.MinValue)
            UpdateDirectionalLight(directionalLightTimeOfDay);
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
    }

    private void OnDisable()
    {
        transitionCancellationTokenSource.SafeCancelAndDispose();
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

        if (preset.IndirectLight)
            RenderSettings.ambientMode = AmbientMode.Trilight;

        if (preset.Fog)
            RenderSettings.fog = true;
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
        UpdateSkyboxColor(phase);
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
    ///     Samples the light colour at the phase and everything tied to the sun's physical position (rotation clip,
    ///     disc size and opacity, halo, moon mask, lens flare) at the time of day.
    /// </summary>
    private void UpdateDirectionalLight(float timeOfDay)
    {
        if (!directionalLight) return;

        //change the color of the light based on the color ramp
        directionalLight.color = preset.DirectionalColorRamp.Evaluate(preset.EvaluatePhase(timeOfDay));

        //sample the right frame of the animation
        if (lightAnimation)
        {
            lightAnimator[lightAnimation.name].time = timeOfDay * lightAnimator[lightAnimation.name].length;
            lightAnimator.Play(lightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        // The clip carries intensity and the disc size/opacity as localScale.x/y channels. A preset curve overrides
        // each of them when authored, so the clip can be reduced to the sun rotation.
        Vector3 directionalLightLocalScale = directionalLight.gameObject.transform.localScale;
        directionalLight.intensity = EvaluateOrFallback(preset.LightIntensity, timeOfDay, directionalLight.intensity);
        RenderSettings.skybox.SetFloat(SUN_SIZE, EvaluateOrFallback(preset.SunSize, timeOfDay, directionalLightLocalScale.x));
        RenderSettings.skybox.SetFloat(SUN_OPACITY, EvaluateOrFallback(preset.SunOpacity, timeOfDay, directionalLightLocalScale.y));

        //sampling sun radiance and intensity curves
        RenderSettings.skybox.SetFloat(SUN_RADIANCE, preset.SunRadiance.Evaluate(timeOfDay));
        RenderSettings.skybox.SetFloat(SUN_RADIANCE_INTENSITY, preset.SunRadianceIntensity.Evaluate(timeOfDay));

        //change size of moon mask
        RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, preset.MoonMaskSize.Evaluate(timeOfDay));

        UpdateLensFlare(timeOfDay);
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

    private void UpdateLensFlare(float timeOfDay)
    {
        if (lensFlare == null) return;

        lensFlare.intensity = preset.LensFlareIntensity.Evaluate(timeOfDay);

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
    ///     Updates the exposed colour parameters of the material at the given phase
    /// </summary>
    private void UpdateSkyboxColor(float phase)
    {
        RenderSettings.skybox.SetColor(ZENIT_COLOR, preset.SkyZenitColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(HORIZON_COLOR, preset.SkyHorizonColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(NADIR_COLOR, preset.SkyNadirColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(SUN_COLOR, preset.SunColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(RIM_COLOR, preset.RimColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetColor(CLOUDS_COLOR, preset.CloudsColorRamp.Evaluate(phase));
        RenderSettings.skybox.SetFloat(CLOUD_HIGHLIGHTS, preset.CloudsHighlightsIntensity.Evaluate(phase));
        RenderSettings.skybox.SetFloat(SKY_PHASE, phase);
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

    // Lets the preset reference be swapped from the inspector while the authoring scene is playing.
    private void OnValidate()
    {
        if (Application.isPlaying && skyboxMaterial && preset)
            ApplyPreset(preset);
    }
#endif
}
