using DCL.ECSComponents;
using ECS.Unity.Materials.Components.Defaults;
using ECS.Unity.Textures.Components;
using ECS.Unity.Textures.Components.Extensions;
using SceneRunner.Scene;
using UnityEngine;

namespace ECS.Unity.Materials.Components
{
    public readonly struct MaterialData
    {
        public readonly bool IsPbrMaterial;
        public readonly TextureComponent? AlbedoTexture;
        public readonly float AlphaTest;

        // Cast shadows is not a part of Material, it's a part of Renderer
        public readonly bool CastShadows;

        public readonly TextureComponent? AlphaTexture;
        public readonly TextureComponent? EmissiveTexture;
        public readonly TextureComponent? BumpTexture;

        public readonly Color AlbedoColor;
        public readonly Color DiffuseColor;
        public readonly Color EmissiveColor;
        public readonly Color ReflectivityColor;

        public readonly MaterialTransparencyMode TransparencyMode;

        public readonly float Metallic;
        public readonly float Roughness;

        public readonly float SpecularIntensity;
        public readonly float EmissiveIntensity;
        public readonly float DirectIntensity;

        internal MaterialData(bool isPbrMaterial, TextureComponent? albedoTexture, TextureComponent? alphaTexture,
            TextureComponent? emissiveTexture, TextureComponent? bumpTexture, float alphaTest, bool castShadows, Color albedoColor,
            Color diffuseColor, Color emissiveColor,
            Color reflectivityColor, MaterialTransparencyMode transparencyMode, float metallic, float roughness,
            float specularIntensity, float emissiveIntensity, float directIntensity)
        {
            IsPbrMaterial = isPbrMaterial;
            AlbedoTexture = albedoTexture;
            AlphaTest = alphaTest;
            CastShadows = castShadows;
            AlphaTexture = alphaTexture;
            EmissiveTexture = emissiveTexture;
            BumpTexture = bumpTexture;
            AlbedoColor = albedoColor;
            DiffuseColor = diffuseColor;
            EmissiveColor = emissiveColor;
            ReflectivityColor = reflectivityColor;
            TransparencyMode = transparencyMode;
            Metallic = metallic;
            Roughness = roughness;
            SpecularIntensity = specularIntensity;
            EmissiveIntensity = emissiveIntensity;
            DirectIntensity = directIntensity;
        }

        public static MaterialData CreateFromPBNftShape(PBNftShape pbNftShape, ISceneData sceneData)
        {
            var albedoTexture = pbNftShape.CreateTextureComponent(sceneData);
            return CreateBasicMaterial(
                albedoTexture,
                0,
                Color.white,
                false
            );
        }

        internal static MaterialData CreateFromPBMaterial(PBMaterial pbMaterial, ISceneData sceneData)
        {
            TextureComponent? albedoTexture = (pbMaterial.Pbr?.Texture ?? pbMaterial.Unlit?.Texture).CreateTextureComponent(sceneData);

            if (pbMaterial.Pbr != null)
            {
                TextureComponent? alphaTexture = pbMaterial.Pbr.AlphaTexture.CreateTextureComponent(sceneData);
                TextureComponent? emissiveTexture = pbMaterial.Pbr.EmissiveTexture.CreateTextureComponent(sceneData);
                TextureComponent? bumpTexture = pbMaterial.Pbr.BumpTexture.CreateTextureComponent(sceneData);

                return CreatePBRMaterial(pbMaterial, albedoTexture, alphaTexture, emissiveTexture, bumpTexture);
            }

            return CreateBasicMaterial(pbMaterial, albedoTexture);
        }

        internal static MaterialData CreateBasicMaterial(PBMaterial pbMaterial, in TextureComponent? albedoTexture) =>
            CreateBasicMaterial(
                albedoTexture,
                pbMaterial.GetAlphaTest(),
                pbMaterial.GetDiffuseColor(),
                pbMaterial.GetCastShadows()
            );

        internal static MaterialData CreateBasicMaterial(TextureComponent? albedoTexture, float alphaTest, Color diffuseColor, bool castShadows)
        {
            Color defaultColor = Color.white;

            return new MaterialData(false, albedoTexture, null, null, null,
                alphaTest, castShadows, defaultColor, diffuseColor, defaultColor, defaultColor, MaterialTransparencyMode.Auto,
                0, 0, 0, 0, 0);
        }

        internal static MaterialData CreatePBRMaterial(in PBMaterial pbMaterial,
            in TextureComponent? albedoTexture,
            in TextureComponent? alphaTexture,
            in TextureComponent? emissiveTexture,
            in TextureComponent? bumpTexture
        ) =>
            CreatePBRMaterial(
                albedoTexture,
                alphaTexture,
                emissiveTexture,
                bumpTexture,
                pbMaterial.GetAlphaTest(),
                pbMaterial.GetCastShadows(),
                pbMaterial.GetAlbedoColor(),
                pbMaterial.GetEmissiveColor(),
                pbMaterial.GetReflectiveColor(),
                pbMaterial.GetTransparencyMode(),
                pbMaterial.GetMetallic(),
                pbMaterial.GetRoughness(),
                pbMaterial.GetSpecularIntensity(),
                pbMaterial.GetEmissiveIntensity(),
                pbMaterial.GetDirectIntensity()
            );

        internal static MaterialData CreatePBRMaterial(
            TextureComponent? albedoTexture,
            TextureComponent? alphaTexture,
            TextureComponent? emissiveTexture,
            TextureComponent? bumpTexture,
            float alphaTest,
            bool castShadows,
            Color albedoColor,
            Color emissiveColor,
            Color reflectivityColor,
            MaterialTransparencyMode transparencyMode,
            float metallic,
            float roughness,
            float specularIntensity,
            float emissiveIntensity,
            float directIntensity)
        {
            Color defaultColor = Color.white;

            return new MaterialData(true, albedoTexture, alphaTexture,
                emissiveTexture, bumpTexture, alphaTest, castShadows, albedoColor, defaultColor, emissiveColor,
                reflectivityColor, transparencyMode, metallic, roughness,
                specularIntensity, emissiveIntensity, directIntensity);
        }
    }
}
