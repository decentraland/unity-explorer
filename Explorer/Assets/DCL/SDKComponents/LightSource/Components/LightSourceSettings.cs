using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.SDKComponents.LightSource
{
    [CreateAssetMenu(fileName = "LightSourceSettings", menuName = "DCL/Light Source/Light Source Settings", order = 1)]
    [Serializable]
    public class LightSourceSettings : ScriptableObject
    {
        [Tooltip("Exponent used in the formula: { Range = Intensity ^ Exponent }.")]
        public float RangeFormulaExponent = 0.25f;

        [Tooltip("Duration of lights fading, both in and out.")]
        public float FadeDuration = 0.25f;

        [Tooltip("Multiplied by the intensity of Spot Lights.")]
        public float SpotLightIntensityScale = 1;

        [Tooltip("Multiplied by the intensity of Point Lights.")]
        public float PointLightIntensityScale = 1;

        [Tooltip("Default values used when a property isn't defined in a protocol object.")]
        public DefaultValuesSettings DefaultValues = new();

        [NonSerialized] public SceneLimitationsSettings SceneLimitations = new();

        [NonSerialized] public List<LodSettings> SpotLightsLods = new();

        [NonSerialized] public List<LodSettings> PointLightsLods = new();

        public void ApplyQualitySettings(in SceneLimitationsSettings sceneLimitations, List<LodSettings> spotLightsLods, List<LodSettings> pointLightsLods)
        {
            SceneLimitations = sceneLimitations;

            SpotLightsLods.Clear();
            SpotLightsLods.AddRange(spotLightsLods);

            PointLightsLods.Clear();
            PointLightsLods.AddRange(pointLightsLods);
        }

        [Serializable]
        public struct DefaultValuesSettings
        {
            [Header("Common")]
            public bool Active;

            public Color Color;

            public float Intensity;

            public float Range;

            public bool Shadows;

            [Header("Spot Lights")]
            public float InnerAngle;

            public float OuterAngle;
        }

        [Serializable]
        public struct SceneLimitationsSettings
        {
            [Tooltip("Multiplied by the parcel count to compute the maximum number of active lights that can be active in a scene at any time.")]
            public float LightsPerParcel;

            [Tooltip("Hard limit to the number of active lights in a scene.")]
            public int HardMaxLightCount;

            [Tooltip("Maximum number of Point Lights that can cast shadows at any time.")]
            public int MaxPointLightShadows;

            [Tooltip("Maximum number of Spot Lights that can cast shadows at any time.")]
            public int MaxSpotLightShadows;
        }

        [Serializable]
        public struct LodSettings
        {
            public float Distance;

            public bool IsCulled;

            public LightShadows Shadows;

            public bool OverrideShadowMapResolution;

            public int ShadowMapResolution;
        }
    }
}
