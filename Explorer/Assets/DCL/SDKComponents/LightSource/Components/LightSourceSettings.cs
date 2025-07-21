using System;
using UnityEngine;

namespace DCL.SDKComponents.LightSource
{
    [CreateAssetMenu(fileName = "LightSourceSettings", menuName = "DCL/Light Source/Light Source Settings", order = 1)]
    [Serializable]
    public class LightSourceSettings : ScriptableObject
    {
        [Tooltip("Default values used when a property isn't defined in a protocol object.")]
        public DefaultValuesSettings DefaultValues = new ();

        [Header("Scene Limitations")]
        [Tooltip("Multiplied by the parcel count to compute the maximum number of active lights that can be active in a scene at any time.")]
        public float LightsPerParcel = 1;

        [Tooltip("Hard limit to the number of active lights in a scene.")]
        public int HardMaxLightCount = 10;

        [Tooltip("Maximum number of Point Lights that can cast shadows at any time.")]
        public int MaxPointLightShadows = 1;

        [Tooltip("Maximum number of Spot Lights that can cast shadows at any time.")]
        public int MaxSpotLightShadows = 3;

        [Header("Other")]
        [Tooltip("Exponent used in the formula: { Range = Intensity ^ Exponent }.")]
        public float RangeFormulaExponent = 0.25f;

        [Tooltip("Duration of lights fading, both in and out.")]
        public float FadeDuration = 0.25f;

        [Tooltip("Multiplied by the intensity of Spot Lights.")]
        public float SpotLightIntensityScale = 1;

        [Tooltip("Multiplied by the intensity of Point Lights.")]
        public float PointLightIntensityScale = 1;

        [Serializable]
        public class DefaultValuesSettings
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
    }
}
