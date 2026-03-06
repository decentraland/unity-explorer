using UnityEngine.Rendering;

namespace DCL.Shaders
{
    public static class SceneShaderUtils
    {
        public static readonly PassType PASS_TYPE = PassType.ScriptableRenderPipeline;

        /// <summary>
        ///     All keywords are needed to create a proper shader variants collection
        /// </summary>
        public static readonly string[] CONSTANT_KEYWORDS =
        {
            ShaderUtils.FW_PLUS,
            ShaderUtils.FW_PLUS_LIGHT_SHADOWS,
            ShaderUtils.FW_PLUS_SHADOWS_CASCADE,
            ShaderUtils.FW_PLUS_SHADOWS_SOFT,
        };

        public static readonly string[] VARIABLE_KEYWORDS =
        {
            ShaderUtils.KEYWORD_OCCLUSION,
            ShaderUtils.KEYWORD_EMISSION,
            ShaderUtils.KEYWORD_ALPHA_TEST,
            ShaderUtils.KEYWORD_SPECGLOSSMAP,
            ShaderUtils.KEYWORD_NORMALMAP,
            ShaderUtils.KEYWORD_METALLICSPECGLOSSMAP,
            ShaderUtils.KEYWORD_SURFACE_TYPE_TRANSPARENT,
            ShaderUtils.FOG_EXP,
            ShaderUtils.FOG_EXP2,
            ShaderUtils.FOG_LINEAR,
        };
    }
}
