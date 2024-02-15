using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public static class TextureArrayConstants
    {
        public const string TOON_KEYWORD = "_DCL_TEXTURE_ARRAYS";

        /// <summary>
        ///     This format is applicable to both Regular and Normal textures
        /// </summary>
        public const TextureFormat DEFAULT_TEXTURE_FORMAT = TextureFormat.BC7;

        // Some textures are less probably contained in the original material
        // so we can use a smaller starting array size for them
        public const int MAIN_TEXTURE_ARRAY_SIZE = 500;
        public const int NORMAL_TEXTURE_ARRAY_SIZE = 250;
        public const int OTHER_TEXTURE_ARRAY_SIZE = 150;

        public static readonly int MAINTEX_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_BaseMap");
        public static readonly int MAINTEX_ARR_SHADER_ID = Shader.PropertyToID("_MainTexArr_ID");
        public static readonly int MAINTEX_ARR_TEX_SHADER_ID = Shader.PropertyToID("_MainTexArr");

        public static readonly int BASE_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_BaseMap");
        public static readonly int BASE_MAP_ARR_SHADER_ID = Shader.PropertyToID("_BaseMapArr_ID");
        public static readonly int BASE_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_BaseMapArr");

        public static readonly int NORMAL_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_NormalMap");
        public static readonly int NORMAL_MAP_ARR_SHADER_ID = Shader.PropertyToID("_NormalMapArr_ID");
        public static readonly int NORMAL_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_NormalMapArr");

        public static readonly int BUMP_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_BumpMap");
        public static readonly int BUMP_MAP_ARR_SHADER_ID = Shader.PropertyToID("_BumpMapArr_ID");
        public static readonly int BUMP_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_BumpMapArr");

        public static readonly int EMISSIVE_TEX_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_Emissive_Tex");
        public static readonly int EMISSIVE_TEX_ARR_SHADER_ID = Shader.PropertyToID("_Emissive_TexArr_ID");
        public static readonly int EMISSIVE_TEX_ARR_TEX_SHADER_ID = Shader.PropertyToID("_Emissive_TexArr");

        public static readonly int EMISSION_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_EmissionMap");
        public static readonly int EMISSION_MAP_ARR_SHADER_ID = Shader.PropertyToID("_EmissionMapArr_ID");
        public static readonly int EMISSION_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_EmissionMapArr");

        public static readonly int METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_MetallicGlossMap");
        public static readonly int METALLIC_GLOSS_MAP_ARR_SHADER_ID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
        public static readonly int METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_MetallicGlossMapArr");

        public static readonly int OCCLUSION_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_OcclusionMap");
        public static readonly int OCCLUSION_MAP_ARR_SHADER_ID = Shader.PropertyToID("_OcclusionMapArr_ID");
        public static readonly int OCCLUSION_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_OcclusionMapArr");
    }
}
