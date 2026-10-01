using DCL.Diagnostics;
using DCL.SkyBox;
using System;
using System.Diagnostics.CodeAnalysis;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Cysharp.Threading.Tasks;
using System.Threading;
using Utility;

public class SkyboxRenderController : MonoBehaviour
{
    [Serializable]
    public class LensFlareTimeEntry
    {
        [Range(0f, 1f)]
        public float StartTime;
        public LensFlareDataSRP FlareAsset = null!;
    }

    private static readonly int ZENIT_COLOR = Shader.PropertyToID("_ZenitColor");
    private static readonly int HORIZON_COLOR = Shader.PropertyToID("_HorizonColor");
    private static readonly int NADIR_COLOR = Shader.PropertyToID("_NadirColor");
    private static readonly int SUN_COLOR = Shader.PropertyToID("_SunColor");
    private static readonly int RIM_COLOR = Shader.PropertyToID("_RimColor");
    private static readonly int CLOUDS_COLOR = Shader.PropertyToID("_CloudsColor");
    private static readonly int CLOUDS_CUBEMAP = Shader.PropertyToID("_Clouds_Cubemap");
    private static readonly int CLOUD_HIGHLIGHTS = Shader.PropertyToID("_Cloud_Highlights");
    private static readonly int SUN_SIZE = Shader.PropertyToID("_SunSize");
    private static readonly int SUN_OPACITY = Shader.PropertyToID("_SunOpacity");
    private static readonly int SUN_RADIANCE = Shader.PropertyToID("_Sun_Radiance");
    private static readonly int SUN_RADIANCE_INTENSITY = Shader.PropertyToID("_Sun_Radiance_Intensity");
    private static readonly int MOON_MASK_SIZE = Shader.PropertyToID("_Moon_Mask_Size");
    private static readonly int SECOND_SUN_SIZE_FACTOR = Shader.PropertyToID("_Second_Sun_Size_Factor");
    private static readonly int CLOUDS_ROTATION_SPEED = Shader.PropertyToID("_CloudsRotationSpeed");
    private static readonly int CLOUD_OPACITY = Shader.PropertyToID("_Cloud_Opacity");
    private static readonly int STARS_BRIGHTNESS = Shader.PropertyToID("_Stars_Brightness");
    private static readonly int SECOND_SUN_ROTATION_SPEED = Shader.PropertyToID("_Second_Sun_Rotation_Speed");
    private static readonly int TIME_PARAMETERS = Shader.PropertyToID("_TimeParameters");

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight = null!;
    [SerializeField] private AnimationClip lightAnimation = null!;
    private Animation? lightAnimator;

    [GradientUsage(true)]
    [SerializeField] private Gradient directionalColorRamp = null!;

    [GradientUsage(true)]
    [SerializeField] private Gradient sunColorRamp = null!;

    [SerializeField] private AnimationCurve sunRadiance = null!;
    [SerializeField] private AnimationCurve sunRadianceIntensity = null!;
    [SerializeField] private AnimationCurve moonMaskSize = null!;

    [Header("Lens Flare")]
    [SerializeField] private AnimationCurve lensFlareIntensity = null!;
    [SerializeField] private LensFlareTimeEntry[] lensFlareEntries = Array.Empty<LensFlareTimeEntry>();
    private LensFlareComponentSRP? lensFlare;
    private LensFlareDataSRP? activeLensFlareData;

    [Header("Skybox Color")]
    [GradientUsage(true)] [SerializeField] private Gradient skyZenitColorRamp = null!;
    [GradientUsage(true)] [SerializeField] private Gradient skyHorizonColorRamp = null!;
    [GradientUsage(true)] [SerializeField] private Gradient skyNadirColorRamp = null!;

    [InspectorName("Rim Light Color")]
    [GradientUsage(true)] [SerializeField] private Gradient rimColorRamp = null!;

    [Header("Indirect Lighting")]
    [InspectorName("Enabled")] [SerializeField] private bool indirectLight = true;
    [GradientUsage(true)] [SerializeField] private Gradient indirectSkyRamp = null!;
    [GradientUsage(true)] [SerializeField] private Gradient indirectEquatorRamp = null!;
    [GradientUsage(true)] [SerializeField] private Gradient groundEquatorRamp = null!;

    [Header("Clouds")]
    [GradientUsage(true)] [SerializeField] private Gradient cloudsColorRamp = null!;
    [SerializeField] private AnimationCurve cloudsHighlightsIntensity = null!;

