using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public class ComputeShaderConstants
    {
        public const string TOON_KEYWORD = "_DCL_COMPUTE_SKINNING";

        public const string HAIR_MATERIAL_NAME = "hair";
        public const string SKIN_MATERIAL_NAME = "skin";

        //Compute shader properties
        public const string SKINNING_KERNEL_NAME = "main";

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

        //TODO Avatar Material. Add this textures arrays to the material

        //Material properties
        public static int _BaseColour_ShaderID = Shader.PropertyToID("_BaseColor");
    }
}
