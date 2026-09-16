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

        [Serializable]
        public class CloudLayer
        {
            public Texture2D? Strip;
            [Tooltip("Unreal MF_CloudLayer semantics: fraction of the dome's height the strip covers. 0.4 = the strip's cloud band spans roughly a 25 degree slice of elevation; larger = taller clouds.")]
            public float StretchV = 0.4f;
            [Tooltip("Unreal MF_CloudLayer semantics: added to the dome V before scaling. Negative moves the layer up, positive down.")]
            public float OffsetV;
            [Tooltip("How many times the strip repeats around the horizon. 2 = clouds half the size.")]
            [Range(1f, 4f)] public float TilingU = 1f;
            [Tooltip("Rotates the strip around the horizon, as a fraction of a full turn. Use different values per layer so clouds do not stack.")]
            [Range(0f, 1f)] public float OffsetU;
            public float Speed = 1f;
            public float Strength = 1.2f;
            [Range(0f, 1f)] public float Opacity = 1f;
            [Tooltip("Visibility per phase anchor: Night, Sunrise, Day, Sunset. 1 = shown, 0 = dissolved.")]
            public Vector4 FlowByPhase = Vector4.one;
        }

        [Header("Timing")]
        [Tooltip("Maps normalized time of day (0 = midnight, 0.5 = noon) to the phase every colour ramp is keyed on. "
                 + "Identity keeps ramps on time. Plateaus hold a palette; steep parts shorten a transition. "
                 + "Sun position and disc curves always follow time, never phase.")]
        [SerializeField] private AnimationCurve timeToPhase = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Sun mechanics (keyed on time of day, not phase)")]
        [Tooltip("Directional light intensity over the day. Empty = keep the light animation clip's intensity channel.")]
        [SerializeField] private AnimationCurve lightIntensity = new ();
        [Tooltip("Sun disc size over the day. Empty = keep the clip's localScale.x channel.")]
        [SerializeField] private AnimationCurve sunSize = new ();
        [Tooltip("Sun disc opacity over the day. Empty = keep the clip's localScale.y channel.")]
        [SerializeField] private AnimationCurve sunOpacity = new ();

        [Header("Directional Light")]
        [GradientUsage(true)] [SerializeField] private Gradient directionalColorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient sunColorRamp = new ();
        [SerializeField] private AnimationCurve sunRadiance = new ();
        [SerializeField] private AnimationCurve sunRadianceIntensity = new ();
        [SerializeField] private AnimationCurve moonMaskSize = new ();

        [Header("Lens Flare")]
        [SerializeField] private AnimationCurve lensFlareIntensity = new ();
        [SerializeField] private List<LensFlareTimeEntry> lensFlareEntries = new ();

        [Header("Celestial path (computed sun and moon rotation)")]
        [Tooltip("Places the directional light on a computed sun arc by day and a separate moon arc by night instead of sampling the rotation clip. Sun Opacity and Moon Mask Size curves are ignored: the disc hides itself while the light crosses from one body to the other.")]
        [SerializeField] private bool computeCelestialPath;
        [Tooltip("Normalized time the sun crosses the horizon on its way up.")]
        [Range(0f, 1f)] [SerializeField] private float sunriseTime = 0.267f;
        [Tooltip("Normalized time the sun crosses the horizon on its way down.")]
        [Range(0f, 1f)] [SerializeField] private float sunsetTime = 0.784f;
        [Tooltip("Compass direction of the sunrise point in degrees, 0 = +Z, 90 = +X.")]
        [Range(0f, 360f)] [SerializeField] private float sunPathAzimuth = 225f;
        [Tooltip("Lean of the sun's arc away from the zenith in degrees. 0 passes overhead; positive leans to the right when facing the sunrise point.")]
        [Range(-80f, 80f)] [SerializeField] private float sunPathTilt;
        [Range(0f, 1f)] [SerializeField] private float moonriseTime = 0.896f;
        [Range(0f, 1f)] [SerializeField] private float moonsetTime = 0.167f;
        [Range(0f, 360f)] [SerializeField] private float moonPathAzimuth = 225f;
        [Tooltip("Lean of the moon's arc; 40 keeps the moon low for long night shadows.")]
        [Range(-80f, 80f)] [SerializeField] private float moonPathTilt = 40f;
        [Tooltip("Normalized duration of the crossover from sun to moon (ending at moonrise) and back (starting at moonset). The disc is hidden and the light dims while it happens.")]
        [Range(0.01f, 0.15f)] [SerializeField] private float celestialSwapDuration = 0.042f;
        [Tooltip("Crescent mask size while the moon is the active body.")]
        [Range(0f, 0.5f)] [SerializeField] private float computedMoonMaskSize = 0.16f;
        [Tooltip("Disc size while the moon is the active body, so the crescent keeps one shape all night instead of following the Sun Size curve.")]
        [Range(0.01f, 0.5f)] [SerializeField] private float computedMoonDiscSize = 0.12f;
        [Tooltip("Where the crescent hole sits relative to the moon, in its own frame (x = right, y = up, radians), constant all night. The legacy Moon Mask Position is a world-space nudge and changes shape as the moon moves.")]
        [SerializeField] private Vector2 computedMoonMaskOffset = new (-0.009f, -0.007f);
        [Tooltip("Moon disc colour over its own rise (0) to set (1) progress.")]
        [GradientUsage(true)] [SerializeField] private Gradient moonColorRamp = new ();

        [Header("Celestial disc")]
        [Tooltip("Dims the disc and halo as the active body nears the horizon, like the Unreal celestial quad.")]
        [SerializeField] private bool discHorizonDarkening;
        [Tooltip("Sine of the elevation at which the disc is back to full brightness.")]
        [Range(0.01f, 0.5f)] [SerializeField] private float discHorizonDarkeningHeight = 0.2f;
        [Tooltip("Brightness multiplier at and below the horizon.")]
        [Range(0f, 1f)] [SerializeField] private float discHorizonDarkeningFloor = 0.1f;

        [Header("Skybox Color")]
        [GradientUsage(true)] [SerializeField] private Gradient skyZenitColorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skyHorizonColorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skyNadirColorRamp = new ();

        [InspectorName("Rim Light Color")]
        [GradientUsage(true)] [SerializeField] private Gradient rimColorRamp = new ();

        [Header("Sky Lookup (phase x elevation)")]
        [Tooltip("Replaces the zenith/horizon/nadir bands with a per-phase colour-over-elevation lookup. "
                 + "The rim (horizon band) is disabled while this is on because the band is authored inside these gradients.")]
        [SerializeField] private bool useSkyLut;
        [Tooltip("Colour over elevation, 0 = horizon, 1 = zenith. One gradient per phase anchor.")]
        [GradientUsage(true)] [SerializeField] private Gradient skyNight = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skySunrise = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skyDay = new ();
        [GradientUsage(true)] [SerializeField] private Gradient skySunset = new ();
        [Tooltip("Baked from the four gradients with the \"Bake sky LUT\" button. Rows: Night, Sunrise, Day, Sunset, Night.")]
        [SerializeField] private Texture2D? skyLut;
        [Tooltip("Tiling noise that perturbs the horizon band into distant-cloud silhouettes (lookup only). None = off.")]
        [SerializeField] private Texture2D? skyHorizonNoise;
        [Tooltip("Elevation offset at full noise (Unreal: Strength x 0.1). 0 disables.")]
        [Range(0f, 0.5f)] [SerializeField] private float skyHorizonNoiseStrength = 0.1f;
        [Tooltip("Noise repeats around the horizon (x) and over the dome height (y).")]
        [SerializeField] private Vector2 skyHorizonNoiseTiling = new (5f, 10f);
        [SerializeField] private float skyHorizonNoiseSpeed = 0.002f;

        [Header("Stars v2 (drawn by the sky lookup)")]
        [Tooltip("Draws a procedural star field inside the sky lookup with per-phase brightness, twinkle and shooting stars. Set the legacy Stars Brightness to 0 when enabling this. The dim patches sample the Sky Horizon Noise texture.")]
        [SerializeField] private bool useStarsV2;
        [SerializeField] private float starsV2Brightness = 2f;
        [Tooltip("Star cells per cube face side; the sky holds roughly half that squared times six stars.")]
        [Range(4f, 64f)] [SerializeField] private float starsDensity = 22f;
        [Tooltip("Star radius in degrees.")]
        [Range(0.02f, 0.5f)] [SerializeField] private float starsSize = 0.1f;
        [Tooltip("Brightness multiplier at each phase anchor: Night, Sunrise, Day, Sunset.")]
        [SerializeField] private Vector4 starsBrightnessByPhase = new (1f, 0.1f, 0f, 0.5f);
        [SerializeField] private float starsRotationSpeed = 0.002f;
        [Range(0f, 1f)] [SerializeField] private float starsTwinkle = 0.6f;
        [SerializeField] private float starsTwinkleSpeed = 0.15f;
        [Tooltip("How much the slow drifting noise dims regions of the field. 0 = uniform.")]
        [Range(0f, 1f)] [SerializeField] private float starsPatchStrength = 0.7f;
        [Tooltip("Stars fade in between these sines of elevation, hiding them in the horizon band.")]
        [SerializeField] private Vector2 starsHorizonFade = new (0.02f, 0.15f);
        [Tooltip("Shooting-star frequency, scaled by the star brightness of the phase. 0 = off.")]
        [Range(0f, 1f)] [SerializeField] private float shootingStarsRate = 0.4f;

        [Header("Indirect Lighting")]
        [InspectorName("Enabled")] [SerializeField] private bool indirectLight = true;
        [GradientUsage(true)] [SerializeField] private Gradient indirectSkyRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient indirectEquatorRamp = new ();
        [GradientUsage(true)] [SerializeField] private Gradient groundEquatorRamp = new ();

        [Header("Clouds")]
        [GradientUsage(true)] [SerializeField] private Gradient cloudsColorRamp = new ();
        [SerializeField] private AnimationCurve cloudsHighlightsIntensity = new ();

        [Header("Clouds v2 (layered strips)")]
        [Tooltip("Replaces the cloud cubemap with up to three packed strips (R shading, G backlit look, B growth order, A mask).")]
        [SerializeField] private bool useCloudsV2;
        [Tooltip("Colour clouds through the shadow-to-lit ramp instead of a flat tint.")]
        [SerializeField] private bool cloudsV2Ramp = true;
        [Tooltip("Darken bodies and light rims when the sun or moon is behind a cloud.")]
        [SerializeField] private bool cloudsV2Backlight = true;
        [Tooltip("Grow clouds in from their base and dissolve them over time.")]
        [SerializeField] private bool cloudsV2Flow = true;
        [Tooltip("Up to three layers, first = highest / farthest.")]
        [SerializeField] private CloudLayer[] cloudLayers = Array.Empty<CloudLayer>();
        [Tooltip("Cloud shadow colour per phase. The lit colour is the Clouds colour ramp above.")]
        [GradientUsage(true)] [SerializeField] private Gradient cloudsShadowColorRamp = new ();
        [Range(0.02f, 0.9f)] [SerializeField] private float cloudsRampKnee = 0.15f;
        [SerializeField] private float cloudsHighlightStrength = 2f;
        [Range(0f, 0.99f)] [SerializeField] private float cloudsHighlightThreshold = 0.6f;
        [SerializeField] private float cloudsHighlightFalloff = 3f;
        [Tooltip("Elevation (0 horizon .. 1 zenith) where clouds start fading out, so the strip never pinches at the zenith.")]
        [Range(0f, 1f)] [SerializeField] private float cloudsZenithFadeStart = 0.55f;
        [Range(0f, 1f)] [SerializeField] private float cloudsZenithFadeEnd = 0.8f;
        [Tooltip("Cloud alpha range over which clouds start / fully hide the sun. Starting above 0 avoids a dark ring at soft edges.")]
        [Range(0f, 1f)] [SerializeField] private float cloudsOcclusionStart = 0.35f;
        [Range(0f, 1f)] [SerializeField] private float cloudsOcclusionEnd = 0.9f;

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

        public AnimationCurve LightIntensity => lightIntensity;
        public AnimationCurve SunSize => sunSize;
        public AnimationCurve SunOpacity => sunOpacity;

        public Gradient DirectionalColorRamp => directionalColorRamp;
        public Gradient SunColorRamp => sunColorRamp;
        public AnimationCurve SunRadiance => sunRadiance;
        public AnimationCurve SunRadianceIntensity => sunRadianceIntensity;
        public AnimationCurve MoonMaskSize => moonMaskSize;

        public AnimationCurve LensFlareIntensity => lensFlareIntensity;
        public IReadOnlyList<LensFlareTimeEntry> LensFlareEntries => lensFlareEntries;

        public bool ComputeCelestialPath => computeCelestialPath;
        public float SunriseTime => sunriseTime;
        public float SunsetTime => sunsetTime;
        public float SunPathAzimuth => sunPathAzimuth;
        public float SunPathTilt => sunPathTilt;
        public float MoonriseTime => moonriseTime;
        public float MoonsetTime => moonsetTime;
        public float MoonPathAzimuth => moonPathAzimuth;
        public float MoonPathTilt => moonPathTilt;
        public float CelestialSwapDuration => celestialSwapDuration;
        public float ComputedMoonMaskSize => computedMoonMaskSize;
        public float ComputedMoonDiscSize => computedMoonDiscSize;
        public Vector2 ComputedMoonMaskOffset => computedMoonMaskOffset;
        public Gradient MoonColorRamp => moonColorRamp;
        public bool DiscHorizonDarkening => discHorizonDarkening;
        public float DiscHorizonDarkeningHeight => discHorizonDarkeningHeight;
        public float DiscHorizonDarkeningFloor => discHorizonDarkeningFloor;

        public Gradient SkyZenitColorRamp => skyZenitColorRamp;
        public Gradient SkyHorizonColorRamp => skyHorizonColorRamp;
        public Gradient SkyNadirColorRamp => skyNadirColorRamp;
        public Gradient RimColorRamp => rimColorRamp;

        public bool UseSkyLut => useSkyLut;
        public Gradient SkyNight => skyNight;
        public Gradient SkySunrise => skySunrise;
        public Gradient SkyDay => skyDay;
        public Gradient SkySunset => skySunset;
        public Texture2D? SkyLut => skyLut;
        public Texture2D? SkyHorizonNoise => skyHorizonNoise;
        public float SkyHorizonNoiseStrength => skyHorizonNoiseStrength;
        public Vector2 SkyHorizonNoiseTiling => skyHorizonNoiseTiling;
        public float SkyHorizonNoiseSpeed => skyHorizonNoiseSpeed;
        public bool UseStarsV2 => useStarsV2;
        public float StarsV2Brightness => starsV2Brightness;
        public Vector4 StarsBrightnessByPhase => starsBrightnessByPhase;
        public float StarsDensity => starsDensity;
        public float StarsSize => starsSize;
        public float StarsRotationSpeed => starsRotationSpeed;
        public float StarsTwinkle => starsTwinkle;
        public float StarsTwinkleSpeed => starsTwinkleSpeed;
        public float StarsPatchStrength => starsPatchStrength;
        public Vector2 StarsHorizonFade => starsHorizonFade;
        public float ShootingStarsRate => shootingStarsRate;

        public bool IndirectLight => indirectLight;
        public Gradient IndirectSkyRamp => indirectSkyRamp;
        public Gradient IndirectEquatorRamp => indirectEquatorRamp;
        public Gradient GroundEquatorRamp => groundEquatorRamp;

        public Gradient CloudsColorRamp => cloudsColorRamp;
        public AnimationCurve CloudsHighlightsIntensity => cloudsHighlightsIntensity;

        public bool UseCloudsV2 => useCloudsV2;
        public bool CloudsV2Ramp => cloudsV2Ramp;
        public bool CloudsV2Backlight => cloudsV2Backlight;
        public bool CloudsV2Flow => cloudsV2Flow;
        public IReadOnlyList<CloudLayer> CloudLayers => cloudLayers;
        public Gradient CloudsShadowColorRamp => cloudsShadowColorRamp;
        public float CloudsRampKnee => cloudsRampKnee;
        public float CloudsHighlightStrength => cloudsHighlightStrength;
        public float CloudsHighlightThreshold => cloudsHighlightThreshold;
        public float CloudsHighlightFalloff => cloudsHighlightFalloff;
        public float CloudsZenithFadeStart => cloudsZenithFadeStart;
        public float CloudsZenithFadeEnd => cloudsZenithFadeEnd;
        public float CloudsOcclusionStart => cloudsOcclusionStart;
        public float CloudsOcclusionEnd => cloudsOcclusionEnd;

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

        /// <summary>
        ///     Linear interpolation of a per-anchor value (Night, Sunrise, Day, Sunset, wrapping back to Night) at the phase.
        /// </summary>
        public static float EvaluateByPhase(Vector4 byPhase, float phase)
        {
            float scaled = Mathf.Clamp01(phase) * 4f;
            int anchor = Mathf.Min((int)scaled, 3);
            float from = byPhase[anchor];
            float to = anchor == 3 ? byPhase[0] : byPhase[anchor + 1];
            return Mathf.Lerp(from, to, scaled - anchor);
        }
    }
}
