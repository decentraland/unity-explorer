using Codice.Client.BaseCommands;
using System;
using UnityEngine;
using UnityEngine.Pool;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public static class TextureArrayContainerFactory
    {
        private static readonly TextureArrayContainer PBR_TEXTURE_ARRAY_CONTAINER = CreatePBR();
        private static readonly TextureArrayContainer TOON_TEXTURE_ARRAY_CONTAINER = CreateToon();
        private static readonly int ARRAY_TYPES_COUNT = Mathf.Max(PBR_TEXTURE_ARRAY_CONTAINER.count, TOON_TEXTURE_ARRAY_CONTAINER.count);
        public static readonly ObjectPool<TextureArraySlot?[]> SLOTS_POOL = new (
            () => new TextureArraySlot?[ARRAY_TYPES_COUNT], actionOnGet: array => Array.Clear(array, 0, array.Length), defaultCapacity: 500);

        internal static TextureArrayContainer CreatePBR() =>
            new (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_ARR_SHADER_ID, BASE_MAP_ARR_TEX_SHADER_ID, MAIN_TEXTURE_RESOLUTION, DEFAULT_BASEMAP_TEXTURE_FORMAT), SHADERID_DCL_PBR, BASE_MAP_ORIGINAL_TEXTURE_ID),
                });

        internal static TextureArrayContainer CreateToon() =>
            new (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_ID, MAINTEX_ARR_TEX_SHADER_ID, MAIN_TEXTURE_RESOLUTION, DEFAULT_BASEMAP_TEXTURE_FORMAT), SHADERID_DCL_TOON, MAINTEX_ORIGINAL_TEXTURE_ID),
                    new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_ID, MAINTEX_ARR_TEX_SHADER_ID, FACIAL_FEATURES_TEXTURE_RESOLUTION, DEFAULT_BASEMAP_TEXTURE_FORMAT),SHADERID_DCL_FACIAL_FEATURES,  MAINTEX_ORIGINAL_TEXTURE_ID),
                    //new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_ARR_SHADER_ID, BASE_MAP_ARR_TEX_SHADER_ID), BASE_MAP_ORIGINAL_TEXTURE_ID),
                    new (new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, NORMAL_MAP_ARR_SHADER_ID, NORMAL_MAP_ARR_TEX_SHADER_ID, NORMAL_TEXTURE_RESOLUTION, DEFAULT_NORMALMAP_TEXTURE_FORMAT), SHADERID_DCL_TOON, BUMP_MAP_ORIGINAL_TEXTURE_ID),
                    new (new TextureArrayHandler(EMISSION_TEXTURE_ARRAY_SIZE, EMISSIVE_TEX_ARR_SHADER_ID, EMISSIVE_TEX_ARR_TEX_SHADER_ID, EMISSION_TEXTURE_RESOLUTION, DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT), SHADERID_DCL_TOON, EMISSION_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, METALLIC_GLOSS_MAP_ARR_SHADER_ID, METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID), METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, OCCLUSION_MAP_ARR_SHADER_ID, OCCLUSION_MAP_ARR_TEX_SHADER_ID), OCCLUSION_MAP_ORIGINAL_TEXTURE_ID),
                });

        public static Material ActivateMaterial(Material material)
        {
            if (material.shader.name == "DCL/DCL_Toon")
            {
                var activatedMaterial = new Material(material);
                activatedMaterial.EnableKeyword("_DCL_TEXTURE_ARRAYS");
                activatedMaterial.EnableKeyword("_DCL_COMPUTE_SKINNING");
                return activatedMaterial;
            }

            return material;
        }

        public static TextureArrayContainer GetCached(Shader shader)
        {
            return shader.name switch
                   {
                       "DCL/DCL_Toon" => TOON_TEXTURE_ARRAY_CONTAINER,
                       _ => PBR_TEXTURE_ARRAY_CONTAINER,
                   };
        }

        internal static TextureArrayContainer Create(Shader shader)
        {
            return shader.name switch
                   {
                       "DCL/DCL_Toon" => CreateToon(),
                       _ => CreatePBR(),
                   };
        }
    }
}
