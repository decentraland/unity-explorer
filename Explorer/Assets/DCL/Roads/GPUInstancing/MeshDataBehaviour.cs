using System;
using UnityEngine;
using UnityEngine.Serialization;

namespace DCL.Roads.GPUInstancing
{
    [Serializable]
    public class MeshData// : IEquatable<LODInstanceData>
    {
        public Transform Transform;

        public Mesh Mesh;
        public Material[] Materials;
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
        }
    }
}
