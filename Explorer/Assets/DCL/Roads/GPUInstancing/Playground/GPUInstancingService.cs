using DCL.Roads.Playground;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancingService
    {
        private const int BATCH_SIZE = 511;

        internal readonly Dictionary<GPUInstancedRenderer, List<Matrix4x4>> gpuInstancingMap = new ();

        public void RenderInstanced(int instanceId = -1)
        {
            foreach (KeyValuePair<GPUInstancedRenderer, List<Matrix4x4>> renderInstances in gpuInstancingMap)
            {
                for (var i = 0; i < renderInstances.Key.RenderParamsArray.Length; i++) // foreach submesh
                {
                    if (instanceId >= renderInstances.Value.Count) continue;
                    List<Matrix4x4> instanceData = instanceId < 0 ? renderInstances.Value : new List<Matrix4x4> { renderInstances.Value[instanceId] };

                    for (var j = 0; j < instanceData.Count; j += BATCH_SIZE)
                    {
                        Matrix4x4[] batch = renderInstances.Value.Skip(j).Take(BATCH_SIZE).ToArray();
                        Graphics.RenderMeshInstanced(in renderInstances.Key.RenderParamsArray[i], renderInstances.Key.Mesh, i, batch);
                    }
                }
            }
        }

        // public void AddToInstancing(PrefabInstanceDataBehaviour[] prefabInstanceData)
        // {
        //     foreach (PrefabInstanceDataBehaviour spawnedRoad in prefabInstanceData)
        //         AddToInstancing(spawnedRoad.PrefabInstance);
        // }

        public void AddToInstancing(PrefabInstanceData prefabData, Matrix4x4 roadRoot)
        {
            AddToInstancing(prefabData.Meshes, roadRoot);

            foreach (LODGroupData lodGroup in prefabData.LODGroups)
            foreach (LODEntryMeshData lods in lodGroup.LODs)
                AddToInstancing(lods.Meshes, roadRoot);
        }

        private void AddToInstancing(MeshData[] meshes, Matrix4x4 roadRoot)
        {
            foreach (MeshData meshData in meshes)
            {
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                Matrix4x4 localMatrix = meshData.LocalMatrixToRoot;

                if (gpuInstancingMap.TryGetValue(instancedRenderer, out List<Matrix4x4> matrix))
                    matrix.Add(roadRoot * localMatrix);
                else
                    gpuInstancingMap.Add(instancedRenderer, new List<Matrix4x4> { roadRoot * localMatrix });
            }
        }

        public void Clear()
        {
            gpuInstancingMap.Clear();
        }
    }
}