    [Header("Fog")]
    [InspectorName("Enabled")] [SerializeField] private bool fog = true;
    [GradientUsage(true)] [SerializeField] private Gradient fogColorRamp = null!;

    private Material? skyboxMaterial;
    private Material? panoramicSkyboxMaterial;

    private SceneEnvironmentProfile? environmentOverride;
    private Texture? cloudsOverride;
    private Texture? defaultCloudsCubemap;
    private EquirectCubemapConverter? cloudsConverter;
    private bool cloudsConverterUnavailable;
    private float lastTimeOfDay;
    private float defaultCloudsOpacity;
    private float defaultCloudsRotationSpeed;
    private float defaultStarsBrightness;
    private float defaultSecondSunSizeFactor;
    private float defaultSunOpacity;
    private bool skyboxTimeDisabled;

    private float directionalLightTimeOfDay = float.MinValue;
    private float targetTimeOfDay = float.MinValue;
    private CancellationTokenSource? transitionCancellationTokenSource;

    [Header("Transition Settings")]
    [SerializeField] private float transitionDuration;

    private bool sunVisible => environmentOverride?.SunVisible ?? true;

    public void Initialize(Material skyboxMat, Material panoramicSkyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, float initialTimeOfDay, bool lensFlareEnabled = true, bool freezeTime = false)
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

