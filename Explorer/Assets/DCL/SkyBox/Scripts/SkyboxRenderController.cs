using DCL.Diagnostics;
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

    [SerializeField] private Material skyboxMaterial;

    [Header("Directional Light")]
    [SerializeField] private Light directionalLight;
    [SerializeField] private AnimationClip lightAnimation;
    private Animation lightAnimator;

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

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip, float initialTimeOfDay)
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
        }

        //setup indirect light
        if (indirectLight)
            RenderSettings.ambientMode = AmbientMode.Trilight;

        //setup fog
        if (fog)
            RenderSettings.fog = true;

        UpdateSkybox(initialTimeOfDay);
    }

    /// <summary>
    ///     Calls all the necessary methods to update the skybox and environment
    /// </summary>
    public void UpdateSkybox(float timeOfDay)
    {
        UpdateIndirectLight(timeOfDay);
        UpdateDirectionalLight(timeOfDay);
        UpdateSkyboxColor(timeOfDay);
        UpdateFog(timeOfDay);
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors
    ///     from the defined gradients based on the normalized time
    /// </summary>
    private void UpdateIndirectLight(float timeOfDay)
    {
        if (!indirectLight) return;

        RenderSettings.ambientSkyColor = indirectSkyRamp.Evaluate(timeOfDay);
        RenderSettings.ambientEquatorColor = indirectEquatorRamp.Evaluate(timeOfDay);
        RenderSettings.ambientGroundColor = groundEquatorRamp.Evaluate(timeOfDay);
    }

    /// <summary>
    ///     Updates the directional light color by sampling the colors
    ///     from the defined gradient and plays the corresponding animation frame
    /// </summary>
    private void UpdateDirectionalLight(float timeOfDay)
    {
        if (!directionalLight) return;

        //change the color of the light based on the color ramp
        directionalLight.color = directionalColorRamp.Evaluate(timeOfDay);

        //sample the right frame of the animation
        if (lightAnimation)
        {
            lightAnimator[lightAnimation.name].time = timeOfDay * lightAnimator[lightAnimation.name].length;
            lightAnimator.Play(lightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        var directionalLightLocalScale = directionalLight.gameObject.transform.localScale;
        RenderSettings.skybox.SetFloat(SUN_SIZE, directionalLightLocalScale.x);
        RenderSettings.skybox.SetFloat(SUN_OPACITY, directionalLightLocalScale.y);

        //sampling sun radiance and intensity curves
        RenderSettings.skybox.SetFloat(SUN_RADIANCE, sunRadiance.Evaluate(timeOfDay));
        RenderSettings.skybox.SetFloat(SUN_RADIANCE_INTENSITY, sunRadianceIntensity.Evaluate(timeOfDay));

        //change size of moon mask
        RenderSettings.skybox.SetFloat(MOON_MASK_SIZE, moonMaskSize.Evaluate(timeOfDay));
    }

    /// <summary>
    ///     Updates the exposed parameters of the material to update gradient colors
    ///     and sun size
    /// </summary>
    private void UpdateSkyboxColor(float timeOfDay)
    {
        RenderSettings.skybox.SetColor(ZENIT_COLOR, skyZenitColorRamp.Evaluate(timeOfDay));
        RenderSettings.skybox.SetColor(HORIZON_COLOR, skyHorizonColorRamp.Evaluate(timeOfDay));
        RenderSettings.skybox.SetColor(NADIR_COLOR, skyNadirColorRamp.Evaluate(timeOfDay));
        RenderSettings.skybox.SetColor(SUN_COLOR, sunColorRamp.Evaluate(timeOfDay));
        RenderSettings.skybox.SetColor(RIM_COLOR, rimColorRamp.Evaluate(timeOfDay));
        RenderSettings.skybox.SetColor(CLOUDS_COLOR, cloudsColorRamp.Evaluate(timeOfDay));
        RenderSettings.skybox.SetFloat(CLOUD_HIGHLIGHTS, cloudsHighlightsIntensity.Evaluate(timeOfDay));
    }

    /// <summary>
    ///     Updates the fog color of the RenderSettings if enabled
    /// </summary>
    private void UpdateFog(float timeOfDay)
    {
        if (fog)
            RenderSettings.fogColor = fogColorRamp.Evaluate(timeOfDay);
    }

#if UNITY_EDITOR
    public bool editMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if (editMode)
            Initialize(null, null, null, 0.5f);
    }
#endif
}
