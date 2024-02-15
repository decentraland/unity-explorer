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
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_ARR_SHADER_ID, BASE_MAP_ARR_TEX_SHADER_ID), BASE_MAP_ORIGINAL_TEXTURE_ID),
                });

        internal static TextureArrayContainer CreateToon() =>
            new (
                new TextureArrayMapping[]
                {
                    new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, MAINTEX_ARR_SHADER_ID, MAINTEX_ARR_TEX_SHADER_ID), MAINTEX_ORIGINAL_TEXTURE_ID),
                    //new (new TextureArrayHandler(MAIN_TEXTURE_ARRAY_SIZE, BASE_MAP_ARR_SHADER_ID, BASE_MAP_ARR_TEX_SHADER_ID), BASE_MAP_ORIGINAL_TEXTURE_ID),
                    //new (new TextureArrayHandler(NORMAL_TEXTURE_ARRAY_SIZE, BUMP_MAP_ARR_SHADER_ID, BUMP_MAP_ARR_TEX_SHADER_ID), BUMP_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, EMISSION_MAP_ARR_SHADER_ID, EMISSION_MAP_ARR_TEX_SHADER_ID), EMISSION_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, METALLIC_GLOSS_MAP_ARR_SHADER_ID, METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID), METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID),
                    // new (new TextureArrayHandler(OTHER_TEXTURE_ARRAY_SIZE, OCCLUSION_MAP_ARR_SHADER_ID, OCCLUSION_MAP_ARR_TEX_SHADER_ID), OCCLUSION_MAP_ORIGINAL_TEXTURE_ID),
                });

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
