using DCL.Diagnostics;
using UnityEngine;
using TMPro;
using UnityEngine.Rendering;

public class SkyboxController : MonoBehaviour
{
    [HideInInspector]
    public int SecondsInDay = 86400;
    public bool PlayOnStart;
    public bool StopRefresh = false;
    public float Speed = 1 * 60;

    [HideInInspector] public float NaturalTime;
    [HideInInspector] public float NormalizedTime;
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
    private bool pause;
    private float sinceLastRefresh = 5;

    private void Start()
    {
        SetTime(SecondsInDay / 2);

        if (PlayOnStart) { Play(); }
        else { Pause(); }
    }

    private void Update()
    {
        if (!isInitialized)
            return;

        //update natural and relative time
        if (!pause)
        {
            float deltaTime = Time.deltaTime * Speed;
            NaturalTime += deltaTime;
            NormalizedTime += deltaTime / SecondsInDay;
        }

        //loops time at the end of the cycle
        if (NaturalTime >= SecondsInDay) { NaturalTime = NormalizedTime = 0; }

        //update skybox only after certain time
        sinceLastRefresh += Time.deltaTime;

        if (sinceLastRefresh >= RefreshTime)
        {
            UpdateSkybox();
            sinceLastRefresh = 0;
        }

        UpdateTimeUI();
    }

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip)
    {
        if (skyboxMat != null)
            skyboxMaterial = skyboxMat;

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
        if (Fog)
        {
            RenderSettings.fog = true;
        }

        isInitialized = true;
    }

    /// <summary>
    ///     Auxiliary method for the Inspector to Pause the cycle
    /// </summary>
    public void Pause()
    {
        pause = true;
    }

    /// <summary>
    ///     Auxiliary method for the Inspector to Play the cycle
    /// </summary>
    public void Play()
    {
        pause = false;
    }

    /// <summary>
    ///     Set the material of the skybox and modify the Render Settings
    /// </summary>
    /// <param name="skyboxMaterial"></param>
    public void SetSkyboxMaterial(Material skyboxMaterial)
    {
        this.skyboxMaterial = skyboxMaterial;
        RenderSettings.skybox = this.skyboxMaterial;
    }

    /// <summary>
    ///     Sets the time of the skybox to an specific second
    /// </summary>
    /// <param name="seconds"></param>
    public void SetTime(float seconds)
    {
        NaturalTime = seconds;
        NormalizedTime = NaturalTime / SecondsInDay;
    }

    /// <summary>
    ///     Sets the time of the skybox to an specific second
    /// </summary>
    /// <param name="seconds"></param>
    public void SetTimeNormalized(float normalizedTime)
    {
        NormalizedTime = normalizedTime;
        NaturalTime = normalizedTime * SecondsInDay;
    }

    /// <summary>
    ///     Auxiliary function to returnt the normalized time in HH:MM:SS
    /// </summary>
    public string GetFormatedTime()
    {
        var totalSec = (int)NaturalTime;

        int hours = totalSec / 3600;
        int minutes = totalSec % 3600 / 60;
        int seconds = totalSec % 60;
        return string.Format("{0:00}:{1:00}:{2:00} - {3}", hours, minutes, seconds, NormalizedTime.ToString("0.000"));
    }

    /// <summary>
    ///     Calls all the necessary methods to update the skybox and environment
    /// </summary>
    private void UpdateSkybox()
    {
        if(!StopRefresh)
        {
            UpdateIndirectLight();
            UpdateDirectionaLight();
            UpdateSkyboxColor();
            UpdateFog();
        }
    }

    /// <summary>
    ///     Updates the exposed parameters of the material to update gradient colors
    ///     and sun size
    /// </summary>
    private void UpdateSkyboxColor()
    {
        RenderSettings.skybox.SetColor("_ZenitColor", SkyZenitColorRamp.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetColor("_HorizonColor", SkyHorizonColorRamp.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetColor("_NadirColor", SkyNadirColorRamp.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetColor("_SunColor", SunColorRamp.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetColor("_RimColor", RimColorRamp.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetColor("_CloudsColor", CloudsColorRamp.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetFloat("_Cloud_Highlights", CloudsHighlightsIntensity.Evaluate(NormalizedTime));
    }

    /// <summary>
    ///     Updates the indirect light of the render settings sampling the colors
    ///     from the defined gradients based on the normalized time
    /// </summary>
    private void UpdateIndirectLight()
    {
        if (IndirectLight)
        {
            RenderSettings.ambientSkyColor = IndirectSkyRamp.Evaluate(NormalizedTime);
            RenderSettings.ambientEquatorColor = IndirectEquatorRamp.Evaluate(NormalizedTime);
            RenderSettings.ambientGroundColor = GroundEquatorRamp.Evaluate(NormalizedTime);
        }
    }

    /// <summary>
    ///     Updates the directional light color by sampling the colors
    ///     from the defined gradient an plays the correspoding animation frame
    /// </summary>
    private void UpdateDirectionaLight()
    {
        if (!DirectionalLight) return;

        //change the color of the light based on the color ramp
        DirectionalLight.color = DirectionalColorRamp.Evaluate(NormalizedTime);

        //sample the right frame of the animation
        if (LightAnimation)
        {
            lightAnimator[LightAnimation.name].time = NormalizedTime * lightAnimator[LightAnimation.name].length;
            lightAnimator.Play(LightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }

        RenderSettings.skybox.SetFloat("_SunSize", DirectionalLight.gameObject.transform.localScale.x);
        RenderSettings.skybox.SetFloat("_SunOpacity", DirectionalLight.gameObject.transform.localScale.y);

        //sampling sun randiance and intensity curves
        RenderSettings.skybox.SetFloat("_Sun_Radiance", sunRadiance.Evaluate(NormalizedTime));
        RenderSettings.skybox.SetFloat("_Sun_Radiance_Intensity", sunRadianceIntensity.Evaluate(NormalizedTime));

        //change size of moon mask
        RenderSettings.skybox.SetFloat("_Moon_Mask_Size", moonMaskSize.Evaluate(NormalizedTime));
    }

    /// <summary>
    ///     Updates the fog color of the RenderSettings if enabled
    /// </summary>
    private void UpdateFog()
    {
        if (Fog)
        {
            RenderSettings.fogColor = FogColorRamp.Evaluate(NormalizedTime);
        }
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
            Initialize(null, null, null);
    }
#endif
}
