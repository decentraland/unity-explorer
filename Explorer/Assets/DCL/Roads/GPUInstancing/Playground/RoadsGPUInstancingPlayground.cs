using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;

namespace DCL.Roads.Playground
{
    [ExecuteAlways]
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        public GPUInstancedPrefab[] originalPrefabs;

        [Header("DEBUG SETTINGS")]
        [Range(0, 54)] public int DebugId;
        public Vector2Int ComparisonShift;

        [Header("ROADS SETTINGS")]
        public bool RoadShift;
        public RoadDescription[] Descriptions;

        [Space(5)]
        [Header("RUN SETTINGS")]
        public bool UseIndirect;
        [Min(0)] public int indirectMeshIndex;
        public bool Run;

        private GraphicsBuffer drawArgsBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

        private GraphicsBuffer instanceBuffer;

        private GameObject originalInstance;

        private GPUInstancedRenderer[] gpuInstancedRenderers;
        private GPUInstancedPrefab currentPrefab;

        public Shader shader;
        private LocalKeyword gpuInstancingKeyword;

        private void OnDisable()
        {
            drawArgsBuffer?.Release();
            drawArgsBuffer = null;

            instanceBuffer?.Release();
            instanceBuffer = null;
        }

        public void Update()
        {
            if (!Run) return;
            RenderMeshesIndirectOld(originalPrefabs[DebugId]);
            return;
            GPUInstancedPrefab gpuInstancedPrefab = RoadShift switch
                                                    {
                                                        true => originalPrefabs.FirstOrDefault(op => op.name == Descriptions[0].RoadModel)!,
                                                        _ => originalPrefabs[DebugId],
                                                    };

            if (currentPrefab.name != gpuInstancedPrefab.name)
            {
                currentPrefab = gpuInstancedPrefab;
                AdjustBuffers(currentPrefab.IndirectInstancedMeshes);
            }

            if (UseIndirect)
            {
                RenderMeshesIndirectAll();
                RenderMeshesInstanced(gpuInstancedPrefab.DirectInstancedMeshes);
            }
            else
                RenderMeshesInstanced(gpuInstancedPrefab.InstancedMeshes);
        }

        private void RenderMeshesIndirectOld(GPUInstancedPrefab prefab)
        {
            // Calculate total instances and commands across all meshes
            int totalInstances = 0;
            int totalCommands = 0;

            // foreach (var mesh in prefab.InstancedMeshes)
            {
                var mesh = prefab.InstancedMeshes[indirectMeshIndex];
                totalInstances += mesh.PerInstancesData?.Length ?? 0;
                totalCommands += mesh.meshInstanceData.ToGPUInstancedRenderer().RenderParamsArray.Length;
            }

            // Create buffers once
            drawArgsBuffer?.Release();
            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            instanceBuffer?.Release();
            instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalInstances, Marshal.SizeOf(typeof(PerInstanceBuffer)));

            gpuInstancingKeyword = new LocalKeyword(shader, "_GPU_INSTANCER_BATCHER");

            int currentInstanceOffset = 0;
            var currentCommandIndex = 0;

