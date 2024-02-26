using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SkyboxController : MonoBehaviour
{
    private bool pause = false;

    [HideInInspector]
    public int SecondsInDay = 86400;
    public bool PlayOnStart = false;
    public float Speed = 1*60;

    [HideInInspector] public float NaturalTime = 0;
    [HideInInspector] public float NormalizedTime = 0;

    [SerializeField]
    private Material skyboxMaterial;

    [Header("Refresh Time")]
    public float RefreshTime = 5;
    private float sinceLastRefresh = 5;

    [Header("Directional Light")]
    public Light DirectionalLight;
    public AnimationClip LightAnimation;

    [GradientUsage(true)]
    public Gradient DirectionalColorRamp;
    private Animation lightAnimator;

    [Header("Skybox Color")]
    [GradientUsage(true)]
    public Gradient SkyZenitColorRamp;
    [GradientUsage(true)]
    public Gradient SkyHorizonColorRamp;
    [GradientUsage(true)]
    public Gradient SkyNadirColorRamp;

    [InspectorName("Rim Light Color")]
    [GradientUsage(true)]
    public Gradient RimColorRamp;

    [Header("Indirect Lighting")]
    [InspectorName("Enabled")]
    public bool IndirectLight = true;
    [GradientUsage(true)]
    public Gradient IndirectSkyRamp;
    [GradientUsage(true)]
    public Gradient indirectEquatorRamp;
    [GradientUsage(true)]
    public Gradient GroundEquatorRamp;

    [Header("Clouds")]
    [GradientUsage(true)]
    public Gradient CloudsColorRamp;


    [Header("Fog")]
    [InspectorName("Enabled")]
    public bool Fog = true;
    [GradientUsage(true)]
    public Gradient FogColorRamp;

    [Header("UI")]
    public TextMeshProUGUI textUI;
    private bool isInitialized;

#if UNITY_EDITOR

    public bool editMode;

    public void Awake()
    {
        //Added the flag to allow editing of the prefab in a separate scene
        //that doesn't have the regular plugin init flow
        if(editMode)
            Initialize(null, null, null);
    }
#endif

    /// <summary>
    /// Set the material of the skybox and modify the Render Settings
    /// </summary>
    /// <param name="skyboxMaterial"></param>
    public void SetSkyboxMaterial(Material skyboxMaterial)
    {
        this.skyboxMaterial = skyboxMaterial;
        RenderSettings.skybox = this.skyboxMaterial;
    }

    public void Initialize(Material skyboxMat, Light dirLight, AnimationClip skyboxAnimationClip)
    {
        if(skyboxMat != null)
            skyboxMaterial = skyboxMat;

        if(dirLight != null)
            DirectionalLight = dirLight;

        if(skyboxAnimationClip != null)
            LightAnimation = skyboxAnimationClip;

        //setup skybox material
        if(!skyboxMaterial)
        {
            Debug.LogWarning("Skybox Controller: No skybox material assigned");
        }
        else
        {
            //assign skybox to render settings
            RenderSettings.skybox = skyboxMaterial;
        }

        //setup directional light
        if(DirectionalLight == null)
        {
            Debug.LogWarning("Skybox Controller: Directional Light has not been assigned");
        }
        else
        {
            //assign light to render settings
            RenderSettings.sun = DirectionalLight;

            //create animation component in runtime and assign animation clip
            lightAnimator = DirectionalLight.gameObject.AddComponent<Animation>();

            if(LightAnimation == null)
            {
                Debug.LogWarning("Skybox Controller: Directional Light animation has not been assigned");
            }
            else
            {
                lightAnimator.AddClip(LightAnimation,LightAnimation.name);
            }
        }

        //setup indirect light
        if(IndirectLight)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        //setup fog
        if(Fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.001f;
        }

        isInitialized = true;
    }

    void Start()
    {
        SetTime(SecondsInDay/2);
        if(PlayOnStart) { Play();} else {Pause();}
    }

    void Update()
    {
        if (!isInitialized)
            return;

        //update natural and relative time
        if(!pause)
        {
            float deltaTime = (Time.deltaTime * Speed);
            NaturalTime += deltaTime;
            NormalizedTime +=  deltaTime / SecondsInDay;
        }

        if (NaturalTime >= SecondsInDay)
        {
            NaturalTime = NormalizedTime = 0;
        }

        //update skybox only after certain time
        sinceLastRefresh += Time.deltaTime;
        if(sinceLastRefresh >= RefreshTime)
        {
            UpdateSkybox();
            sinceLastRefresh = 0;
        }

        UpdateTimeUI();
    }

    /// <summary>
    /// Auxiliary method for the Inspector to Pause the cycle
    /// </summary>
    public void Pause()
    {
        pause = true;
    }

    /// <summary>
    /// Auxiliary method for the Inspector to Play the cycle
    /// </summary>
    public void Play()
    {
        pause = false;
    }

    /// <summary>
    /// Sets the time of the skybox to an specific second
    /// </summary>
    /// <param name="seconds"></param>
    public void SetTime(float seconds)
    {
        NaturalTime = seconds;
        NormalizedTime = NaturalTime / SecondsInDay;
    }

    /// <summary>
    /// Auxiliary function to returnt the normalized time in HH:MM:SS
    /// </summary>
    public string GetFormatedTime()
    {
        int totalSec = (int)NaturalTime;

        int hours = totalSec / 3600;
        int minutes = (totalSec % 3600) / 60;
        int seconds = totalSec % 60;
        return string.Format("{0:00}:{1:00}:{2:00} - {3}", hours, minutes, seconds, NormalizedTime.ToString("0.000"));
    }

    /// <summary>
    /// Calls all the necessary methods to update the skybox and environment
    /// </summary>
    private void UpdateSkybox()
    {
        UpdateIndirectLight();
        UpdateDirectionaLight();
        UpdateSkyboxColor();
        UpdateFog();
    }

    /// <summary>
    /// Updates the exposed parameters of the material to update gradient colors
    /// and sun size
    /// </summary>
    private void UpdateSkyboxColor()
    {
        Color zenitColor = SkyZenitColorRamp.Evaluate(NormalizedTime);
        Color horizonColor = SkyHorizonColorRamp.Evaluate(NormalizedTime);
        Color nadirColor = SkyNadirColorRamp.Evaluate(NormalizedTime);

        RenderSettings.skybox.SetColor("_ZenitColor", zenitColor);
        RenderSettings.skybox.SetColor("_HorizonColor", horizonColor);
        RenderSettings.skybox.SetColor("_NadirColor", nadirColor);

        Color sunColor = DirectionalColorRamp.Evaluate(NormalizedTime);
        RenderSettings.skybox.SetColor("_SunColor", sunColor);

        Color rimColor = RimColorRamp.Evaluate(NormalizedTime);
        RenderSettings.skybox.SetColor("_RimColor", rimColor);

        Color cloudColor = CloudsColorRamp.Evaluate(NormalizedTime);
        RenderSettings.skybox.SetColor("_CloudsColor", cloudColor);
    }

    /// <summary>
    /// Updates the indirect light of the render settings sampling the colors
    /// from the defined gradients based on the normalized time
    /// </summary>
    private void UpdateIndirectLight()
    {
        if(IndirectLight) //TODO: replace this by delegate / event
        {
            Color skyColor = IndirectSkyRamp.Evaluate(NormalizedTime);
            Color equatorColor = indirectEquatorRamp.Evaluate(NormalizedTime);
            Color groundColor = GroundEquatorRamp.Evaluate(NormalizedTime);

            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor;
        }
    }

    /// <summary>
    /// Updates the directional light color by sampling the colors
    /// from the defined gradient an plays the correspoding animation frame
    /// </summary>
    private void UpdateDirectionaLight()
    {
        //change the color of the light based on the color ramp
        DirectionalLight.color = DirectionalColorRamp.Evaluate(NormalizedTime);

        //sample the right frame of the animation
        if(LightAnimation)
        {
            lightAnimator[LightAnimation.name].time = NormalizedTime * lightAnimator[LightAnimation.name].length;
            lightAnimator.Play(LightAnimation.name);
            lightAnimator.Sample();
            lightAnimator.Stop();
        }
    }

    /// <summary>
    /// Updates the fog color of the RenderSettings if enabled
    /// </summary>
    private void UpdateFog()
    {
        if(Fog) //TODO: replace this by delegate / event
        {
            Color fogColor = FogColorRamp.Evaluate(NormalizedTime);
            RenderSettings.fogColor = fogColor;
        }
    }

    /// <summary>
    /// Auxiliary function to render the time in the UI
    /// </summary>
    private void UpdateTimeUI()
    {
        if(textUI)
        {
            textUI.text = GetFormatedTime();
        }
    }
}
