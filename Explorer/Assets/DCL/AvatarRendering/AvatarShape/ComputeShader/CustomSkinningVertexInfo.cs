using JetBrains.Annotations;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public struct CustomSkinningVertexInfo
    {
        [UsedImplicitly]
        public Vector3 position;

        [UsedImplicitly]
        public Vector3 normal;

        [UsedImplicitly]
        public Vector4 tangent;
    }
}