            // Set instance data
            // Matrix4x4 baseMatrix = RoadShift
            //     ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
            //     : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            // foreach (var mesh in prefab.InstancedMeshes)
            {
                var mesh = prefab.InstancedMeshes[indirectMeshIndex];
                var instancedRenderer = mesh.meshInstanceData.ToGPUInstancedRenderer();
                int submeshCount = instancedRenderer.RenderParamsArray.Length;
                int instanceCount = mesh.PerInstancesData?.Length ?? 0;

                PerInstanceBuffer[] shiftedTRS = mesh.PerInstancesData;
                // for (var i = 0; i < shiftedTRS.Length; i++)
                // {
                //     PerInstanceBuffer pid = shiftedTRS[i];
                //     pid.instMatrix = baseMatrix * pid.instMatrix;
                //     shiftedTRS[i] = pid;
                // }

                instanceBuffer.SetData(shiftedTRS, 0, currentInstanceOffset, instanceCount);

                // Set commands and render
                for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    commandData[currentCommandIndex].indexCountPerInstance = instancedRenderer.Mesh.GetIndexCount(submeshIndex);
                    commandData[currentCommandIndex].instanceCount = (uint)instanceCount;
                    commandData[currentCommandIndex].startIndex = instancedRenderer.Mesh.GetIndexStart(submeshIndex);
                    commandData[currentCommandIndex].baseVertexIndex = 0;
                    commandData[currentCommandIndex].startInstance = (uint)currentInstanceOffset;

                    drawArgsBuffer.SetData(commandData, currentCommandIndex, currentCommandIndex, count: 1);

                    RenderParams rparams = instancedRenderer.RenderParamsArray[submeshIndex];
                    rparams.matProps = new MaterialPropertyBlock();
                    rparams.matProps.SetBuffer("_PerInstanceBuffer", instanceBuffer);

                    if(rparams.material.shader == shader)
                        rparams.material.EnableKeyword(gpuInstancingKeyword);
                    else
                        Debug.LogWarning($"material {rparams.material.name} has different shader {rparams.material.shader}");

                    Graphics.RenderMeshIndirect(rparams, instancedRenderer.Mesh, drawArgsBuffer, commandCount: 1, currentCommandIndex);
                    currentCommandIndex++;
                }

