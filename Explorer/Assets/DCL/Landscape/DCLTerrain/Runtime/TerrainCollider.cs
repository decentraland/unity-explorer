using System;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;
using Random = Unity.Mathematics.Random;

namespace Decentraland.Terrain
{
    public sealed class TerrainCollider : MonoBehaviour
    {
        [field: SerializeField] private TerrainData TerrainData { get; set; }

        private static short[] indexBuffer;
        private TerrainColliderState state;

        private static readonly VertexAttributeDescriptor[] vertexLayout =
        {
            new (VertexAttribute.Position)
#if UNITY_EDITOR
          , new (VertexAttribute.Normal),
#endif
        };

        private void Awake()
        {
            state = new TerrainColliderState(TerrainData
#if UNITY_EDITOR
              , transform
#endif
            );
        }

        private void OnDestroy() =>
            state.Dispose();

        private void Update() =>
            Update(state, TerrainColliderUser.GetPositionsXZ());

        public static void Update(TerrainColliderState state, List<float2> userPositionsXZ)
        {
            if (userPositionsXZ.Count == 0)
                return;

            TerrainDataData terrainData = state.terrainData.GetData();
            float useRadius = terrainData.ParcelSize * (1f / 3f);

            for (var i = 0; i < userPositionsXZ.Count; i++)
            {
                RectInt usedRect = terrainData.PositionToParcelRect(userPositionsXZ[i], useRadius);

                for (int j = state.usedParcels.Count - 1; j >= 0; j--)
                {
                    ParcelData parcelData = state.usedParcels[j];

                    if (!usedRect.Contains(parcelData.parcel.ToVector2Int()))
                    {
                        state.usedParcels.RemoveAtSwapBack(j);
                        state.freeParcels.Add(parcelData);
                    }
                }

                for (int y = usedRect.yMin; y < usedRect.yMax; y++)
                for (int x = usedRect.xMin; x < usedRect.xMax; x++)
                {
                    int2 parcel = int2(x, y);

                    if (ContainsParcel(state.usedParcels, parcel))
                        continue;

                    if (state.freeParcels.Count > 0)
                    {
                        ParcelData parcelData = null;

                        // First, check if the exact parcel we need is in the free list already. If so,
                        // there's nothing to do.
                        for (int j = state.freeParcels.Count - 1; j >= 0; j--)
                        {
                            ParcelData freeParcel = state.freeParcels[j];

                            if (all(freeParcel.parcel == parcel))
                            {
                                parcelData = freeParcel;
                                state.freeParcels.RemoveAtSwapBack(j);
                                state.usedParcels.Add(parcelData);
                                break;
                            }
                        }

                        // Else, reuse the last parcel in the free list.
                        if (parcelData == null)
                        {
                            int lastIndex = state.freeParcels.Count - 1;
                            parcelData = state.freeParcels[lastIndex];
                            parcelData.parcel = parcel;
                            state.dirtyParcels.Add(parcelData);
                            state.freeParcels.RemoveAt(lastIndex);
                            state.usedParcels.Add(parcelData);

                            parcelData.collider.transform.position = new Vector3(
                                parcel.x * terrainData.ParcelSize, 0f,
                                parcel.y * terrainData.ParcelSize);

                            for (var j = 0; j < parcelData.trees.Count; j++)
                            {
                                TreeInstance tree = parcelData.trees[j];
                                state.treePools[tree.prototypeIndex].Release(tree.gameObject);
                            }

                            parcelData.trees.Clear();
                            GenerateTrees(parcel, in terrainData, parcelData, state);
                        }
                    }
                    else
                    {
                        Mesh mesh = CreateParcelMesh(in terrainData);
                        ParcelData parcelData = new () { parcel = parcel, mesh = mesh };
                        state.dirtyParcels.Add(parcelData);
                        state.usedParcels.Add(parcelData);

                        parcelData.collider = new GameObject("Parcel Collider")
                           .AddComponent<MeshCollider>();

                        parcelData.collider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;
                        Transform transform = parcelData.collider.transform;

                        transform.position = new Vector3(parcel.x * terrainData.ParcelSize, 0f,
                            parcel.y * terrainData.ParcelSize);

                        transform.SetParent(state.parent, true);

                        GenerateTrees(parcel, in terrainData, parcelData, state);
                    }
                }
            }

            if (state.dirtyParcels.Count == 0)
                return;

            var parcels = new NativeArray<int2>(state.dirtyParcels.Count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < state.dirtyParcels.Count; i++)
                parcels[i] = state.dirtyParcels[i].parcel;

            int sideVertexCount = terrainData.ParcelSize + 1;
            int meshVertexCount = sideVertexCount * sideVertexCount;

            var vertices = new NativeArray<Vertex>(meshVertexCount * state.dirtyParcels.Count,
                Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var generateVerticesJob = new GenerateVertices
            {
                terrainData = terrainData,
                parcels = parcels,
                vertices = vertices,
            };

            generateVerticesJob.Schedule(vertices.Length).Complete();

            for (var i = 0; i < state.dirtyParcels.Count; i++)
            {
                ParcelData parcelData = state.dirtyParcels[i];

                parcelData.mesh.SetVertexBufferData(vertices, i * meshVertexCount, 0, meshVertexCount,
                    0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            }

            var meshes = new NativeArray<int>(state.dirtyParcels.Count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            for (var i = 0; i < state.dirtyParcels.Count; i++)
                meshes[i] = state.dirtyParcels[i].mesh.GetInstanceID();

            var bakeMeshesJob = new BakeMeshes { meshes = meshes };
            bakeMeshesJob.Schedule(meshes.Length, 1).Complete();

            for (var i = 0; i < state.dirtyParcels.Count; i++)
            {
                ParcelData parcelData = state.dirtyParcels[i];

                // Needed even if the mesh is already assigned to the collider. Doing it will cause the
                // collider to check if the mesh has changed.
                parcelData.collider.sharedMesh = parcelData.mesh;
            }

            state.dirtyParcels.Clear();
        }

        private void OnDrawGizmos()
        {
            if (state == null)
                return;

            Gizmos.color = Color.magenta;

            for (var i = 0; i < state.usedParcels.Count; i++)
            {
                ParcelData parcelData = state.usedParcels[i];

                for (var j = 0; j < parcelData.trees.Count; j++)
                {
                    TreeInstance tree = parcelData.trees[j];

                    Gizmos.DrawWireSphere(tree.gameObject.transform.position,
                        state.terrainData.TreePrototypes[tree.prototypeIndex].Radius);
                }
            }
        }

        private static bool ContainsParcel(List<ParcelData> parcels, int2 parcel)
        {
            for (var i = 0; i < parcels.Count; i++)
                if (all(parcels[i].parcel == parcel))
                    return true;

            return false;
        }

        private static short[] CreateIndexBuffer(int parcelSize)
        {
            var index = 0;
            var indexBuffer = new short[parcelSize * parcelSize * 6];
            int sideVertexCount = parcelSize + 1;

            for (var z = 0; z < parcelSize; z++)
            {
                for (var x = 0; x < parcelSize; x++)
                {
                    int start = (z * sideVertexCount) + x;

                    indexBuffer[index++] = (short)start;
                    indexBuffer[index++] = (short)(start + sideVertexCount + 1);
                    indexBuffer[index++] = (short)(start + 1);

                    indexBuffer[index++] = (short)start;
                    indexBuffer[index++] = (short)(start + sideVertexCount);
                    indexBuffer[index++] = (short)(start + sideVertexCount + 1);
                }
            }

            return indexBuffer;
        }

        private static Mesh CreateParcelMesh(in TerrainDataData terrainData)
        {
            if (indexBuffer == null)
                indexBuffer = CreateIndexBuffer(terrainData.ParcelSize);

            var mesh = new Mesh { name = "Parcel Collision Mesh" };
            mesh.MarkDynamic();
            int sideVertexCount = terrainData.ParcelSize + 1;
            mesh.SetVertexBufferParams(sideVertexCount * sideVertexCount, vertexLayout);
            mesh.SetIndexBufferParams(indexBuffer.Length, IndexFormat.UInt16);
            mesh.subMeshCount = 1;

            MeshUpdateFlags flags = MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontNotifyMeshUsers
                                                                        | MeshUpdateFlags.DontRecalculateBounds;

            mesh.SetIndexBufferData(indexBuffer, 0, 0, indexBuffer.Length, flags);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexBuffer.Length), flags);

            var parcelMax = new Vector3(terrainData.ParcelSize, terrainData.maxHeight,
                terrainData.ParcelSize);

            mesh.bounds = new Bounds(parcelMax * 0.5f, parcelMax);

            return mesh;
        }

        private static void GenerateTrees(int2 parcel, in TerrainDataData terrainData,
            ParcelData parcelData, TerrainColliderState state)
        {
            if (terrainData.IsOccupied(parcel))
                return;

            Random random = terrainData.GetRandom(parcel);
            ReadOnlySpan<Terrain.TreeInstance> instances = terrainData.GetTreeInstances(parcel);

            for (var i = 0; i < instances.Length; i++)
            {
                if (!terrainData.TryGenerateTree(parcel, instances[i], out float3 position,
                        out float rotationY, out float scaleXZ, out float scaleY)) { continue; }

                Terrain.TreeInstance instance = instances[i];
                PrefabInstancePool pool = state.treePools[instance.PrototypeIndex];

                if (!pool.IsCreated)
                    continue;

                var tree = new TreeInstance
                {
                    prototypeIndex = instance.PrototypeIndex,
                    gameObject = pool.Get(),
                };

                Transform t = tree.gameObject.transform;
                t.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
                t.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

                parcelData.trees.Add(tree);
            }
        }

        [BurstCompile]
        private struct BakeMeshes : IJobParallelFor
        {
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<int> meshes;

            public void Execute(int index) =>
                Physics.BakeMesh(meshes[index], false, MeshColliderCookingOptions.UseFastMidphase);
        }

        [BurstCompile]
        private struct GenerateVertices : IJobParallelForBatch
        {
            public TerrainDataData terrainData;
            [ReadOnly] [DeallocateOnJobCompletion] public NativeArray<int2> parcels;
            [WriteOnly] public NativeArray<Vertex> vertices;

            public void Execute(int startIndex, int count)
            {
                int batchEnd = startIndex + count;
                int vertexIndex = startIndex;
                int sideVertexCount = terrainData.ParcelSize + 1;
                int meshVertexCount = sideVertexCount * sideVertexCount;
                int meshIndex = startIndex / meshVertexCount;

                while (vertexIndex < batchEnd)
                {
                    int2 parcel = parcels[meshIndex];
                    int2 parcelOriginXZ = parcel * terrainData.ParcelSize;
                    int meshStart = meshIndex * meshVertexCount;
                    int meshEnd = min(meshStart + meshVertexCount, batchEnd);

                    while (vertexIndex < meshEnd)
                    {
                        int x = (vertexIndex - meshStart) % sideVertexCount;
                        int z = (vertexIndex - meshStart) / sideVertexCount;
                        float y = terrainData.GetHeight(x + parcelOriginXZ.x, z + parcelOriginXZ.y);

                        var vertex = new Vertex { position = float3(x, y, z) };
#if UNITY_EDITOR
                        vertex.normal = terrainData.GetNormal(x, z);
#endif

                        vertices[vertexIndex] = vertex;
                        vertexIndex++;
                    }

                    meshIndex++;
                }
            }
        }

        private struct Vertex
        {
            public float3 position;
#if UNITY_EDITOR // Normals are only needed to draw the collider gizmo.
            public float3 normal;
#endif
        }

        [Serializable]
        internal sealed class ParcelData
        {
            public int2 parcel;
            public MeshCollider collider;
            public Mesh mesh;
            public List<TreeInstance> trees = new ();
        }

        [Serializable]
        internal struct TreeInstance
        {
            public int prototypeIndex;
            public GameObject gameObject;
        }
    }

    [Serializable]
    public sealed class TerrainColliderState : IDisposable
    {
        internal List<TerrainCollider.ParcelData> dirtyParcels = new ();
        internal List<TerrainCollider.ParcelData> freeParcels = new ();
        internal List<TerrainCollider.ParcelData> usedParcels = new ();
        internal Transform parent;
        internal TerrainData terrainData;
        internal PrefabInstancePool[] treePools;

        public TerrainColliderState(TerrainData terrainData, Transform parent = null)
        {
            this.terrainData = terrainData;
            this.parent = parent;
            treePools = new PrefabInstancePool[terrainData.TreePrototypes.Length];

            for (var i = 0; i < treePools.Length; i++)
            {
                GameObject collider = terrainData.TreePrototypes[i].Collider;

                if (collider != null)
                    treePools[i] = new PrefabInstancePool(collider, parent);
            }
        }

        public void Dispose()
        {
            for (var i = 0; i < treePools.Length; i++)
            {
                PrefabInstancePool pool = treePools[i];

                if (pool.IsCreated)
                    treePools[i].Dispose();
            }
        }
    }
}
