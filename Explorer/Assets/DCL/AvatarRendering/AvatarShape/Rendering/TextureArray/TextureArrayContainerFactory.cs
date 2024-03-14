using System;
using System.Collections.Generic;
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

        internal static TextureArrayContainer CreatePBR(int customResolution, Texture defaultMain)
        {
            return new TextureArrayContainer (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_TEX_ARR_INDEX, BASE_MAP_TEX_ARR, customResolution, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultMain), BASE_MAP_ORIGINAL_TEXTURE)
                });
        }

        internal static TextureArrayContainer CreateToon(int customResolution, Texture defaultMain, Texture defaultNormal, Texture defaultEmmissive)
        {
            return new TextureArrayContainer (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, customResolution, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultMain),
                        MAINTEX_ORIGINAL_TEXTURE),
                    new (new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, NORMAL_MAP_TEX_ARR_INDEX, NORMAL_MAP_TEX_ARR, customResolution, DEFAULT_NORMALMAP_TEXTURE_FORMAT, defaultNormal),
                        BUMP_MAP_ORIGINAL_TEXTURE_ID),
                    new (new TextureArrayHandler(EMISSION_TEXTURE_ARRAY_SIZE, EMISSIVE_MAP_TEX_ARR_INDEX, EMISSIVE_MAP_TEX_ARR, customResolution, DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT, defaultEmmissive),
                        EMISSION_MAP_ORIGINAL_TEXTURE_ID)
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, METALLIC_GLOSS_MAP_ARR_SHADER_ID, METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID), METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, OCCLUSION_MAP_ARR_SHADER_ID, OCCLUSION_MAP_ARR_TEX_SHADER_ID), OCCLUSION_MAP_ORIGINAL_TEXTURE_ID),
                });
        }

        internal static TextureArrayContainer CreateFacial(int customResolution, Texture defaultMain)
        {
            return new TextureArrayContainer (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, customResolution, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultMain),  MAINTEX_ORIGINAL_TEXTURE)
                });
        }


        public static TextureArrayContainer Create(Shader shader, int resolution, Dictionary<string, Texture> defaultTextures)
        {
            return shader.name switch
            {
                TOON_SHADER => CreateToon(resolution, defaultTextures[$"Main_{resolution}"], defaultTextures[$"Normal_{resolution}"], defaultTextures[$"Emmisive_{resolution}"]),
                FACIAL_SHADER => CreateFacial(resolution, defaultTextures[$"Main_{resolution}"]),
                _ => CreatePBR(resolution, defaultTextures[$"Main_{resolution}"])
            };
        }

    }
}
