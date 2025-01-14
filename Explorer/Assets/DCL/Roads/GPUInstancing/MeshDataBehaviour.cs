using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;

namespace DCL.Roads.GPUInstancing
{
    [Serializable]
    public class MeshData// : IEquatable<LODInstanceData>
    {
        public Mesh Mesh;

        // RenderParams that differs foreach material
        public Material[] Materials;

        // RenderParams that same foreach material
        public bool ReceiveShadows;
        public ShadowCastingMode ShadowCastingMode;

        // converted into Matrix4x4 to be included in Matrix4x4[] instances array
        public Transform Transform;
    }

    public class MeshDataBehaviour : MonoBehaviour
    {
        [FormerlySerializedAs("MeshInstanceData")] public MeshData meshData;

        [ContextMenu(nameof(CollectSelfData))]
        private void CollectSelfData()
        {
            meshData = new MeshData
            {
                Transform = transform,
                Mesh = GetComponent<MeshFilter>().sharedMesh,
                Materials = GetComponent<MeshRenderer>().sharedMaterials,
            };

            var meshRenderer = GetComponent<MeshRenderer>();
            meshData.ReceiveShadows = meshRenderer.receiveShadows;
            meshData.ShadowCastingMode = meshRenderer.shadowCastingMode;
        }
    }
}
