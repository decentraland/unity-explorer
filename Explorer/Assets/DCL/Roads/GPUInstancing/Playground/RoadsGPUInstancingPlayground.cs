using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System;
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

        public PrefabInstanceDataBehaviour[] originalPrefabs;

        [Header("DEBUG SETTINGS")]
        [Range(0, 54)] public int DebugId;
        public Vector2Int ComparisonShift;

        [Header("ROADS SETTINGS")]
        public bool RoadShift;
        public RoadDescription[] Descriptions;

        [Space(5)]
        [Header("RUN SETTINGS")]
        public bool UseIndirect;
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

            PrefabInstanceDataBehaviour prefab = GetAndSpawnOriginalPrefab();

            if (UseIndirect)
                PrepareIndirectBuffers(prefab);

            if (UseIndirect)
                RenderMeshesIndirect(prefab.meshInstances[0]);
            else
                RenderMeshesInstanced(prefab.meshInstances);

            // Render Instanced/Indirect
            // {
            //     RenderMesh(prefab.Meshes);
            //
            //     foreach (LODGroupData lodGroup in prefab.LODGroups)
            //     {
            //         LODEntryMeshData lods = lodGroup.LODs[0];
            //         RenderMesh(lods.Meshes);
            //     }
            // }
        }

        private void PrepareIndirectBuffers(PrefabInstanceDataBehaviour prefab)
        {
            int totalCommands = 1;// prefab.meshInstances.Sum(mesh => mesh.MeshData.SharedMaterials.Length);

            if (drawArgsBuffer == null || commandData.Length != totalCommands)
                InitializeArgsBuffer(totalCommands);

            PreparePerInstanceBuffer(prefab);

            currentCommandIndex = 0;
        }

        private void PreparePerInstanceBuffer(PrefabInstanceDataBehaviour prefab)
        {
            if (instanceBuffer != null)
            {
                instanceBuffer.Release();
                instanceBuffer = null;
            }

            int totalInstanceCount = prefab.meshInstances[0].PerInstancesData.Length; //prefab.meshInstances.Sum(mesh => mesh.PerInstancesData.Length);

            if (totalInstanceCount == 0)
                return;

            instanceBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, totalInstanceCount, Marshal.SizeOf(typeof(PerInstanceBuffer)));
            instanceBuffer.SetData(prefab.meshInstances[0].PerInstancesData);
        }

        private void InitializeArgsBuffer(int commandCount)
        {
            drawArgsBuffer?.Release();
            drawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
        }

        private void RenderMeshesIndirect(MeshInstanceData mesh)
        {
            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            // foreach (MeshData meshData in meshes)
            {
                MeshData meshData = mesh.MeshData;
                int submeshCount = meshData.SharedMaterials.Length;
                int instanceCount = mesh.PerInstancesData?.Length ?? 0;

                for (int submeshIndex = 0; submeshIndex < submeshCount; submeshIndex++)
                {
                    // Fill in each submesh draw command:
                    commandData[currentCommandIndex].indexCountPerInstance = meshData.SharedMesh.GetIndexCount(submeshIndex);
                    commandData[currentCommandIndex].instanceCount = (uint)instanceCount;  // All instances for this submesh
                    commandData[currentCommandIndex].startIndex = meshData.SharedMesh.GetIndexStart(submeshIndex);
                    commandData[currentCommandIndex].baseVertexIndex = 0;
                    commandData[currentCommandIndex].startInstance = 0;

                    currentCommandIndex++;
                }

                drawArgsBuffer.SetData(commandData);

                // 3) We only need ONE RenderParams for the call if we plan to do
                //    a single multi-command draw. We'll take the first submesh’s
                //    RenderParams just to get the right Material, etc.

                var instancedRenderer = meshData.ToGPUInstancedRenderer();
                RenderParams baseRparams = instancedRenderer.RenderParamsArray[0];

                baseRparams.matProps ??= new MaterialPropertyBlock();
                baseRparams.matProps.SetBuffer("PerInstanceBuffer", instanceBuffer);
                baseRparams.matProps.SetMatrix("_RootTransform", baseMatrix);

                Graphics.RenderMeshIndirect(baseRparams, meshData.SharedMesh, drawArgsBuffer, submeshCount,
                    currentCommandIndex - submeshCount // startCommand (go back by however many we just wrote)
                );
            }
        }

        private void RenderMeshesInstanced(List<MeshInstanceData> meshInstances)
        {
            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            foreach (MeshInstanceData mesh in meshInstances)
            {
                MeshData meshData = mesh.MeshData;
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                List<Matrix4x4> instanceData = new (mesh.PerInstancesData.Length);
                instanceData.AddRange(mesh.PerInstancesData.Select(matrix => baseMatrix * matrix.instMatrix));

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], meshData.SharedMesh, i, instanceData);
            }
        }

        private void RenderMeshesIndirectDebug(MeshData[] meshes)
        {
            // int totalCommandCount = meshes.SelectMany(mesh => mesh.SharedMaterials).Count();
            // if (commandBuffer == null || commandData.Length != totalCommandCount)
            //     InitializeBuffers(totalCommandCount);

            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            // var currentCommandIndex = 0; // Track the overall command index
            foreach (MeshData meshData in meshes)
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

        private void RenderMeshesInstancedMeshAndLods(MeshData[] meshes)
        {
            Matrix4x4 baseMatrix = RoadShift
                ? Matrix4x4.TRS(Descriptions[0].RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, Descriptions[0].Rotation.SelfOrIdentity(), Vector3.one)
                : Matrix4x4.TRS(Vector3.zero, Quaternion.identity, Vector3.one);

            foreach (MeshData meshData in meshes)
            {
                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                for (var i = 0; i < instancedRenderer.RenderParamsArray.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParamsArray[i], meshData.SharedMesh, i, new[] { baseMatrix * meshData.Transform.localToWorldMatrix });
            }
        }

        private void RenderMeshDebug(MeshData[] meshes)
        {
            if (UseIndirect)
                RenderMeshesIndirectDebug(meshes);
            else
                RenderMeshesInstancedMeshAndLods(meshes);
        }

        private PrefabInstanceDataBehaviour GetAndSpawnOriginalPrefab()
        {
            PrefabInstanceDataBehaviour originalPrefab = RoadShift switch
                                                         {
                                                             true => originalPrefabs.FirstOrDefault(op => op.name == Descriptions[0].RoadModel)!,
                                                             _ => originalPrefabs[DebugId],
                                                         };

            if (originalInstance == null || originalInstance.name != originalPrefab.name)
            {
                if (originalInstance != null)
                    DestroyImmediate(originalInstance);

                originalInstance = Instantiate(originalPrefab.gameObject);
                originalInstance.name = originalPrefab.name;
                originalInstance.transform.Translate(ComparisonShift.x, 0, ComparisonShift.y);
            }

            originalPrefab.CollectSelfData();

            return originalPrefab;
        }

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (PrefabInstanceDataBehaviour prefab in originalPrefabs)
                prefab.CollectSelfData();
        }
    }
}
