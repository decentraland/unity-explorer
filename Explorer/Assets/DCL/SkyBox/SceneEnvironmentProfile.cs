using DCL.ECSComponents;
using UnityEngine;

namespace DCL.SkyBox
{
    /// <summary>
    ///     Immutable snapshot of the environment overrides of a PBSkybox: color ramps sampled by normalized time of day,
    ///     constant floats for the cloud layer and the star field, and the sun visibility. A null member keeps the
    ///     time-of-day default.
    /// </summary>
    public sealed class SceneEnvironmentProfile
    {
        public readonly ColorRamp? SunColor;
        public readonly ColorRamp? Zenith;
        public readonly ColorRamp? Horizon;
        public readonly ColorRamp? Nadir;
        public readonly ColorRamp? FogColor;
        public readonly float? CloudsOpacity;
        public readonly float? CloudsSpeed;
        public readonly float? StarsBrightness;

        /// <summary>
        ///     False hides the sun and moon discs and the lens flare; the light they cast is unaffected.
        /// </summary>
        public readonly bool? SunVisible;

        private SceneEnvironmentProfile(ColorRamp? sunColor, ColorRamp? zenith, ColorRamp? horizon, ColorRamp? nadir, ColorRamp? fogColor,
            float? cloudsOpacity, float? cloudsSpeed, float? starsBrightness, bool? sunVisible)
        {
            SunColor = sunColor;
            Zenith = zenith;
            Horizon = horizon;
            Nadir = nadir;
            FogColor = fogColor;
            CloudsOpacity = cloudsOpacity;
            CloudsSpeed = cloudsSpeed;
            StarsBrightness = starsBrightness;
            SunVisible = sunVisible;
        }

        /// <summary>
        ///     Null when the component sets no environment override at all, so an unset group and an empty gradient
        ///     both resolve to the default environment.
        /// </summary>
        public static SceneEnvironmentProfile? FromProto(PBSkybox pbSkybox)
        {
            PBSkybox.Types.Sun? sun = pbSkybox.Sun;
            ColorRamp? sunColor = ColorRamp.FromProto(sun?.Color);
            bool? sunVisible = sun is { HasVisible: true } ? sun.Visible : null;

            PBSkybox.Types.SkyColors? skyColors = pbSkybox.SkyColors;
            ColorRamp? zenith = ColorRamp.FromProto(skyColors?.Zenith);
            ColorRamp? horizon = ColorRamp.FromProto(skyColors?.Horizon);
            ColorRamp? nadir = ColorRamp.FromProto(skyColors?.Nadir);

            ColorRamp? fogColor = ColorRamp.FromProto(pbSkybox.Fog?.Color);

            PBSkybox.Types.Clouds? clouds = pbSkybox.Clouds;
            float? cloudsOpacity = clouds is { HasOpacity: true } ? Mathf.Clamp01(clouds.Opacity) : null;
            float? cloudsSpeed = clouds is { HasSpeed: true } ? Mathf.Max(0f, clouds.Speed) : null;

            PBSkybox.Types.Stars? stars = pbSkybox.Stars;
            float? starsBrightness = stars is { HasBrightness: true } ? Mathf.Max(0f, stars.Brightness) : null;

            bool anySet = sunColor != null || zenith != null || horizon != null || nadir != null || fogColor != null
                          || cloudsOpacity != null || cloudsSpeed != null || starsBrightness != null || sunVisible != null;

            return anySet
                ? new SceneEnvironmentProfile(sunColor, zenith, horizon, nadir, fogColor, cloudsOpacity, cloudsSpeed, starsBrightness, sunVisible)
                : null;
        }
    }
}
