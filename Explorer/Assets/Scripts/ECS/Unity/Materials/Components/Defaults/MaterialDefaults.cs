using DCL.ECSComponents;
using ECS.Unity.ColorComponent;
using UnityEngine;

namespace ECS.Unity.Materials.Components.Defaults
{
    public static class MaterialDefaults
    {
        public static float GetAlphaTest(this PBMaterial self)
        {
            if (self.Pbr != null && self.Pbr.HasAlphaTest)
                return self.Pbr.HasAlphaTest ? self.Pbr.AlphaTest : 0.5f;

            if (self.Unlit != null && self.Unlit.HasAlphaTest)
                return self.Unlit.HasAlphaTest ? self.Unlit.AlphaTest : 0.5f;

            return 0.5f;
        }

        public static bool GetCastShadows(this PBMaterial self)
        {
            // Note: HasCastShadows represent the existence of the parameter and not the value of castShadows itself.
            if (self.Pbr != null)
                return !self.Pbr.HasCastShadows || self.Pbr.CastShadows;

            if (self.Unlit != null)
                return !self.Unlit.HasCastShadows || self.Unlit.CastShadows;

            return true;
        }

        public static Color GetDiffuseColor(this PBMaterial self) =>
            self.Unlit?.DiffuseColor?.ToUnityColor() ?? ColorDefaults.COLOR_WHITE;

        public static Color GetAlbedoColor(this PBMaterial self) =>
            self.Pbr?.AlbedoColor?.ToUnityColor() ?? ColorDefaults.COLOR_WHITE;

        public static Color GetEmissiveColor(this PBMaterial self) =>
            self.Pbr?.EmissiveColor?.ToUnityColor() ?? ColorDefaults.COLOR_BLACK;

        public static Color GetReflectiveColor(this PBMaterial self) =>
            self.Pbr?.ReflectivityColor?.ToUnityColor() ?? ColorDefaults.COLOR_WHITE;

        public static MaterialTransparencyMode GetTransparencyMode(this PBMaterial self) =>
            self.Pbr?.HasTransparencyMode == true ? (MaterialTransparencyMode)self.Pbr.TransparencyMode : MaterialTransparencyMode.Auto;

        public static float GetMetallic(this PBMaterial self)
        {
            if (self.Pbr != null)
                return self.Pbr.HasMetallic ? self.Pbr.Metallic : 0.5f;

            return 0.5f;
        }

        public static float GetRoughness(this PBMaterial self)
        {
            if (self.Pbr != null)
                return self.Pbr.HasRoughness ? self.Pbr.Roughness : 0.5f;

            return 0.5f;
        }

        public static float GetSpecularIntensity(this PBMaterial self)
        {
            if (self.Pbr != null)
                return self.Pbr.HasSpecularIntensity ? self.Pbr.SpecularIntensity : 1f;

            return 1f;
        }

        public static float GetEmissiveIntensity(this PBMaterial self)
        {
            if (self.Pbr != null)
                return self.Pbr.HasEmissiveIntensity ? self.Pbr.EmissiveIntensity : 2f;

            return 2f;
        }

        public static float GetDirectIntensity(this PBMaterial self)
        {
            if (self.Pbr != null)
                return self.Pbr.HasDirectIntensity ? self.Pbr.DirectIntensity : 1f;

            return 1f;
        }
    }
}
