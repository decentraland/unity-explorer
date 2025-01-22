using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Linq;
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
        [Range(0, 53)] public int DebugId;
        public Vector2Int ComparisonShift;

        [Header("ROADS SETTINGS")]
        public bool RoadShift;
        public RoadDescription[] Descriptions;

        [Space(5)]
        [Header("RUN SETTINGS")]
        public bool UseIndirect;
        public bool Run;

        private int currentCommandIndex;
        private GraphicsBuffer commandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

        private GameObject originalInstance;

        public void Update()
        {
            if (!Run) return;

            PrefabInstanceDataBehaviour prefab = GetAndSpawnOriginalPrefab();

            if (UseIndirect)
                PrepareIndirectBuffer(prefab);

            // Render Instanced/Indirect
            {
                RenderMesh(prefab.Meshes);

                foreach (LODGroupData lodGroup in prefab.LODGroups)
                {
                    LODEntryMeshData lods = lodGroup.LODs[0];
                    RenderMesh(lods.Meshes);
                }
            }

            if (UseIndirect)
                commandBuffer.SetData(commandData);
        }

        private void PrepareIndirectBuffer(PrefabInstanceDataBehaviour prefab)
        {
            int totalCommands = prefab.Meshes.Sum(mesh => mesh.SharedMaterials.Length)
                                + prefab.LODGroups
                                        .Select(lodGroup => lodGroup.LODs[0])
                                        .Select(lods => lods.Meshes.Sum(mesh => mesh.SharedMaterials.Length))
                                        .Sum();

            if (commandBuffer == null || commandData.Length != totalCommands)
                InitializeBuffers(totalCommands);

            currentCommandIndex = 0;
        }

        private void OnDisable()
        {
            commandBuffer?.Release();
            commandBuffer = null;
        }

        private void RenderMesh(MeshData[] meshes)
        {
            if (UseIndirect)
                DebugRenderMeshesIndirect(meshes);
            else
                DebugRenderMeshesInstanced(meshes);
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

        private void InitializeBuffers(int commandCount)
        {
            commandBuffer?.Release();

            commandBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, commandCount, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[commandCount];
        }

        private void DebugRenderMeshesIndirect(MeshData[] meshes)
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

                    Graphics.RenderMeshIndirect(rparams, meshData.SharedMesh, commandBuffer, 1, currentCommandIndex);

                    currentCommandIndex++;
                }
            }

            commandBuffer!.SetData(commandData);
        }

        private void DebugRenderMeshesInstanced(MeshData[] meshes)
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

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (PrefabInstanceDataBehaviour prefab in originalPrefabs)
                prefab.CollectSelfData();
        }
    }
}
