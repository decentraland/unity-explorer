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

        public List<RenderParamsSerialized> RenderParamsSerialized;
        public RenderParams[] RenderParamsArray { get; private set; }// array for submeshes

        public CombinedLodsRenderer(Material material, Renderer rend)
        {
            СombineInstances = new List<CombineInstance>();
            RenderParamsSerialized = new List<RenderParamsSerialized>();

            parent = rend.transform.parent;

            SharedMaterial = material;
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
            combinedMesh.name = $"{parent.name}_{SharedMaterial.name}";

            return combinedMesh;
        }
    }
}
