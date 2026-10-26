// ReSharper disable InconsistentNaming
namespace ECS.Unity.GLTFContainer.Systems
{
    /// <summary>
    ///     JSON payload of the <c>explorer-alfa-plaza-snow</c> feature flag (variant <c>config</c>). Every field is optional:
    ///     without <see cref="baseParcels" /> no scene gets snow, the look fields fall back to the defaults in <see cref="PlazaSnow" />.
    /// </summary>
    // Schema: defined here, the payload is authored in the feature flag service. Example:
    // { "baseParcels": ["0,0"], "excludedGltfs": ["models/shop_interior.glb"], "amount": 1, "color": "#F2F6FF" }
    public class PlazaSnowConfig
    {
        /// <summary>Base parcels ("x,y") of the Genesis City scenes that get snow.</summary>
        public string[]? baseParcels;

        /// <summary>GltfContainer <c>src</c> paths inside those scenes that stay free of snow, case-insensitive.</summary>
        public string[]? excludedGltfs;

        public float? amount;
        public float? coverageStart;
        public float? coverageFalloff;
        public float? tiling;
        public float? smoothness;
        public float? normalStrength;
        public float? edgeNoise;
        public float? edgeNoiseFrequency;
        public float? bumpStrength;
        public float? bumpFrequency;
        public float? sparkleIntensity;
        public float? sparkleDensity;
        public float? sparkleSize;
        public float? sparkleSharpness;
        public string? color;
    }
}
