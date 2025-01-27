using DCL.Roads.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DCL.Roads.GPUInstancing.Playground
{
    public class GPUInstancingService
    {
        private const int BATCH_SIZE = 511;

        internal readonly Dictionary<GPUInstancedRenderer, PerInstanceBuffer[]> gpuInstancingMap = new ();
        private readonly Dictionary<GPUInstancedRenderer, List<PerInstanceBuffer[]>> gpuBatchesMap = new ();

        public void RenderInstanced()
        {
            foreach (KeyValuePair<GPUInstancedRenderer, PerInstanceBuffer[]> renderInstances in gpuInstancingMap)
            {
                for (var i = 0; i < renderInstances.Key.RenderParamsArray.Length; i++) // foreach submesh
                for (var j = 0; j < renderInstances.Value.Length; j += BATCH_SIZE)
                {
                    PerInstanceBuffer[] batch = renderInstances.Value.Skip(j).Take(BATCH_SIZE).ToArray();
                    Graphics.RenderMeshInstanced(in renderInstances.Key.RenderParamsArray[i], renderInstances.Key.Mesh, i, batch);
                }
            }
        }

        public void RenderInstancedBatched()
        {
            foreach ((GPUInstancedRenderer renderer, List<PerInstanceBuffer[]> batches) in gpuBatchesMap)
            {
                for (var subMeshIndex = 0; subMeshIndex < renderer.RenderParamsArray.Length; subMeshIndex++)
                {
                    foreach (PerInstanceBuffer[] batch in batches)
                    {
                        if (batch.Length != 0)
                            Graphics.RenderMeshInstanced(in renderer.RenderParamsArray[subMeshIndex], renderer.Mesh, subMeshIndex, batch);
                    }
                }
            }
        }

        public void PrepareBatches()
        {
            // gpuBatchesMap.Clear();
            //
            // foreach ((GPUInstancedRenderer renderer, PerInstance[] matrices) in gpuInstancingMap)
            // {
            //     var batches = new List<PerInstance[]>();
            //
            //     for (var i = 0; i < matrices.Length; i += BATCH_SIZE)
            //     {
            //         int count = Mathf.Min(BATCH_SIZE, matrices.Length - i);
            //         var batch = new PerInstance[count];
            //         matrices.CopyTo(i, batch, 0, count);
            //         batches.Add(batch);
            //     }
            //
            //     gpuBatchesMap[renderer] = batches;
            // }
        }

        public void AddToInstancingDirectCopy(List<GPUInstancedMesh> meshInstances)
        {
            foreach (GPUInstancedMesh prefabMeshInstance in meshInstances)
            {
                var instancedRenderer = prefabMeshInstance.meshInstanceData.ToGPUInstancedRenderer();

                if (!gpuInstancingMap.ContainsKey(instancedRenderer))
                    gpuInstancingMap.Add(instancedRenderer, prefabMeshInstance.PerInstancesData);
            }
        }

        public void AddToInstancing(GPUInstancedPrefab[] prefabInstanceData, Matrix4x4 roadRoot)
        {
            foreach (GPUInstancedPrefab spawnedRoad in prefabInstanceData)
                AddToInstancing(spawnedRoad, roadRoot);
        }

        public void AddToInstancing(List<GPUInstancedMesh> meshInstances, Matrix4x4 roadRoot)
        {
            foreach (GPUInstancedMesh prefabMeshInstance in meshInstances)
            {
                var instancedRenderer = prefabMeshInstance.meshInstanceData.ToGPUInstancedRenderer();

                if (!gpuInstancingMap.TryGetValue(instancedRenderer, out PerInstanceBuffer[] matrix))
                {
                    matrix = new PerInstanceBuffer[prefabMeshInstance.PerInstancesData.Length];
                    gpuInstancingMap.Add(instancedRenderer, matrix);
                }

                for (var i = 0; i < prefabMeshInstance.PerInstancesData.Length; i++)
                {
                    PerInstanceBuffer instanceBufferData = prefabMeshInstance.PerInstancesData[i];
                    matrix[i] = new PerInstanceBuffer { instMatrix = roadRoot * instanceBufferData.instMatrix };
                }
            }
        }

        private void AddToInstancing(MeshInstanceData[] meshes, Matrix4x4 roadRoot)
        {
            // foreach (MeshData meshData in meshes)
            // {
            //     var instancedRenderer = meshData.ToGPUInstancedRenderer();
            //     var instanceData = new PerInstance { objectToWorld = roadRoot * meshData.LocalToRootMatrix };
            //
            //     if (gpuInstancingMap.TryGetValue(instancedRenderer, out PerInstance[] matrix))
            //         matrix.Add(instanceData);
            //     else
            //         gpuInstancingMap.Add(instancedRenderer, new[] { instanceData });
            // }
        }

        private void AddToInstancing(GPUInstancedPrefab gpuInstancedPrefabData, Matrix4x4 roadRoot)
        {
            AddToInstancing(gpuInstancedPrefabData.Meshes, roadRoot);

            foreach (LODGroupData lodGroup in gpuInstancedPrefabData.LODGroups)
            foreach (GPUInstancedLOD lods in lodGroup.LODs)
                AddToInstancing(lods.Meshes, roadRoot);
        }

        public void Clear()
        {
            gpuInstancingMap.Clear();
        }
    }
}
