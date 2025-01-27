using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using UnityEngine;
using Utility;

namespace DCL.Roads.Playground
{
    [ExecuteAlways]
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        private static readonly int OBJECT_TO_WORLD = Shader.PropertyToID("_ObjectToWorld");

        public GPUInstancedPrefab[] originalPrefabs;
        public Material DebugMaterial;

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

        private int currentCommandIndex;
        private GraphicsBuffer drawArgsBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

        private GraphicsBuffer instanceBuffer;

        private GameObject originalInstance;

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

            // PrefabInstanceDataBehaviour prefab = GetAndSpawnOriginalPrefab();

            GPUInstancedPrefab gpuInstancedPrefab = RoadShift switch
                                                 {
                                                     true => originalPrefabs.FirstOrDefault(op => op.name == Descriptions[0].RoadModel)!,
                                                     _ => originalPrefabs[DebugId],
                                                 };

            if (UseIndirect)
                RenderMeshesIndirect(gpuInstancedPrefab);
            else
                RenderMeshesInstanced(gpuInstancedPrefab.GPUInstancedMeshes);
        }

    private void RenderMeshesIndirect(GPUInstancedPrefab gpuInstancedPrefab)
{
    // Calculate total instances and commands across all meshes
    int totalInstances = 0;
    int totalCommands = 0;
    foreach (var mesh in gpuInstancedPrefab.GPUInstancedMeshes)
    {
        totalInstances += mesh.PerInstancesData?.Length ?? 0;
        totalCommands += mesh.meshInstanceData.ToGPUInstancedRenderer().RenderParamsArray.Length;
    }

    // Create buffers once
    drawArgsBuffer?.Release();
    drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, totalCommands, GraphicsBuffer.IndirectDrawIndexedArgs.size);
    commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[totalCommands];

    instanceBuffer?.Release();
    instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalInstances, Marshal.SizeOf(typeof(PerInstanceBuffer)));

    int currentInstanceOffset = 0;
    currentCommandIndex = 0;

    // Set instance data
    Matrix4x4 baseMatrix = RoadShift
        ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
        : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

    foreach (GPUInstancedMesh gpuInstancedMesh in gpuInstancedPrefab.GPUInstancedMeshes)
    {
        var instancedRenderer = gpuInstancedMesh.meshInstanceData.ToGPUInstancedRenderer();
        int submeshCount = instancedRenderer.RenderParamsArray.Length;
        int instanceCount = gpuInstancedMesh.PerInstancesData?.Length ?? 0;

        PerInstanceBuffer[] shiftedTRS = gpuInstancedMesh.PerInstancesData;
        for (var i = 0; i < shiftedTRS.Length; i++)
        {
            PerInstanceBuffer pid = shiftedTRS[i];
            pid.instMatrix = baseMatrix * pid.instMatrix;
            shiftedTRS[i] = pid;
        }

        instanceBuffer.SetData(shiftedTRS, managedBufferStartIndex: 0, graphicsBufferStartIndex: currentInstanceOffset, instanceCount);

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
            rparams.matProps.SetInt("_StartInstance", currentInstanceOffset);
            rparams.matProps.SetBuffer("_PerInstanceBuffer", instanceBuffer);

            Graphics.RenderMeshIndirect(rparams, instancedRenderer.Mesh, drawArgsBuffer, commandCount: 1, currentCommandIndex);
            currentCommandIndex++;
        }

        currentInstanceOffset += instanceCount;
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

        private void RenderMeshesIndirectMeshAndLods(MeshInstanceData[] meshes)
        {
            // int totalCommandCount = meshes.SelectMany(mesh => mesh.SharedMaterials).Count();
            // if (commandBuffer == null || commandData.Length != totalCommandCount)
            //     InitializeBuffers(totalCommandCount);

            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            // var currentCommandIndex = 0; // Track the overall command index
            foreach (MeshInstanceData meshData in meshes)
            {
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                for (var submeshIndex = 0; submeshIndex < instancedRenderer.RenderParamsArray.Length; submeshIndex++)
                {
                    // Setup command data for this submesh
                    commandData[currentCommandIndex].startIndex = meshData.SharedMesh.GetIndexStart(submeshIndex);
                    commandData[currentCommandIndex].indexCountPerInstance = meshData.SharedMesh.GetIndexCount(submeshIndex);
                    commandData[currentCommandIndex].baseVertexIndex = 0;
                    commandData[currentCommandIndex].startInstance = 0;
                    commandData[currentCommandIndex].instanceCount = 1; // Since we're only rendering one instance

                    RenderParams rparams = instancedRenderer.RenderParamsArray[submeshIndex];
                    rparams.worldBounds = new Bounds(Vector3.zero, 10000 * Vector3.one); //meshData.SharedMesh.bounds; // Adjust bounds as needed, use tighter bounds for better FOV culling
                    rparams.matProps ??= new MaterialPropertyBlock();
                    rparams.matProps.SetMatrix(OBJECT_TO_WORLD, baseMatrix * meshData.Transform.localToWorldMatrix);

                    Graphics.RenderMeshIndirect(rparams, meshData.SharedMesh, drawArgsBuffer, 1, currentCommandIndex);

                    currentCommandIndex++;
                }
            }

            drawArgsBuffer!.SetData(commandData);
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

        private void RenderMeshDebug(MeshInstanceData[] meshes)
        {
            if (UseIndirect)
                RenderMeshesIndirectMeshAndLods(meshes);
            else
                RenderMeshesInstancedMeshAndLods(meshes);
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
