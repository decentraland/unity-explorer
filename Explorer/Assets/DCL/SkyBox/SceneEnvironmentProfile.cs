using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Immutable snapshot of the look overrides of a scene, built from its PBSkybox, PBSun and PBEnvironmentFog:
    ///     gradients keyed by normalized time of day for the sun, the sky bands, the horizon rim, the fog and the cloud tint,
    ///     constant floats for the fog density, the cloud layer and the star field, and the sun visibility. A null member
    ///     keeps the value of the scene look preset. The sun intensity and direction are not look values; they are
    ///     a <see cref="SceneSunOverride" />.
    /// </summary>
    public sealed class SceneEnvironmentProfile
    {
        /// <summary>
        ///     Nothing overridden: applying it writes the scene look preset values back.
        /// </summary>
        public static readonly SceneEnvironmentProfile EMPTY = new (null, null, null, null, null, null, null, null, null, null, null, null);

        // SDK gradient time is the time of day itself, so the scene copy never remaps it through a phase curve.
        private static readonly AnimationCurve IDENTITY_PHASE = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        // A hidden sun writes zero through every curve the disc, its halo and the flare are read from.
        private static readonly AnimationCurve ZERO = AnimationCurve.Constant(0f, 1f, 0f);

        public readonly Gradient? SunColor;
        public readonly Gradient? Zenith;
        public readonly Gradient? Horizon;
        public readonly Gradient? Nadir;

        /// <summary>
        ///     Glow along the horizon line. Null follows <see cref="Horizon" /> when that is set, otherwise the preset default.
        /// </summary>
        public readonly Gradient? Rim;
        public readonly Gradient? FogColor;

        /// <summary>
        ///     Constant exponential fog density per meter, written to every phase anchor. Null keeps the preset value.
        /// </summary>
        public readonly float? FogDensity;
        public readonly Gradient? CloudsColor;
        public readonly float? CloudsOpacity;
        public readonly float? CloudsSpeed;
        public readonly float? StarsBrightness;

        /// <summary>
        ///     False hides the sun and moon discs and the lens flare; the light they cast is unaffected.
        /// </summary>
        public readonly bool? SunVisible;

        private SceneEnvironmentProfile(Gradient? sunColor, Gradient? zenith, Gradient? horizon, Gradient? nadir, Gradient? rim,
            Gradient? fogColor, float? fogDensity, Gradient? cloudsColor, float? cloudsOpacity, float? cloudsSpeed, float? starsBrightness,
            bool? sunVisible)
        {
            SunColor = sunColor;
            Zenith = zenith;
            Horizon = horizon;
            Nadir = nadir;
            Rim = rim;
            FogColor = fogColor;
            FogDensity = fogDensity;
            CloudsColor = cloudsColor;
            CloudsOpacity = cloudsOpacity;
            CloudsSpeed = cloudsSpeed;
            StarsBrightness = starsBrightness;
            SunVisible = sunVisible;
        }

        /// <summary>
        ///     Null when none of the components sets a look override, so a missing component, an unset group, an empty
        ///     gradient and a non-finite value all resolve to the default look.
        /// </summary>
        public static SceneEnvironmentProfile? FromProto(PBSkybox? pbSkybox, PBSun? pbSun, PBEnvironmentFog? pbFog)
        {
            Gradient? sunColor = ColorGradientConverter.ToGradient(pbSun?.Color);
            bool? sunVisible = pbSun is { HasVisible: true } ? pbSun.Visible : null;

            PBSkybox.Types.SkyColors? skyColors = pbSkybox?.SkyColors;
            Gradient? zenith = ColorGradientConverter.ToGradient(skyColors?.Zenith);
            Gradient? horizon = ColorGradientConverter.ToGradient(skyColors?.Horizon);
            Gradient? nadir = ColorGradientConverter.ToGradient(skyColors?.Nadir);
            Gradient? rim = ColorGradientConverter.ToGradient(skyColors?.Rim);

            Gradient? fogColor = ColorGradientConverter.ToGradient(pbFog?.Color);
            float? fogDensity = pbFog is { HasDensity: true } ? Finite(pbFog.Density, 0f, float.MaxValue) : null;

            PBSkybox.Types.Clouds? clouds = pbSkybox?.Clouds;
            Gradient? cloudsColor = ColorGradientConverter.ToGradient(clouds?.Color);
            float? cloudsOpacity = clouds is { HasOpacity: true } ? Finite(clouds.Opacity, 0f, 1f) : null;
            float? cloudsSpeed = clouds is { HasSpeed: true } ? Finite(clouds.Speed, 0f, float.MaxValue) : null;

            PBSkybox.Types.Stars? stars = pbSkybox?.Stars;
            float? starsBrightness = stars is { HasBrightness: true } ? Finite(stars.Brightness, 0f, float.MaxValue) : null;

            bool anySet = sunColor != null || zenith != null || horizon != null || nadir != null || rim != null || fogColor != null
                          || fogDensity != null || cloudsColor != null || cloudsOpacity != null || cloudsSpeed != null || starsBrightness != null
                          || sunVisible != null;

            return anySet
                ? new SceneEnvironmentProfile(sunColor, zenith, horizon, nadir, rim, fogColor, fogDensity, cloudsColor, cloudsOpacity, cloudsSpeed,
                    starsBrightness, sunVisible)
                : null;
        }

        /// <summary>
        ///     Clamps a scene value into its range; NaN and infinity count as unset so they never reach the render state.
        /// </summary>
        internal static float? Finite(float value, float min, float max) =>
            float.IsFinite(value) ? Mathf.Clamp(value, min, max) : null;

        /// <summary>
        ///     Writes the SDK-controlled values of <paramref name="target" />: each override of this profile, or the value
        ///     of <paramref name="defaults" /> where the profile has none. The sky bands also drive the ambient trilight
        ///     (zenith → sky, horizon → equator, nadir → ground), the rim follows an overridden horizon, the sun color tints
        ///     both the light and the disc, a hidden sun zeroes the disc, its halo, the moon and the lens flare, and the constant
        ///     fog density is written to all four phase anchors.
        /// </summary>
        public void ApplyTo(SkyboxLookPreset target, SkyboxLookPreset defaults)
        {
            target.TimeToPhase = IDENTITY_PHASE;

            target.DirectionalColorRamp = SunColor ?? defaults.DirectionalColorRamp;
            target.SunColorRamp = SunColor ?? defaults.SunColorRamp;

            target.SkyZenitColorRamp = Zenith ?? defaults.SkyZenitColorRamp;
            target.SkyHorizonColorRamp = Horizon ?? defaults.SkyHorizonColorRamp;
            target.SkyNadirColorRamp = Nadir ?? defaults.SkyNadirColorRamp;
            target.RimColorRamp = Rim ?? Horizon ?? defaults.RimColorRamp;

            target.IndirectSkyRamp = Zenith ?? defaults.IndirectSkyRamp;
            target.IndirectEquatorRamp = Horizon ?? defaults.IndirectEquatorRamp;
            target.GroundEquatorRamp = Nadir ?? defaults.GroundEquatorRamp;

            target.FogColorRamp = FogColor ?? defaults.FogColorRamp;
            target.FogDensityByPhase = FogDensity is { } fogDensity ? new Vector4(fogDensity, fogDensity, fogDensity, fogDensity) : defaults.FogDensityByPhase;

            target.CloudsColorRamp = CloudsColor ?? defaults.CloudsColorRamp;
            target.CloudOpacity = CloudsOpacity ?? defaults.CloudOpacity;
            target.CloudsRotationSpeed = CloudsSpeed ?? defaults.CloudsRotationSpeed;
            target.StarsBrightness = StarsBrightness ?? defaults.StarsBrightness;

            bool sunVisible = SunVisible ?? true;
            target.SunOpacity = sunVisible ? defaults.SunOpacity : ZERO;
            target.SunRadiance = sunVisible ? defaults.SunRadiance : ZERO;
            target.SunRadianceIntensity = sunVisible ? defaults.SunRadianceIntensity : ZERO;
            target.LensFlareIntensity = sunVisible ? defaults.LensFlareIntensity : ZERO;
            target.SecondSunSizeFactor = sunVisible ? defaults.SecondSunSizeFactor : 0f;
        }
    }
}
