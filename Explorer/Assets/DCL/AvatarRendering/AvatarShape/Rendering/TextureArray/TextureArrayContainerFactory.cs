using Codice.Client.BaseCommands;
using System;
using UnityEngine;
using UnityEngine.Pool;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public static class TextureArrayContainerFactory
    {
        public static int ARRAY_TYPES_COUNT = 0;
        public static readonly ObjectPool<TextureArraySlot?[]> SLOTS_POOL = new (
            () => new TextureArraySlot?[ARRAY_TYPES_COUNT], actionOnGet: array => Array.Clear(array, 0, array.Length), defaultCapacity: 500);

        internal static TextureArrayContainer CreatePBR() =>
            new (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_ARR_SHADER_ID, BASE_MAP_ARR_TEX_SHADER_ID, MAIN_TEXTURE_RESOLUTION, DEFAULT_BASEMAP_TEXTURE_FORMAT, Resources.Load<Texture2D>("TempTextures/DefaultWhite_BC7")), BASE_MAP_ORIGINAL_TEXTURE_ID),
                });

        internal static TextureArrayContainer CreateToon(int customResolution) =>
            new (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_ID, MAINTEX_ARR_TEX_SHADER_ID, customResolution, DEFAULT_BASEMAP_TEXTURE_FORMAT, Resources.Load<Texture2D>("TempTextures/DefaultWhite_BC7")), 
                        MAINTEX_ORIGINAL_TEXTURE_ID),
                    //new (new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, NORMAL_MAP_ARR_SHADER_ID, NORMAL_MAP_ARR_TEX_SHADER_ID, NORMAL_TEXTURE_RESOLUTION, DEFAULT_NORMALMAP_TEXTURE_FORMAT, Resources.Load<Texture2D>("TempTextures/FlatNormal_BC5")), 
                    //    BUMP_MAP_ORIGINAL_TEXTURE_ID),
                    //new (new TextureArrayHandler(EMISSION_TEXTURE_ARRAY_SIZE, EMISSIVE_TEX_ARR_SHADER_ID, EMISSIVE_TEX_ARR_TEX_SHADER_ID, EMISSION_TEXTURE_RESOLUTION, DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT, Resources.Load<Texture2D>("TempTextures/DefaultBlack_BC7")), 
                    //    EMISSION_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, METALLIC_GLOSS_MAP_ARR_SHADER_ID, METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID), METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, OCCLUSION_MAP_ARR_SHADER_ID, OCCLUSION_MAP_ARR_TEX_SHADER_ID), OCCLUSION_MAP_ORIGINAL_TEXTURE_ID),
                });
        
        internal static TextureArrayContainer CreateFacial(int customResolution) =>
            new (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_ID, MAINTEX_ARR_TEX_SHADER_ID, customResolution, DEFAULT_BASEMAP_TEXTURE_FORMAT, Resources.Load<Texture2D>("TempTextures/DefaultWhite_BC7")),  MAINTEX_ORIGINAL_TEXTURE_ID),
                });


        public static TextureArrayContainer Create(Shader shader, int resolution)
        {
            return shader.name switch
            {
                TOON_SHADER => CreateToon(resolution),
                FACIAL_SHADER => CreateFacial(resolution),
                _ => CreatePBR(),
            };
        }

    }
}