                currentInstanceOffset += instanceCount;
            }
        }

        private void AdjustBuffers(List<GPUInstancedMesh> gpuInstancedMeshes)
        {
            // Calculate total instances and commands across all meshes
            var totalInstances = 0;
            var totalCommands = 0;

            foreach (var mesh in gpuInstancedMeshes)
            {
                totalInstances += mesh.PerInstancesData?.Length ?? 0;
                totalCommands += mesh.meshInstanceData.ToGPUInstancedRenderer().RenderParamsArray.Length;
            }

            // Create buffers once
            drawArgsBuffer?.Release();
            instanceBuffer?.Release();

            if(totalCommands == 0) return;

            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

            instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalInstances, Marshal.SizeOf(typeof(PerInstanceBuffer)));

            // Set instance data
            var baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            PrepareForRenderMeshesIndirect(gpuInstancedMeshes, baseMatrix);
        }

        private void PrepareForRenderMeshesIndirect(List<GPUInstancedMesh> gpuInstancedMeshes, Matrix4x4 baseMatrix)
        {
            var currentInstanceOffset = 0;
            var currentCommandIndex = 0;

            gpuInstancedRenderers = new GPUInstancedRenderer[gpuInstancedMeshes.Count];

            for (var id = 0; id < gpuInstancedMeshes.Count; id++)
            {
                GPUInstancedMesh gpuInstancedMesh = gpuInstancedMeshes[id];
                var gpuInstancedRenderer = gpuInstancedMesh.meshInstanceData.ToGPUInstancedRenderer();

                int submeshCount = gpuInstancedRenderer.RenderParamsArray.Length;
                int instanceCount = gpuInstancedMesh.PerInstancesData?.Length ?? 0;

                // PerInstanceBuffer[] shiftedTRS = gpuInstancedMesh.PerInstancesData;
                // for (var i = 0; i < shiftedTRS.Length; i++)
                // {
                //     PerInstanceBuffer pid = shiftedTRS[i];
                //     pid.instMatrix = baseMatrix * pid.instMatrix;
                //     shiftedTRS[i] = pid;
                // }

                instanceBuffer.SetData(gpuInstancedMesh.PerInstancesData, managedBufferStartIndex: 0, graphicsBufferStartIndex: currentInstanceOffset, instanceCount);

                // Set commands and render
                for (var submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    commandData[currentCommandIndex].indexCountPerInstance = gpuInstancedRenderer.Mesh.GetIndexCount(submeshIndex);
                    commandData[currentCommandIndex].instanceCount = (uint)instanceCount;
                    commandData[currentCommandIndex].startIndex = gpuInstancedRenderer.Mesh.GetIndexStart(submeshIndex);
                    commandData[currentCommandIndex].baseVertexIndex = 0;
                    commandData[currentCommandIndex].startInstance = (uint)currentInstanceOffset;

                    drawArgsBuffer.SetData(commandData, currentCommandIndex, currentCommandIndex, count: 1);

                    gpuInstancedRenderer.RenderParamsArray[submeshIndex].matProps = new MaterialPropertyBlock();
                    gpuInstancedRenderer.RenderParamsArray[submeshIndex].matProps.SetInt("_StartInstance", currentInstanceOffset);
                    gpuInstancedRenderer.RenderParamsArray[submeshIndex].matProps.SetBuffer("_PerInstanceBuffer", instanceBuffer);

                    currentCommandIndex++;
                }

                currentInstanceOffset += instanceCount;
                gpuInstancedRenderers[id] = gpuInstancedRenderer;
            }
        }

        private void RenderMeshesIndirectAll()
        {
            for (var commandId = 0; commandId < gpuInstancedRenderers.Length; commandId++)
            {
                ref GPUInstancedRenderer gpuInstancedRenderer = ref gpuInstancedRenderers[commandId];
                for (var index = 0; index < gpuInstancedRenderer.RenderParamsArray.Length; index++)
                {
                    ref RenderParams renderParams = ref gpuInstancedRenderer.RenderParamsArray[index];
                    Graphics.RenderMeshIndirect(renderParams, gpuInstancedRenderer.Mesh, drawArgsBuffer, commandCount: 1, commandId);
                }
            }
        }

        private void RenderMeshesInstanced(List<GPUInstancedMesh> instancedMeshes)
        {
            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            foreach (GPUInstancedMesh instancedMesh in instancedMeshes)
            {
                GPUInstancedRenderer instancedRenderer = instancedMesh.meshInstanceData.ToGPUInstancedRenderer();

                List<Matrix4x4> shiftedInstanceData = new (instancedMesh.PerInstancesData.Length);
                shiftedInstanceData.AddRange(RoadShift
                    ? instancedMesh.PerInstancesData.Select(matrix => baseMatrix * matrix.instMatrix)
                    : instancedMesh.PerInstancesData.Select(matrix => matrix.instMatrix));

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], instancedRenderer.Mesh, i, shiftedInstanceData);
            }
        }

        private void RenderMeshesInstancedMeshAndLods(MeshInstanceData[] meshes)
        {
            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            foreach (MeshInstanceData meshData in meshes)
            {
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], meshData.SharedMesh, i, new[] { baseMatrix * meshData.Transform.localToWorldMatrix });
            }
        }

        private GPUInstancedPrefab GetAndSpawnOriginalPrefab()
        {
            GPUInstancedPrefab originalGPUInstancedPrefab = RoadShift switch
                                                         {
                                                             true => originalPrefabs.FirstOrDefault(op => op.name == Descriptions[0].RoadModel)!,
                                                             _ => originalPrefabs[DebugId],
                                                         };

            if (originalInstance == null || originalInstance.name != originalGPUInstancedPrefab.name)
            {
                if (originalInstance != null)
                    DestroyImmediate(originalInstance);

                originalInstance = Instantiate(originalGPUInstancedPrefab.gameObject);
                originalInstance.name = originalGPUInstancedPrefab.name;
                originalInstance.transform.Translate(ComparisonShift.x, 0, ComparisonShift.y);
            }

            originalGPUInstancedPrefab.CollectSelfData();

            return originalGPUInstancedPrefab;
        }

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (GPUInstancedPrefab prefab in originalPrefabs)
                prefab.CollectSelfData();
        }
    }
}
