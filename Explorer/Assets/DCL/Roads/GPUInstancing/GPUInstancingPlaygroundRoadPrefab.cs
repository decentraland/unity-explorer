using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Playground;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing
{
    [ExecuteAlways]
    public class GPUInstancingPlaygroundRoadPrefab : MonoBehaviour
    {
        public GPUInstancingCandidate[] originalPrefabs;
        public bool Run;

        public void Update()
        {
            throw new NotImplementedException();
        }

        private void RenderMeshesInstanced()
        {

            foreach (MeshRenderingData[] lod in originalPrefabs[0].Lods)
            foreach (MeshRenderingData meshRendering in lod)
            {
                GPUInstancedRenderer renderParamsArray = meshRendering.ToGPUInstancedRenderer();

                // List<Matrix4x4> shiftedInstanceData = new (instancedMesh.PerInstancesData.Length);
                // shiftedInstanceData.AddRange(RoadShift
                //     ? instancedMesh.PerInstancesData.Select(matrix => baseMatrix * matrix.instMatrix)
                //     : instancedMesh.PerInstancesData.Select(matrix => matrix.instMatrix));
                //
                // for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                //     Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], instancedRenderer.Mesh, i, shiftedInstanceData);
            }
        }
    }
}
