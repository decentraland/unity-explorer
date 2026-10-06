using DCL.AvatarRendering.Loading.Assets;
using UnityEngine;

namespace DCL.AvatarRendering.AvatarShape.ComputeShader
{
    public readonly struct MeshData
    {
        public readonly MeshFilter Mesh;
        public readonly Renderer Renderer;
        public readonly Material OriginalMaterial;
        public readonly Transform Transform;
        public readonly Transform RootTransform;

        /// <summary>
        ///     The attachment asset <see cref="Mesh" /> was instantiated from, which owns the mesh and therefore
        ///     the record of whether its tangents have already been recalculated.
        /// </summary>
        public readonly AttachmentRegularAsset OriginalAsset;

        /// <summary>
        ///     Number of spring bones from previous wearables. Added to BoneWeight indices >= BASE_BONE_COUNT
        ///     so each wearable's spring bones map to the correct slot in the global bone matrix buffer.
        /// </summary>
        public readonly int SpringBoneOffset;

        public MeshData(MeshFilter mesh, Renderer renderer, Transform transform, Transform rootTransform, Material originalMaterial, AttachmentRegularAsset originalAsset, int springBoneOffset = 0)
        {
            Mesh = mesh;
            Transform = transform;
            RootTransform = rootTransform;
            OriginalMaterial = originalMaterial;
            Renderer = renderer;
            OriginalAsset = originalAsset;
            SpringBoneOffset = springBoneOffset;
        }
    }
}
