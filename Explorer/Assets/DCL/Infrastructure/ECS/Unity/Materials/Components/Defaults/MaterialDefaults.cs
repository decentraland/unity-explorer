using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using UnityEngine;

namespace ECS.Unity.Materials.Components.Defaults
{
    public static class PBMaterialExtensions
    {
        public static float GetAlphaTest(this PBMaterial self)
        {
            if (self.Pbr != null)
                return self.Pbr.HasAlphaTest ? self.Pbr.AlphaTest : Default.ALPHA_TEST;

            if (self.Unlit != null)
                return self.Unlit.HasAlphaTest ? self.Unlit.AlphaTest : Default.ALPHA_TEST;

            return Default.ALPHA_TEST;
        }

        public static bool GetCastShadows(this PBMaterial self)
        {
            // Note: HasCastShadows represent the existence of the parameter and not the value of castShadows itself.
            if (self.Pbr != null)
                return !self.Pbr.HasCastShadows || self.Pbr.CastShadows;

            if (self.Unlit != null)
                return !self.Unlit.HasCastShadows || self.Unlit.CastShadows;

            return Default.CAST_SHADOWS;
        }

        public static Color GetDiffuseColor(this PBMaterial self) =>
            self.Unlit?.DiffuseColor?.ToUnityColor() ?? Default.DIFFUSE_COLOR;

        public static Color GetAlbedoColor(this PBMaterial self) =>
            self.Pbr?.AlbedoColor?.ToUnityColor() ?? Default.ALBEDO_COLOR;

        public static Color GetEmissiveColor(this PBMaterial self) =>
            self.Pbr?.EmissiveColor?.ToUnityColor() ?? Default.EMISSIVE_COLOR;

        public static Color GetReflectiveColor(this PBMaterial self) =>
            self.Pbr?.ReflectivityColor?.ToUnityColor() ?? Default.REFLECTIVE_COLOR;

        public static MaterialTransparencyMode GetTransparencyMode(this PBMaterial self) =>
            self.Pbr?.HasTransparencyMode == true ? (MaterialTransparencyMode)self.Pbr.TransparencyMode : Default.TRANSPARENCY_MODE;

        public static float GetMetallic(this PBMaterial self) =>
            self.Pbr?.HasMetallic == true ? self.Pbr.Metallic : Default.METALLIC;

        public static float GetRoughness(this PBMaterial self) =>
            self.Pbr?.HasRoughness == true ? self.Pbr.Roughness : Default.ROUGHNESS;

        public static float GetSpecularIntensity(this PBMaterial self) =>
            self.Pbr?.HasSpecularIntensity == true ? self.Pbr.SpecularIntensity : Default.SPECULAR_INTENSITY;

        public static float GetEmissiveIntensity(this PBMaterial self) =>
            self.Pbr?.HasEmissiveIntensity == true ? self.Pbr.EmissiveIntensity : Default.EMISSIVE_INTENSITY;

        public static float GetDirectIntensity(this PBMaterial self) =>
            self.Pbr?.HasDirectIntensity == true ? self.Pbr.DirectIntensity : Default.DIRECT_INTENSITY;

        /// <summary>
        ///     Default constant values for material properties, that rewrite protobuf defaults
        /// </summary>
        private static class Default
        {
            public const float ALPHA_TEST = 0.5f;
            public const bool CAST_SHADOWS = true;

            public static readonly Color ALBEDO_COLOR = ColorDefaults.COLOR_WHITE;
            public static readonly Color DIFFUSE_COLOR = ColorDefaults.COLOR_WHITE;
            public static readonly Color EMISSIVE_COLOR = ColorDefaults.COLOR_BLACK;
            public static readonly Color REFLECTIVE_COLOR = ColorDefaults.COLOR_WHITE;

            public const MaterialTransparencyMode TRANSPARENCY_MODE = MaterialTransparencyMode.Auto;

            public const float METALLIC = 0.5f;
            public const float ROUGHNESS = 0.5f;
            public const float GLOSSINESS = 1f;

            public const float SPECULAR_INTENSITY = 1f;
            public const float EMISSIVE_INTENSITY = 2f;
            public const float DIRECT_INTENSITY = 1f;
        }
    }
}
