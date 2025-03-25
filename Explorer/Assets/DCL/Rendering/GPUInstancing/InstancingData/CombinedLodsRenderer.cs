using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Rendering.GPUInstancing.InstancingData
{
    [Serializable]
    public struct LODMeshData
    {
        public uint BaseVertex;
        public uint StartIndex;
        public uint IndexCount;

    }

    [Serializable]
    public class CombinedLodsRenderer
    {
        public int SubMeshId;

        public Mesh CombinedMesh;
        public Material SharedMaterial;

        public RenderParamsSerialized RenderParamsSerialized;
        public RenderParams RenderParamsArray;

        public LODMeshData[] LodsMeshDataArray;

        public CombinedLodsRenderer(Material material, (Mesh combinedMesh, LODMeshData[] lodsMeshDataArray) meshData, int subMeshId, RenderParamsSerialized renderParams)
        {
            RenderParamsSerialized = renderParams;
            SubMeshId = subMeshId;
            SharedMaterial = material;
            CombinedMesh = meshData.combinedMesh;
            LodsMeshDataArray = meshData.lodsMeshDataArray;
        }

        public CombinedLodsRenderer(Material material, Renderer rend, MeshFilter meshFilter)
        {
            RenderParamsSerialized = new RenderParamsSerialized(rend);
            SubMeshId = 0;
            SharedMaterial = material;
            CombinedMesh = meshFilter.sharedMesh;
        }

        public void InitializeRenderParams(GPUInstancingMaterialsCache materialsCache)
        {
            RenderParamsArray = RenderParamsSerialized.ToRenderParams(SharedMaterial, materialsCache);
        }
    }
}
