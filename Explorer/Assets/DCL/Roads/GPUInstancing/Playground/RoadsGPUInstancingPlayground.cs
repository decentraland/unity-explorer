using DCL.Roads.GPUInstancing;
using DCL.Roads.GPUInstancing.Playground;
using DCL.Roads.Settings;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Utility;

namespace DCL.Roads.Playground
{
    [ExecuteAlways]
    public class RoadsGPUInstancingPlayground : MonoBehaviour
    {
        private static readonly int OBJECT_TO_WORLD = Shader.PropertyToID("_ObjectToWorld");

        private readonly Dictionary<GPUInstancedRenderer, List<Matrix4x4>> gpuInstancingMap = new ();

        public PrefabInstanceDataBehaviour[] originalPrefabs;
        public RoadDescription[] Descriptions;

        [Space]
        public RoadSettingsAsset RoadsConfig;

        public bool Debug;
        [Range(0, 53)] public int DebugId;
        public bool RoadShift;
        public Vector2Int ComparisonShift;
        public bool Run;

        [Header("DEBUG")]
        public Mesh[] Props;
        public Material[] Materials1;
        public Vector2Int ParcelsMin;
        public Vector2Int ParcelsMax;
        public bool UseIndirect;

        private int currentCommandIndex;
        private GraphicsBuffer commandBuffer;
        private GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;

        private Transform debugRoot;

        private GameObject originalInstance;

        public void Update()
        {
            if (!Run) return;

            if (Debug)
            {
                PrefabInstanceData prefab = GetAndSpawnOriginalPrefab().PrefabInstance;

                if (UseIndirect)
                    PrepareIndirectBuffer(prefab);

                // Render Instanced/Indirect
                {
                    DebugRenderMesh(prefab.Meshes);

                    foreach (LODGroupData lodGroup in prefab.LODGroups)
                    {
                        LODEntryMeshData lods = lodGroup.LODs[0];
                        DebugRenderMesh(lods.Meshes);
                    }
                }

                if (UseIndirect)
                    commandBuffer.SetData(commandData);
            }
            else
            {
                foreach (KeyValuePair<GPUInstancedRenderer, List<Matrix4x4>> renderInstances in gpuInstancingMap)
                {
                    for (var i = 0; i < renderInstances.Key.RenderParams.Length; i++) // foreach submesh
                    {
                        // Graphics.RenderMeshIndirect()
                        Graphics.RenderMeshInstanced(in renderInstances.Key.RenderParams[i], renderInstances.Key.Mesh, i, renderInstances.Value);
                    }
                }
            }
        }

        private void PrepareIndirectBuffer(PrefabInstanceData prefab)
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

        private void DebugRenderMesh(MeshData[] meshes)
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

        [ContextMenu(nameof(PrefabsSelfCollect))]
        private void PrefabsSelfCollect()
        {
            foreach (PrefabInstanceDataBehaviour prefab in originalPrefabs)
                prefab.CollectSelfData();
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

                for (var submeshIndex = 0; submeshIndex < instancedRenderer.RenderParams.Length; submeshIndex++)
                {
                    // Setup command data for this submesh
                    commandData[currentCommandIndex].startIndex = meshData.SharedMesh.GetIndexStart(submeshIndex);
                    commandData[currentCommandIndex].indexCountPerInstance = meshData.SharedMesh.GetIndexCount(submeshIndex);
                    commandData[currentCommandIndex].baseVertexIndex = 0;
                    commandData[currentCommandIndex].startInstance = 0;
                    commandData[currentCommandIndex].instanceCount = 1; // Since we're only rendering one instance

                    RenderParams rparams = instancedRenderer.RenderParams[submeshIndex];
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

                for (var i = 0; i < instancedRenderer.RenderParams.Length; i++)
                    Graphics.RenderMeshInstanced(in instancedRenderer.RenderParams[i], meshData.SharedMesh, i, new[] { baseMatrix * meshData.Transform.localToWorldMatrix });
            }
        }

        [ContextMenu(nameof(Collect))]
        public void Collect()
        {
            CreateDebugRoot();

            originalPrefabs[0].CollectSelfData();
            gpuInstancingMap.Clear();
            PrepareInstancesMap(RoadsConfig.RoadDescriptions);

            CollectDebugInfo();
        }

        private void PrepareInstancesMap(IEnumerable<RoadDescription> from)
        {
            HashSet<int> processedRoads = new ();

            foreach (RoadDescription roadDescription in from)
            {
                if (IsOutOfRange(roadDescription.RoadCoordinate)) continue;
                if (!processedRoads.Add(roadDescription.GetHashCode())) continue;

                var roadTransform = Matrix4x4.TRS(roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation, roadDescription.Rotation.SelfOrIdentity(), Vector3.one);

                PrefabInstanceDataBehaviour prefab = originalPrefabs.FirstOrDefault(op => op.name == roadDescription.RoadModel);
                if (prefab == null) continue;
                prefab.CollectSelfData();

                PrefabInstanceDataBehaviour roadAsset = Instantiate(prefab);

                roadAsset.transform.localPosition = roadDescription.RoadCoordinate.ParcelToPositionFlat() + ParcelMathHelper.RoadPivotDeviation;
                roadAsset.transform.localRotation = roadDescription.Rotation;
                roadAsset.gameObject.SetActive(true);

                roadAsset.transform.parent = debugRoot;

                AddPrefabDataToInstancingMap(roadAsset.PrefabInstance, roadTransform);
            }
        }

        private bool IsOutOfRange(Vector2Int roadCoordinate) =>
            roadCoordinate.x < ParcelsMin.x || roadCoordinate.x > ParcelsMax.x ||
            roadCoordinate.y < ParcelsMin.y || roadCoordinate.y > ParcelsMax.y;

        private void AddPrefabDataToInstancingMap(PrefabInstanceData prefabData, Matrix4x4 roadTransform)
        {
            AddMeshDataToInstancingMap(prefabData.Meshes, roadTransform);

            foreach (LODGroupData lodGroup in prefabData.LODGroups)
            foreach (LODEntryMeshData lods in lodGroup.LODs)
                AddMeshDataToInstancingMap(lods.Meshes, roadTransform);
        }

        private void AddMeshDataToInstancingMap(MeshData[] meshes, Matrix4x4 roadTransform)
        {
            foreach (MeshData meshData in meshes)
            {
                // Matrix4x4 finalMatrix = roadTransform * meshData.Transform.localToWorldMatrix;
                Matrix4x4 finalMatrix = meshData.Transform.localToWorldMatrix;

                var instancedRenderer = meshData.ToGPUInstancedRenderer();

                if (gpuInstancingMap.TryGetValue(instancedRenderer, out List<Matrix4x4> matrix))
                    matrix.Add(finalMatrix);
                else
                    gpuInstancingMap.Add(instancedRenderer, new List<Matrix4x4> { finalMatrix });
            }
        }

        private void CreateDebugRoot()
        {
            if (GameObject.Find("RoadsRoot"))
                DestroyImmediate(GameObject.Find("RoadsRoot"));

            debugRoot = new GameObject("RoadsRoot").transform;
            debugRoot.gameObject.SetActive(false);
        }

        private void CollectDebugInfo()
        {
            var props = new List<Mesh>();
            var materials = new List<Material>();

            foreach (KeyValuePair<GPUInstancedRenderer, List<Matrix4x4>> propPair in gpuInstancingMap)
            {
                props.Add(propPair.Key.Mesh);
                materials.Add(propPair.Key.RenderParams[0].material);
            }

            Materials1 = materials.ToArray();
            Props = props.ToArray();
        }
    }
}
