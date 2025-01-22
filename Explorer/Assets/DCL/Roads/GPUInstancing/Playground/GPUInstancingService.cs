using DCL.Roads.Playground;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancingService
    {
        private const int BATCH_SIZE = 511;

        internal readonly Dictionary<GPUInstancedRenderer, HashSet<Matrix4x4>> gpuInstancingMap = new ();

        public void RenderInstanced()
        {
            foreach (KeyValuePair<GPUInstancedRenderer, HashSet<Matrix4x4>> renderInstances in gpuInstancingMap)
            {
                for (var i = 0; i < renderInstances.Key.RenderParamsArray.Length; i++) // foreach submesh
                for (var j = 0; j < renderInstances.Value.Count; j += BATCH_SIZE)
                {
                    Matrix4x4[] batch = renderInstances.Value.Skip(j).Take(BATCH_SIZE).ToArray();
                    Graphics.RenderMeshInstanced(in renderInstances.Key.RenderParamsArray[i], renderInstances.Key.Mesh, i, batch);
                }
            }
        }

        public void AddToInstancing(PrefabInstanceDataBehaviour[] prefabInstanceData, Matrix4x4 roadRoot)
        {
            foreach (PrefabInstanceDataBehaviour spawnedRoad in prefabInstanceData)
                AddToInstancing(spawnedRoad, roadRoot);
        }

        public void AddToInstancing(List<MeshInstanceData> meshInstances, Matrix4x4 roadRoot)
        {
            foreach (MeshInstanceData prefabMeshInstance in meshInstances)
            {
                var instancedRenderer = prefabMeshInstance.MeshData.ToGPUInstancedRenderer();

                if (!gpuInstancingMap.TryGetValue(instancedRenderer, out HashSet<Matrix4x4> matrix))
                {
                    matrix = new HashSet<Matrix4x4>(prefabMeshInstance.InstancesMatrices.Count);
                    gpuInstancingMap.Add(instancedRenderer, matrix);
                }

                foreach (var instanceMatrix in prefabMeshInstance.InstancesMatrices)
                    matrix.Add(roadRoot * instanceMatrix);
            }
        }

        private void AddToInstancing(MeshData[] meshes, Matrix4x4 roadRoot)
        {
            foreach (MeshData meshData in meshes)
            {
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                Matrix4x4 localMatrix = meshData.LocalToRootMatrix;
                if (gpuInstancingMap.TryGetValue(instancedRenderer, out HashSet<Matrix4x4> matrix))
                    matrix.Add(roadRoot * localMatrix);
                else
                    gpuInstancingMap.Add(instancedRenderer, new HashSet<Matrix4x4> { roadRoot * localMatrix });
            }
        }

        public void AddToInstancing(PrefabInstanceDataBehaviour prefabData, Matrix4x4 roadRoot)
        {
            AddToInstancing(prefabData.Meshes, roadRoot);

            foreach (LODGroupData lodGroup in prefabData.LODGroups)
            foreach (LODEntryMeshData lods in lodGroup.LODs)
                AddToInstancing(lods.Meshes, roadRoot);
        }

        public void Clear()
        {
            gpuInstancingMap.Clear();
        }
    }
}