            // Genesis material values a scene environment override replaces and a cleared override restores
            defaultCloudsOpacity = skyboxMaterial.GetFloat(CLOUD_OPACITY);
            defaultCloudsRotationSpeed = skyboxMaterial.GetFloat(CLOUDS_ROTATION_SPEED);
            defaultStarsBrightness = skyboxMaterial.GetFloat(STARS_BRIGHTNESS);
            defaultSecondSunSizeFactor = skyboxMaterial.GetFloat(SECOND_SUN_SIZE_FACTOR);
            defaultSunOpacity = skyboxMaterial.GetFloat(SUN_OPACITY);
            defaultCloudsCubemap = skyboxMaterial.GetTexture(CLOUDS_CUBEMAP);
        }

        if (panoramicSkyboxMat)
        {
#if UNITY_EDITOR
            panoramicSkyboxMat = new Material(panoramicSkyboxMat);
#endif
            panoramicSkyboxMaterial = panoramicSkyboxMat;
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
            // Unity-aware null check: in the Editor a missing component comes back as a placeholder object
            Animation? animator = directionalLight.gameObject.GetComponent<Animation>();

            if (animator == null)
                animator = directionalLight.gameObject.AddComponent<Animation>();

            lightAnimator = animator;

            if (!lightAnimation)
                ReportHub.LogWarning(ReportCategory.LANDSCAPE, "Skybox Controller: Directional Light animation has not been assigned");
            else
                animator.AddClip(lightAnimation, lightAnimation.name);

            InitializeLensFlare(lensFlareEnabled);
        }

        //setup indirect light
        if (indirectLight)
            RenderSettings.ambientMode = AmbientMode.Trilight;

        //setup fog
        if (fog)
            RenderSettings.fog = true;

        lastTimeOfDay = initialTimeOfDay;
        UpdateSkybox(initialTimeOfDay);

        directionalLightTimeOfDay = initialTimeOfDay;
        UpdateDirectionalLight(initialTimeOfDay);
    }

    /// <summary>
    ///     Replaces the time-of-day ramps, the cloud and star constants and the sun visibility with the ones of the profile
    ///     (null restores the defaults) and re-samples the environment at the current time of day. The directional light
    ///     keeps its ongoing transition; without one, the sun and moon are written straight to the material.
    /// </summary>
    public void SetEnvironmentOverride(SceneEnvironmentProfile? profile)
    {
        environmentOverride = profile;

        UpdateIndirectLight(lastTimeOfDay);

        if (skyboxMaterial != null)
        {
            UpdateSkyboxColor(skyboxMaterial, lastTimeOfDay);
            UpdateCloudsAndStars(skyboxMaterial);
        }

        UpdateFog(lastTimeOfDay);

        if (directionalLight)
            UpdateDirectionalLight(Mathf.Clamp01(directionalLightTimeOfDay));
        else if (skyboxMaterial != null)
            UpdateSunAndMoon(skyboxMaterial, lastTimeOfDay, defaultSunOpacity);
    }

    /// <summary>
    ///     Shows the equirectangular texture as the visible sky through the panoramic material; null restores the time-of-day skybox material.
    ///     Time-of-day keeps updating the skybox material while it is swapped out, so restoring it is seamless.
    /// </summary>
    public void SetSkyboxOverride(Texture? equirect)
    {
        if (equirect != null && panoramicSkyboxMaterial != null)
        {
            panoramicSkyboxMaterial.mainTexture = equirect;
            RenderSettings.skybox = panoramicSkyboxMaterial;
        }
        else
            RenderSettings.skybox = skyboxMaterial;
    }

    /// <summary>
    ///     Projects the equirectangular texture into the cloud cubemap of the time-of-day skybox material; null restores the
    ///     default cloud cubemap and releases the projected one. A render texture source (live video) is projected again on
    ///     every <see cref="ProjectLiveClouds" /> call.
    /// </summary>
    public void SetCloudsOverride(Texture? equirect)
    {
        cloudsOverride = equirect;

        if (skyboxMaterial == null) return;

        if (equirect == null)
        {
            skyboxMaterial.SetTexture(CLOUDS_CUBEMAP, defaultCloudsCubemap);
            cloudsConverter?.ReleaseCubemap();
            return;
        }

        ConvertClouds(skyboxMaterial, equirect);
    }

    /// <summary>
    ///     Re-projects a render texture clouds override (live video) into the cloud cubemap; a static override or none is a no-op.
    ///     Independent of <see cref="UpdateSkybox" /> so the layer keeps moving while skybox time is paused or frozen.
    /// </summary>
    public void ProjectLiveClouds()
    {
        if (skyboxMaterial != null && cloudsOverride is RenderTexture liveClouds)
            ConvertClouds(skyboxMaterial, liveClouds);
    }

    /// <summary>
    ///     Calls all the necessary methods to update the skybox and environment
    /// </summary>
    public void UpdateSkybox(float timeOfDay)
    {
        lastTimeOfDay = timeOfDay;

        UpdateIndirectLight(timeOfDay);

        if (skyboxMaterial != null)
            UpdateSkyboxColor(skyboxMaterial, timeOfDay);

        UpdateFog(timeOfDay);

        // we update light in intervals to hide visible artifacts for moving shadows (anti-aliasing related, espescially on the benches)
        if (!Mathf.Approximately(targetTimeOfDay, timeOfDay))
        {
            targetTimeOfDay = timeOfDay;
            StartDirectionalLightTransitionAsync(timeOfDay, transitionDuration).Forget();
        }
    }

    private async UniTaskVoid StartDirectionalLightTransitionAsync(float newTimeOfDay, float duration)
    {
        transitionCancellationTokenSource = transitionCancellationTokenSource.SafeRestart();

        CancellationToken token = transitionCancellationTokenSource.Token;
        float startTime = directionalLightTimeOfDay;
        float startTimeStamp = Time.time;

        while (Time.time - startTimeStamp < duration && !token.IsCancellationRequested)
        {
            float progress = (Time.time - startTimeStamp) / duration;
            float lerpedTimeOfDay = Mathf.Lerp(startTime, newTimeOfDay, progress);

            directionalLightTimeOfDay = lerpedTimeOfDay;
            UpdateDirectionalLight(lerpedTimeOfDay);

            await UniTask.Yield(token);
        }

        directionalLightTimeOfDay = newTimeOfDay;
        UpdateDirectionalLight(newTimeOfDay);
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors from the defined gradients based on
    ///     the normalized time. The sky colors of a scene environment override replace the gradients: zenith drives the sky
    ///     ambient, horizon the equator ambient and nadir the ground ambient.
    /// </summary>
    private void UpdateIndirectLight(float timeOfDay)
    {
        if (!indirectLight) return;

        RenderSettings.ambientSkyColor = Sample(environmentOverride?.Zenith, indirectSkyRamp, timeOfDay);
        RenderSettings.ambientEquatorColor = Sample(environmentOverride?.Horizon, indirectEquatorRamp, timeOfDay);
        RenderSettings.ambientGroundColor = Sample(environmentOverride?.Nadir, groundEquatorRamp, timeOfDay);
    }

    /// <summary>
    ///     Samples the override ramp when the scene environment sets one, otherwise the default gradient.
    /// </summary>
    private static Color Sample(ColorRamp? overrideRamp, Gradient defaultRamp, float timeOfDay) =>
        overrideRamp != null ? overrideRamp.Evaluate(timeOfDay) : defaultRamp.Evaluate(timeOfDay);

    /// <summary>
    ///     Updates the directional light color by sampling the colors from the defined gradient, or from the sun color
    ///     of the scene environment override, plays the corresponding animation frame and writes the sun and moon it
    ///     drives to the material
    /// </summary>
    private void UpdateDirectionalLight(float timeOfDay)
    {
        if (!directionalLight) return;

        //change the color of the light based on the color ramp
        directionalLight.color = Sample(environmentOverride?.SunColor, directionalColorRamp, timeOfDay);

        //sample the right frame of the animation
        if (lightAnimation && lightAnimator != null)
        {
            lightAnimator[lightAnimation.name].time = timeOfDay * lightAnimator[lightAnimation.name].length;
            lightAnimator.Play(lightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        if (skyboxMaterial != null)
        {
            // The animation drives the sun disc size and opacity through the light's scale
            Vector3 directionalLightLocalScale = directionalLight.gameObject.transform.localScale;
            skyboxMaterial.SetFloat(SUN_SIZE, directionalLightLocalScale.x);
            UpdateSunAndMoon(skyboxMaterial, timeOfDay, directionalLightLocalScale.y);
        }

        UpdateLensFlare(timeOfDay);
    }

    /// <summary>
    ///     Writes the sun disc opacity, its radiance and the moon size to the material. A hidden sun zeroes the opacity,
    ///     the radiance and the moon size factor; the moon mask keeps following its curve.
    /// </summary>
    private void UpdateSunAndMoon(Material material, float timeOfDay, float sunOpacity)
    {
        bool visible = sunVisible;

        material.SetFloat(SUN_OPACITY, visible ? sunOpacity : 0f);
        material.SetFloat(SUN_RADIANCE, visible ? sunRadiance.Evaluate(timeOfDay) : 0f);
        material.SetFloat(SUN_RADIANCE_INTENSITY, visible ? sunRadianceIntensity.Evaluate(timeOfDay) : 0f);
        material.SetFloat(MOON_MASK_SIZE, moonMaskSize.Evaluate(timeOfDay));
        material.SetFloat(SECOND_SUN_SIZE_FACTOR, visible ? defaultSecondSunSizeFactor : 0f);
    }

    private void InitializeLensFlare(bool lensFlareEnabled)
    {
        lensFlare = directionalLight.gameObject.GetComponent<LensFlareComponentSRP>()
                    ?? directionalLight.gameObject.AddComponent<LensFlareComponentSRP>();

        lensFlare.useOcclusion = true;
        lensFlare.enabled = lensFlareEnabled;

        Array.Sort(lensFlareEntries, static (a, b) => a.StartTime.CompareTo(b.StartTime));
    }

    /// <summary>
    ///     Drives the flare intensity from its curve, or zero while the scene environment hides the sun, and swaps
    ///     the flare asset of the current time window
    /// </summary>
    private void UpdateLensFlare(float timeOfDay)
    {
        if (lensFlare == null) return;

        lensFlare.intensity = sunVisible ? lensFlareIntensity.Evaluate(timeOfDay) : 0f;

        LensFlareDataSRP? newFlareData = GetActiveLensFlareData(timeOfDay);

        if (newFlareData != activeLensFlareData)
        {
            activeLensFlareData = newFlareData;
            lensFlare.lensFlareData = newFlareData;
        }
    }

    private LensFlareDataSRP? GetActiveLensFlareData(float timeOfDay)
    {
        if (lensFlareEntries.Length == 0)
            return null;

        // Find the last entry whose StartTime is <= current time
        LensFlareDataSRP? result = null;

        for (int i = 0; i < lensFlareEntries.Length; i++)
        {
            if (lensFlareEntries[i].StartTime <= timeOfDay)
                result = lensFlareEntries[i].FlareAsset;
        }

        // If timeOfDay is before the first entry, wrap around to the last
        return result ?? lensFlareEntries[lensFlareEntries.Length - 1].FlareAsset;
    }

    /// <summary>
    ///     Updates the exposed color parameters of the material from the defined gradients, or from the sky, sun, rim
    ///     and cloud colors of the scene environment override. The rim follows an explicit rim override first, then an
    ///     overridden horizon, otherwise its default gradient.
    /// </summary>
    private void UpdateSkyboxColor(Material material, float timeOfDay)
    {
        ColorRamp? rim = environmentOverride?.Rim ?? environmentOverride?.Horizon;

        material.SetColor(ZENIT_COLOR, Sample(environmentOverride?.Zenith, skyZenitColorRamp, timeOfDay));
        material.SetColor(HORIZON_COLOR, Sample(environmentOverride?.Horizon, skyHorizonColorRamp, timeOfDay));
        material.SetColor(NADIR_COLOR, Sample(environmentOverride?.Nadir, skyNadirColorRamp, timeOfDay));
        material.SetColor(SUN_COLOR, Sample(environmentOverride?.SunColor, sunColorRamp, timeOfDay));
        material.SetColor(RIM_COLOR, Sample(rim, rimColorRamp, timeOfDay));
        material.SetColor(CLOUDS_COLOR, Sample(environmentOverride?.CloudsColor, cloudsColorRamp, timeOfDay));
        material.SetFloat(CLOUD_HIGHLIGHTS, cloudsHighlightsIntensity.Evaluate(timeOfDay));
    }

    /// <summary>
    ///     Updates the fog color of the RenderSettings if enabled, from the defined gradient or from the scene environment override
    /// </summary>
    private void UpdateFog(float timeOfDay)
    {
        if (fog)
            RenderSettings.fogColor = Sample(environmentOverride?.FogColor, fogColorRamp, timeOfDay);
    }

    /// <summary>
    ///     Writes the cloud and star constants of the scene environment override to the material, falling back to the
    ///     Genesis defaults. The cloud rotation stays at zero while skybox time is disabled.
    /// </summary>
    private void UpdateCloudsAndStars(Material material)
    {
        material.SetFloat(CLOUD_OPACITY, environmentOverride?.CloudsOpacity ?? defaultCloudsOpacity);
        material.SetFloat(STARS_BRIGHTNESS, environmentOverride?.StarsBrightness ?? defaultStarsBrightness);
        material.SetFloat(CLOUDS_ROTATION_SPEED, skyboxTimeDisabled ? 0f : environmentOverride?.CloudsSpeed ?? defaultCloudsRotationSpeed);
    }

    private void ConvertClouds(Material material, Texture equirect)
    {
        if (TryGetCloudsConverter(out EquirectCubemapConverter? converter))
            material.SetTexture(CLOUDS_CUBEMAP, converter.Convert(equirect));
    }

    /// <summary>
    ///     Creates the converter on first use; when the shader cannot be created the failure is logged once and the
    ///     default clouds are kept.
    /// </summary>
    private bool TryGetCloudsConverter([NotNullWhen(true)] out EquirectCubemapConverter? converter)
    {
        if (cloudsConverter == null && !cloudsConverterUnavailable)
        {
            cloudsConverter = EquirectCubemapConverter.TryCreate();
            cloudsConverterUnavailable = cloudsConverter == null;

            if (cloudsConverterUnavailable)
                ReportHub.LogWarning(ReportCategory.SKYBOX, "Skybox Controller: clouds texture override unavailable, the equirect-to-cube shader could not be created");
        }

        converter = cloudsConverter;
        return converter != null;
    }

    public void DisableSkyboxTime()
    {
        skyboxTimeDisabled = true;

        skyboxMaterial?.SetFloat(CLOUDS_ROTATION_SPEED, 0f);
        skyboxMaterial?.SetFloat(SECOND_SUN_ROTATION_SPEED, 0f);

        // Override shader-side time per-material to disable stars rotation and sky oscillation,
        // which are driven by Unity's _TimeParameters global but have no material speed property.
        skyboxMaterial?.SetVector(TIME_PARAMETERS, Vector4.one);

        // Cancel the pending directional light transition started during Initialize(),
        // which would lerp from float.MinValue and corrupt the light's position.
        transitionCancellationTokenSource?.Cancel();
    }

    [JetBrains.Annotations.UsedImplicitly] // Unity event function
    private void OnDestroy()
    {
        transitionCancellationTokenSource.SafeCancelAndDispose();
        cloudsConverter?.Dispose();
    }

    [JetBrains.Annotations.UsedImplicitly] // Unity event function
    private void OnDisable()
    {
        transitionCancellationTokenSource.SafeCancelAndDispose();
    }

#if UNITY_EDITOR
    [FormerlySerializedAs("editMode")]
    public bool EditMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if (EditMode)
            Initialize(RenderSettings.skybox, null!, null!, null!, 0.5f);
    }
#endif
}
