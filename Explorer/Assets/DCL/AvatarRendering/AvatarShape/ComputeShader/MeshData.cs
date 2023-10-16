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

        public MeshData(MeshFilter mesh, Renderer renderer, Transform transform, Transform rootTransform, Material originalMaterial)
        {
            Mesh = mesh;
            Transform = transform;
            RootTransform = rootTransform;
            OriginalMaterial = originalMaterial;
            Renderer = renderer;
        }
    }
}
