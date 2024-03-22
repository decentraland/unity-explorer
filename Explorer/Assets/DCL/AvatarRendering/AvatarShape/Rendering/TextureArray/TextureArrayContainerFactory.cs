using System.Collections.Generic;
using UnityEngine;
using static DCL.AvatarRendering.AvatarShape.Rendering.TextureArray.TextureArrayConstants;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public static class TextureArrayContainerFactory
    {
        internal static TextureArrayContainer CreatePBR(IReadOnlyList<int> defaultResolutions, IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures)
        {
            return new TextureArrayContainer(
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_TEX_ARR_INDEX, BASE_MAP_TEX_ARR, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures), BASE_MAP_ORIGINAL_TEXTURE, MAIN_TEXTURE_RESOLUTION),
                });
        }

        internal static TextureArrayContainer CreateToon(IReadOnlyList<int> defaultResolutions, IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures)
        {
            return new TextureArrayContainer(
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures),
                        MAINTEX_ORIGINAL_TEXTURE, MAIN_TEXTURE_RESOLUTION),
                    new (new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, NORMAL_MAP_TEX_ARR_INDEX, NORMAL_MAP_TEX_ARR, defaultResolutions, DEFAULT_NORMALMAP_TEXTURE_FORMAT, defaultTextures),
                        BUMP_MAP_ORIGINAL_TEXTURE_ID, NORMAL_TEXTURE_RESOLUTION),
                    new (new TextureArrayHandler(EMISSION_TEXTURE_ARRAY_SIZE, EMISSIVE_MAP_TEX_ARR_INDEX, EMISSIVE_MAP_TEX_ARR, defaultResolutions, DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT, defaultTextures),
                        EMISSION_MAP_ORIGINAL_TEXTURE_ID, EMISSION_TEXTURE_RESOLUTION),

                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, METALLIC_GLOSS_MAP_ARR_SHADER_ID, METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID), METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, OCCLUSION_MAP_ARR_SHADER_ID, OCCLUSION_MAP_ARR_TEX_SHADER_ID), OCCLUSION_MAP_ORIGINAL_TEXTURE_ID),
                });
        }

        internal static TextureArrayContainer CreateFacial(IReadOnlyList<int> defaultResolutions, IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures)
        {
            return new TextureArrayContainer(
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_INDEX, MAINTEX_ARR_TEX_SHADER, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures), MAINTEX_ORIGINAL_TEXTURE, FACIAL_FEATURES_TEXTURE_RESOLUTION),
                    new (new TextureArrayHandler(FACIAL_FEATURES_TEXTURE_ARRAY_SIZE, MASK_ARR_SHADER_ID, MASK_ARR_TEX_SHADER_ID, defaultResolutions, DEFAULT_BASEMAP_TEXTURE_FORMAT, defaultTextures), MASK_ORIGINAL_TEXTURE_ID, FACIAL_FEATURES_TEXTURE_RESOLUTION),
                });
        }

        public static TextureArrayContainer Create(Shader shader, IReadOnlyList<int> defaultResolutions, IReadOnlyDictionary<TextureArrayKey, Texture> defaultTextures)
        {
            return shader.name switch
                   {
                       TOON_SHADER => CreateToon(defaultResolutions, defaultTextures),
                       FACIAL_SHADER => CreateFacial(defaultResolutions, defaultTextures),
                       _ => CreatePBR(defaultResolutions, defaultTextures),
                   };
        }
    }
}
