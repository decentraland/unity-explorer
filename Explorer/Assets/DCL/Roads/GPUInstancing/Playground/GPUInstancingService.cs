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
                // Debug.Log($"{renderInstances.Key.Mesh.name}");

                for (var i = 0; i < renderInstances.Key.RenderParams.Length; i++) // foreach submesh
                {
                    if (instanceId >= renderInstances.Value.Count) continue;
                    var instanceData = instanceId < 0 ? renderInstances.Value : new List<Matrix4x4> { renderInstances.Value[instanceId] };

                    for (var j = 0; j < instanceData.Count; j += BATCH_SIZE)
                    {
                        var batch = renderInstances.Value.Skip(j).Take(BATCH_SIZE).ToArray();
                        Graphics.RenderMeshInstanced(in renderInstances.Key.RenderParams[i], renderInstances.Key.Mesh, i, batch);
                    }

                    // Debug.Log($"{renderInstances.Key.Mesh.name} - {renderInstances.Key.RenderParams[i].material.name}");
                    // foreach (Matrix4x4 value in renderInstances.Value)
                    //     Debug.Log($"{value}");
                }
            }
        }

        public void AddToInstancing(PrefabInstanceDataBehaviour[] prefabInstanceData)
        {
            foreach (PrefabInstanceDataBehaviour spawnedRoad in prefabInstanceData)
                AddToInstancing(spawnedRoad.PrefabInstance);
        }

        private void AddToInstancing(PrefabInstanceData prefabData)
        {
            AddToInstancing(prefabData.Meshes);

            foreach (LODGroupData lodGroup in prefabData.LODGroups)
            foreach (LODEntryMeshData lods in lodGroup.LODs)
                AddToInstancing(lods.Meshes);
        }

        private void AddToInstancing(MeshData[] meshes)
        {
            foreach (MeshData meshData in meshes)
            {
                Debug.Log($"-- Adding mesh {meshData.Transform.gameObject.name} from {meshData.Transform.position}");

                Matrix4x4 localMatrix = meshData.Transform.localToWorldMatrix;
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                Debug.Log($"Adding Render datas of amount {meshData.Transform.localToWorldMatrix}");
                Debug.Log($"Actual transform position: {meshData.Transform.position}");

                if (gpuInstancingMap.TryGetValue(instancedRenderer, out List<Matrix4x4> matrix))
                    matrix.Add(localMatrix);
                else
                    gpuInstancingMap.Add(instancedRenderer, new List<Matrix4x4> { localMatrix });
            }
        }

        public void Clear()
        {
            gpuInstancingMap.Clear();
        }
    }
}
