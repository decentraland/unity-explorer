using ECS.Unity.Textures.Components;
using UnityEngine;

namespace ECS.Unity.Materials.Components
{
    public enum MaterialTransparencyMode : byte
    {
        Opaque = 0,
        AlphaTest = 1,
        AlphaBlend = 2,
        AlphaTestAndAlphaBlend = 3,
        Auto = 4,
    }

    public static class MaterialTransparencyModeExtensions
    {
        public static void ResolveAutoMode(this ref MaterialTransparencyMode transparencyMode, in TextureComponent? alphaTexture, in Color albedoColor)
        {
            if (transparencyMode == MaterialTransparencyMode.Auto)
                transparencyMode = alphaTexture != null || albedoColor.a < 1f
                    ? MaterialTransparencyMode.AlphaBlend //AlphaBlend
                    : MaterialTransparencyMode.Opaque; // Opaque
        }
    }
}
