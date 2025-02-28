using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    [Serializable]
    public class CombinedLodsRenderer
    {
        public List<CombineInstance> СombineInstances;

        public Transform parent;

        [SerializeField] private Mesh combinedMesh;
        public Mesh CombinedMesh => combinedMesh ??= CreateCombinedMesh();

        public Material SharedMaterial;
        public int SubMeshId;

        public List<RenderParamsSerialized> RenderParamsSerialized;
        public RenderParams[] RenderParamsArray { get; private set; }// array for submeshes

        public CombinedLodsRenderer(Material material, Renderer rend, int subMeshId)
        {
            SubMeshId = subMeshId;

            СombineInstances = new List<CombineInstance>();
            RenderParamsSerialized = new List<RenderParamsSerialized>();

            parent = rend.transform.parent;

            SharedMaterial = material;
        }

        public CombinedLodsRenderer(Material material, Renderer rend, MeshFilter meshFilter)
        {
            SubMeshId = 0;

            parent = rend.transform.parent;
            SharedMaterial = material;
            combinedMesh = meshFilter.sharedMesh;

            RenderParamsSerialized = new List<RenderParamsSerialized> { new (rend) };
        }

        public void InitializeRenderParams(Dictionary<Material, Material> instancingMaterials)
        {
            RenderParamsArray = new RenderParams[RenderParamsSerialized.Count];

            for (var i = 0; i < RenderParamsSerialized.Count; i++)
                RenderParamsArray[i] = RenderParamsSerialized[i].ToRenderParams(SharedMaterial, instancingMaterials);
        }

        public void AddCombineInstance(CombineInstance combineInstance, Renderer rend)
        {
            СombineInstances.Add(combineInstance);
            RenderParamsSerialized.Add(new RenderParamsSerialized(rend));
        }

        public Mesh CreateCombinedMesh()
        {
            combinedMesh = new Mesh();

            //  mergeSubMeshes == false, so each submesh represents separate LOD level
            combinedMesh.CombineMeshes(СombineInstances.ToArray(), mergeSubMeshes: false, useMatrices: true);
            combinedMesh.UploadMeshData(true); // disable read/write
            combinedMesh.name = $"{parent.name}_{SharedMaterial.name}";

            return combinedMesh;
        }
    }
}
