using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderConstants
    {
        public enum TextureArrayType
        {
            ALBEDO = 0,
            ALPHA = 1,
            METALLIC = 2,
            BUMP = 3,
            EMMISSION = 4,
        }

        public static readonly int _BaseMapArr_ShaderID = Shader.PropertyToID("_BaseMapArr_ID");
        public static readonly int _BaseMapArrTex_ShaderID = Shader.PropertyToID("_BaseMapArr");

        //Compute shader properties
        public static readonly int BONE_COUNT = 62;
        public static readonly int VERT_COUNT_ID = Shader.PropertyToID("g_VertCount");
        public static readonly int LAST_AVATAR_VERT_COUNT_ID = Shader.PropertyToID("_lastAvatarVertCount");
        public static readonly int LAST_WEARABLE_VERT_COUNT_ID = Shader.PropertyToID("_lastWearableVertCount");
        public static readonly int VERTS_IN_ID = Shader.PropertyToID("g_VertsIn");
        public static readonly int NORMALS_IN_ID = Shader.PropertyToID("g_NormalsIn");
        public static readonly int TANGENTS_IN_ID = Shader.PropertyToID("g_TangentsIn");
        public static readonly int SOURCE_SKIN_ID = Shader.PropertyToID("g_SourceSkin");
        public static readonly int BIND_POSE_ID = Shader.PropertyToID("g_BindPoses");
        public static readonly int BIND_POSES_INDEX_ID = Shader.PropertyToID("g_BindPosesIndex");
        public static readonly int BONES_ID = Shader.PropertyToID("g_mBones");

        public const string HAIR_MATERIAL_NAME = "hair";
        public const string SKIN_MATERIAL_NAME = "skin";

        //TODO Avatar Material. Add this textures arrays to the material
        public static int _AlphaTextureArr_ShaderID = Shader.PropertyToID("_AlphaTextureArr_ID");
        public static int _MetallicGlossMapArr_ShaderID = Shader.PropertyToID("_MetallicGlossMapArr_ID");
        public static int _BumpMapArr_ShaderID = Shader.PropertyToID("_BumpMapArr_ID");
        public static int _EmissionMapArr_ShaderID = Shader.PropertyToID("_EmissionMapArr_ID");

        //Material properties
        public static string[] keywordsToCheck = { "_NORMALMAP", "_ALPHATEST_ON", "_EMISSION", "_SURFACE_TYPE_TRANSPARENT" };
        public static int _BaseColour_ShaderID = Shader.PropertyToID("_BaseColor");

        //Compute shader properties
        public const string SKINNING_KERNEL_NAME = "main";
    }
}
