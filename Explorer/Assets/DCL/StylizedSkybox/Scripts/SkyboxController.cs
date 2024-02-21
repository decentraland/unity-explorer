using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

public class SkyboxController : MonoBehaviour
{
    private bool pause = false;

    [HideInInspector] 
    public int SecondsInDay = 86400;
    public bool playOnStart = false;
    public float speed = 1*60;

    [HideInInspector] 
    public float _naturalTime = 0;
    private float _normalizedTime;

    public Material skyboxMaterial;

    [Header("Refresh Time")]
    public float refreshTime = 5;
    private float _sinceLastRefresh = 5;

    [Header("Directional Light")]
    public Light directionalLight;
    public AnimationClip lightAnimation;
    [GradientUsage(true)] 
    public Gradient directionalColorRamp;
    private Animation _lightAnimator;

    [Header("Skybox Color")]
    [GradientUsage(true)] 
    public Gradient skyZenitColor;
    [GradientUsage(true)] 
    public Gradient skyHorizonColor;
    [GradientUsage(true)] 
    public Gradient skyNadirColor;

    [InspectorName("Rim Light Color")]
    [GradientUsage(true)] 
    public Gradient rimColorRamp;

    [Header("Indirect Lighting")]
    [InspectorName("Enabled")]
    public bool indirectLight = true;
    [GradientUsage(true)] 
    public Gradient indirectSkyRamp;
    [GradientUsage(true)] 
    public Gradient indirectEquatorRamp;
    [GradientUsage(true)] 
    public Gradient groundEquatorRamp;

    [Header("Clouds")]
    [GradientUsage(true)] 
    public Gradient cloudsColorRamp;


    [Header("Fog")]
    [InspectorName("Enabled")]
    public bool fog = true;
    [GradientUsage(true)] 
    public Gradient fogColorRamp;

    [Header("UI")]
    public TextMeshProUGUI textUI;   

    void Awake()
    {
        //setup skybox material
        if(skyboxMaterial)
        {
            RenderSettings.skybox = skyboxMaterial;
        }

        //setup directional light
        if(directionalLight == null)
        {
            Debug.LogWarning("Skybox Controller: Directional Light has not been assigned");
        }
        else
        {
            //create animation component in runtime and assign animation clip
            _lightAnimator = directionalLight.gameObject.AddComponent<Animation>();

            if(lightAnimation == null)
            {
                Debug.LogWarning("Skybox Controller: Directional Light animation has not been assigned");
            }
            else
            {
                _lightAnimator.AddClip(lightAnimation,lightAnimation.name);
            }
        }

        //setup indirect light
        if(indirectLight)
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        }

        //setup fog
        if(fog)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Exponential;
            RenderSettings.fogDensity = 0.001f;
        } 
    }

    void Start()
    {
        SetTime(SecondsInDay/2);
        if(playOnStart) { Play();} else {Pause();}
    }

    void Update()
    {
        //update natural and relative time
        if(!pause)
        {
            float deltaTime = (Time.deltaTime * speed);
            _naturalTime += deltaTime;
            _normalizedTime +=  deltaTime / SecondsInDay;
        }

        if (_naturalTime >= SecondsInDay)
        {
            _naturalTime = _normalizedTime = 0;
        }

        //update skybox only after certain time
        _sinceLastRefresh += Time.deltaTime;
        if(_sinceLastRefresh >= refreshTime)
        {
            UpdateSkybox();
            _sinceLastRefresh = 0;
        }

        UpdateTimeUI();
    }
    public void UpdateSkybox()
    {
        UpdateIndirectLight();
        UpdateDirectionaLight();
        UpdateSkyboxColor();
        UpdateFog();
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
    /// Auxiliary function for the Inspector to set an specific timer
    /// </summary>
    /// <param name="seconds"></param>
    public void SetTime(float seconds)
    {
        _naturalTime = seconds;
        _normalizedTime = _naturalTime / SecondsInDay;
    }

    /// <summary>
    /// Auxiliary function to render the time in the UI
    /// </summary>
    void UpdateTimeUI()
    {
        if(textUI)
        {
            textUI.text = GetFormatedTime();
        }
    }

    /// <summary>
    /// Auxiliary function to returnt the normalized time in HH:MM:SS
    /// </summary>
    public string GetFormatedTime()
    {
        int totalSec = (int)_naturalTime;

        int hours = totalSec / 3600;
        int minutes = (totalSec % 3600) / 60;
        int seconds = totalSec % 60;
        return string.Format("{0:00}:{1:00}:{2:00} - {3}", hours, minutes, seconds, _normalizedTime.ToString("0.000"));
    }

    /// <summary>
    /// Updates the exposed parameters of the material to update gradient colors
    /// and sun size
    /// </summary>
    public void UpdateSkyboxColor()
    {
        Color zenitColor = skyZenitColor.Evaluate(_normalizedTime);
        Color horizonColor = skyHorizonColor.Evaluate(_normalizedTime);
        Color nadirColor = skyNadirColor.Evaluate(_normalizedTime);

        RenderSettings.skybox.SetColor("_ZenitColor", zenitColor);
        RenderSettings.skybox.SetColor("_HorizonColor", horizonColor);
        RenderSettings.skybox.SetColor("_NadirColor", nadirColor);

        Color sunColor = directionalColorRamp.Evaluate(_normalizedTime);
        RenderSettings.skybox.SetColor("_SunColor", sunColor);

        Color rimColor = rimColorRamp.Evaluate(_normalizedTime);
        RenderSettings.skybox.SetColor("_RimColor", rimColor);

        Color cloudColor = cloudsColorRamp.Evaluate(_normalizedTime);
        RenderSettings.skybox.SetColor("_CloudsColor", cloudColor);
    }

    /// <summary>
    /// Updates the indirect light of the render settings sampling the colors
    /// from the defined gradients based on the normalized time
    /// </summary>
    void UpdateIndirectLight()
    {
        if(indirectLight) //TODO: replace this by delegate / event
        {
            Color skyColor = indirectSkyRamp.Evaluate(_normalizedTime);
            Color equatorColor = indirectEquatorRamp.Evaluate(_normalizedTime);
            Color groundColor = groundEquatorRamp.Evaluate(_normalizedTime);

            RenderSettings.ambientSkyColor = skyColor;
            RenderSettings.ambientEquatorColor = equatorColor;
            RenderSettings.ambientGroundColor = groundColor;
        }
    }

    /// <summary>
    /// Updates the directional light color by sampling the colors
    /// from the defined gradient an plays the correspoding animation frame
    /// </summary>
    void UpdateDirectionaLight()
    {
        //change the color of the light based on the color ramp
        directionalLight.color = directionalColorRamp.Evaluate(_normalizedTime);
        
        //sample the right frame of the animation
        if(lightAnimation)
        {
            _lightAnimator[lightAnimation.name].time = _normalizedTime * _lightAnimator[lightAnimation.name].length;
            _lightAnimator.Play(lightAnimation.name);
            _lightAnimator.Sample();
            _lightAnimator.Stop();
        }
    }

    public void UpdateFog()
    {
        if(fog) //TODO: replace this by delegate / event
        {
            Color fogColor = fogColorRamp.Evaluate(_normalizedTime);
            RenderSettings.fogColor = fogColor;
        }
    }
}
