using DCL.AvatarRendering.Wearables.Helpers;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.Rendering.TextureArray
{
    public static class TextureArrayConstants
    {
        public const string TOON_SHADER = "DCL/DCL_Toon";
        public const string FACIAL_SHADER = "DCL/DCL_Avatar_Facial_Features";

        /// <summary>
        ///     This format is applicable to both Regular and Normal textures
        /// </summary>
        public const TextureFormat DEFAULT_BASEMAP_TEXTURE_FORMAT = TextureFormat.BC7;
        public const TextureFormat DEFAULT_NORMALMAP_TEXTURE_FORMAT = TextureFormat.BC5;
        public const TextureFormat DEFAULT_EMISSIVEMAP_TEXTURE_FORMAT = TextureFormat.BC7;

        // Some textures are less probably contained in the original material
        // so we can use a smaller starting array size for them
        public const int MAIN_TEXTURE_ARRAY_SIZE = 500;
        public const int NORMAL_TEXTURE_ARRAY_SIZE = 250;
        public const int EMISSION_TEXTURE_ARRAY_SIZE = 150;
        public const int FACIAL_FEATURES_TEXTURE_ARRAY_SIZE = 250;

        public const int MAIN_TEXTURE_RESOLUTION = 512;
        public const int NORMAL_TEXTURE_RESOLUTION = 512;
        public const int EMISSION_TEXTURE_RESOLUTION = 512;
        public const int FACIAL_FEATURES_TEXTURE_RESOLUTION = 256;

        public const int SHADERID_DCL_PBR = 1;
        public const int SHADERID_DCL_TOON = 2;
        public const int SHADERID_DCL_FACIAL_FEATURES = 3;

        public static readonly int MAINTEX_ORIGINAL_TEXTURE = WearableTextureConstants.MAINTEX_ORIGINAL_TEXTURE;
        public static readonly int MAINTEX_ARR_SHADER_INDEX = Shader.PropertyToID("_MainTexArr_ID");
        public static readonly int MAINTEX_ARR_TEX_SHADER = Shader.PropertyToID("_MainTexArr");

        public static readonly int BASE_MAP_ORIGINAL_TEXTURE = Shader.PropertyToID("_BaseMap");
        public static readonly int BASE_MAP_TEX_ARR_INDEX = Shader.PropertyToID("_BaseMapArr_ID");
        public static readonly int BASE_MAP_TEX_ARR = Shader.PropertyToID("_BaseMapArr");

        public static readonly int NORMAL_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_NormalMap");
        public static readonly int NORMAL_MAP_TEX_ARR_INDEX = Shader.PropertyToID("_NormalMapArr_ID");
        public static readonly int NORMAL_MAP_TEX_ARR = Shader.PropertyToID("_NormalMapArr");

        public static readonly int BUMP_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_BumpMap");
        public static readonly int BUMP_MAP_ARR_SHADER_ID = Shader.PropertyToID("_BumpMapArr_ID");
        public static readonly int BUMP_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_BumpMapArr");

        public static readonly int EMISSIVE_TEX_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_Emissive_Tex");
        public static readonly int EMISSIVE_MAP_TEX_ARR_INDEX = Shader.PropertyToID("_Emissive_TexArr_ID");
        public static readonly int EMISSIVE_MAP_TEX_ARR = Shader.PropertyToID("_Emissive_TexArr");

        public static readonly int EMISSION_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_EmissionMap");
        public static readonly int EMISSION_MAP_ARR_SHADER_ID = Shader.PropertyToID("_EmissionMapArr_ID");
        public static readonly int EMISSION_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_EmissionMapArr");

        public static readonly int METALLIC_GLOSS_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_MetallicGlossMap");
        public static readonly int METALLIC_GLOSS_MAP_ARR_SHADER_ID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
        public static readonly int METALLIC_GLOSS_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_MetallicGlossMapArr");

        public static readonly int OCCLUSION_MAP_ORIGINAL_TEXTURE_ID = Shader.PropertyToID("_OcclusionMap");
        public static readonly int OCCLUSION_MAP_ARR_SHADER_ID = Shader.PropertyToID("_OcclusionMapArr_ID");
        public static readonly int OCCLUSION_MAP_ARR_TEX_SHADER_ID = Shader.PropertyToID("_OcclusionMapArr");

        public static readonly int MASK_ORIGINAL_TEXTURE_ID = WearableTextureConstants.MASK_ORIGINAL_TEXTURE_ID;
        public static readonly int MASK_ARR_SHADER_ID = Shader.PropertyToID("_MaskTexArr_ID");
        public static readonly int MASK_ARR_TEX_SHADER_ID = Shader.PropertyToID("_MaskTexArr");
    }
}
