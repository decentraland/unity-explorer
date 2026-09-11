using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Everything that defines how the sky looks over a day: the colour ramps and curves that
    ///     <see cref="SkyboxRenderController" /> evaluates every frame, plus the material values and textures it
    ///     applies once when the preset is selected. Read-only at runtime; switching looks means switching presets.
    /// </summary>
    [CreateAssetMenu(fileName = "SkyboxLookPreset", menuName = "DCL/Skybox/Look Preset")]
    public class SkyboxLookPreset : ScriptableObject
    {
        [Serializable]
        public class LensFlareTimeEntry
        {
            [Range(0f, 1f)]
            public float StartTime;
            public LensFlareDataSRP FlareAsset = null!;
        }

        [Header("Timing")]
        [Tooltip("Maps normalized time of day (0 = midnight, 0.5 = noon) to the phase every colour ramp is keyed on. "
                 + "Identity keeps ramps on time. Plateaus hold a palette; steep parts shorten a transition. "
                 + "Sun position and disc curves always follow time, never phase.")]
        [SerializeField] private AnimationCurve timeToPhase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Directional Light")]
        [GradientUsage(true)] [SerializeField] private Gradient directionalColorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient sunColorRamp = new ();
        [SerializeField] private AnimationCurve sunRadiance = new ();
        [SerializeField] private AnimationCurve sunRadianceIntensity = new ();
        [SerializeField] private AnimationCurve moonMaskSize = new ();

        [Header("Lens Flare")]
        [SerializeField] private AnimationCurve lensFlareIntensity = new ();
        [SerializeField] private List<LensFlareTimeEntry> lensFlareEntries = new ();

        [Header("Skybox Color")]
        [GradientUsage(true)] [SerializeField] private Gradient skyZenitColorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skyHorizonColorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skyNadirColorRamp = new ();

        [InspectorName("Rim Light Color")]
        [GradientUsage(true)] [SerializeField] private Gradient rimColorRamp = new ();

        [Header("Indirect Lighting")]
        [InspectorName("Enabled")] [SerializeField] private bool indirectLight = true;
        [GradientUsage(true)] [SerializeField] private Gradient indirectSkyRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient indirectEquatorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient groundEquatorRamp = new ();

        [Header("Clouds")]
        [GradientUsage(true)] [SerializeField] private Gradient cloudsColorRamp = new ();
        [SerializeField] private AnimationCurve cloudsHighlightsIntensity = new ();

        [Header("Fog")]
        [InspectorName("Enabled")] [SerializeField] private bool fog = true;
        [GradientUsage(true)] [SerializeField] private Gradient fogColorRamp = new ();

        [Header("Material (applied once per preset)")]
        [SerializeField] private float zenitSpread = 0.404f;
        [SerializeField] private float zenitBlend = 0.214f;
        [SerializeField] private float groundSpread = 0.2f;
        [SerializeField] private float groundBlend = 0.436f;
        [SerializeField] private float blendTwist;
        [SerializeField] private float rimSpread = 0.362f;
        [SerializeField] private float rimOpacity = 0.94f;
        [SerializeField] private float starsBrightness = 4.62f;
        [SerializeField] private Texture2D? starsTexture;
        [SerializeField] private Cubemap? cloudsCubemap;
        [SerializeField] private float cloudOpacity = 1f;
        [SerializeField] private float cloudsRotationSpeed = 0.01f;
        [SerializeField] private Vector2 moonMaskPosition = new (0.01f, -0.01f);
        [SerializeField] private float secondSunSizeFactor = 0.1f;
        [SerializeField] private float secondSunRotationSpeed = 0.1f;
        [SerializeField] private float secondSunOrbitSize = 0.1f;

        public AnimationCurve TimeToPhase => timeToPhase;

        public Gradient DirectionalColorRamp => directionalColorRamp;
        public Gradient SunColorRamp => sunColorRamp;
        public AnimationCurve SunRadiance => sunRadiance;
        public AnimationCurve SunRadianceIntensity => sunRadianceIntensity;
        public AnimationCurve MoonMaskSize => moonMaskSize;

        public AnimationCurve LensFlareIntensity => lensFlareIntensity;
        public IReadOnlyList<LensFlareTimeEntry> LensFlareEntries => lensFlareEntries;

        public Gradient SkyZenitColorRamp => skyZenitColorRamp;
        public Gradient SkyHorizonColorRamp => skyHorizonColorRamp;
        public Gradient SkyNadirColorRamp => skyNadirColorRamp;
        public Gradient RimColorRamp => rimColorRamp;

        public bool IndirectLight => indirectLight;
        public Gradient IndirectSkyRamp => indirectSkyRamp;
        public Gradient IndirectEquatorRamp => indirectEquatorRamp;
        public Gradient GroundEquatorRamp => groundEquatorRamp;

        public Gradient CloudsColorRamp => cloudsColorRamp;
        public AnimationCurve CloudsHighlightsIntensity => cloudsHighlightsIntensity;

        public bool Fog => fog;
        public Gradient FogColorRamp => fogColorRamp;

        public float ZenitSpread => zenitSpread;
        public float ZenitBlend => zenitBlend;
        public float GroundSpread => groundSpread;
        public float GroundBlend => groundBlend;
        public float BlendTwist => blendTwist;
        public float RimSpread => rimSpread;
        public float RimOpacity => rimOpacity;
        public float StarsBrightness => starsBrightness;
        public Texture2D? StarsTexture => starsTexture;
        public Cubemap? CloudsCubemap => cloudsCubemap;
        public float CloudOpacity => cloudOpacity;
        public float CloudsRotationSpeed => cloudsRotationSpeed;
        public Vector2 MoonMaskPosition => moonMaskPosition;
        public float SecondSunSizeFactor => secondSunSizeFactor;
        public float SecondSunRotationSpeed => secondSunRotationSpeed;
        public float SecondSunOrbitSize => secondSunOrbitSize;

        /// <summary>
        ///     Phase the colour ramps are sampled at for the given normalized time of day. A curve without keys
        ///     evaluates to zero in Unity, which would pin the palette to midnight, so it falls back to identity.
        /// </summary>
        public float EvaluatePhase(float timeOfDay) =>
            timeToPhase.length == 0 ? timeOfDay : Mathf.Clamp01(timeToPhase.Evaluate(timeOfDay));
    }
}
