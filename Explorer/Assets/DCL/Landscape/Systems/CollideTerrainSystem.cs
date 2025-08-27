using Arch.Core;
using Arch.System;
using Arch.SystemGroups;
using DCL.Character.Components;
using DCL.CharacterMotion.Systems;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using ECS.Abstract;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Pool;
using UnityEngine.Rendering;
using static Unity.Mathematics.math;

namespace DCL.Landscape.Systems
{
    [LogCategory(ReportCategory.LANDSCAPE)]
    [UpdateInGroup(typeof(ChangeCharacterPositionGroup))]
    [UpdateAfter(typeof(InterpolateCharacterSystem))]
    public sealed partial class CollideTerrainSystem : BaseUnityLoopSystem
    {
        private readonly LandscapeData landscapeData;
        private readonly TerrainGenerator terrainGenerator;
        private readonly float collisionRadius;
        private readonly List<ParcelData> dirtyParcels;
        private readonly List<ParcelData> freeParcels;
        private readonly List<ParcelData> usedParcels;
        private readonly short[] indexBuffer;
        private readonly ObjectPool<GameObject>[] treePools;

        private static readonly VertexAttributeDescriptor[] VERTEX_LAYOUT =
        {
            new (VertexAttribute.Position)
#if UNITY_EDITOR // Only needed for drawing gizmos.
          , new (VertexAttribute.Normal)
#endif
        };

        private CollideTerrainSystem(World world, LandscapeData landscapeData,
            TerrainGenerator terrainGenerator) : base(world)
        {
            this.landscapeData = landscapeData;
            this.terrainGenerator = terrainGenerator;
            collisionRadius = landscapeData.terrainData.parcelSize * (1f / 3f);
            dirtyParcels = new List<ParcelData>();
            freeParcels = new List<ParcelData>();
            usedParcels = new List<ParcelData>();
            indexBuffer = CreateIndexBuffer(landscapeData.terrainData.parcelSize);
            treePools = new ObjectPool<GameObject>[landscapeData.terrainData.treeAssets.Length];

            for (int i = 0; i < treePools.Length; i++)
            {
                LandscapeAsset prototype = landscapeData.terrainData.treeAssets[i];
                GameObject? collider = prototype.Collider;
                Transform terrainRoot = terrainGenerator.TerrainRoot;

                if (collider != null)
                {
                    treePools[i] = new ObjectPool<GameObject>(
                        createFunc: () => Object.Instantiate(collider, terrainRoot),
                        actionOnDestroy: static instance => Object.Destroy(instance));
                }
            }
        }

        protected override void Update(float t)
        {
            if (!terrainGenerator.IsTerrainShown)
                return;

            ApplyCharacterPositionsQuery(World);

            if (dirtyParcels.Count == 0)
                return;

            var parcels = new NativeArray<int2>(dirtyParcels.Count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < dirtyParcels.Count; i++)
            {
                Vector2Int parcel = dirtyParcels[i].Parcel;
                parcels[i] = int2(parcel.x, parcel.y);
            }

            int sideVertexCount = landscapeData.terrainData.parcelSize + 1;
            int meshVertexCount = sideVertexCount * sideVertexCount;

            var vertices = new NativeArray<GroundColliderVertex>(meshVertexCount * dirtyParcels.Count,
                Allocator.TempJob, NativeArrayOptions.UninitializedMemory);

            var generateColliderVerticesJob = new GenerateColliderVertices()
            {
                Parcels = parcels,
                Vertices = vertices,
                MaxHeight = terrainGenerator.MaxHeight,
                OccupancyMap = terrainGenerator.OccupancyMap.GetRawTextureData<byte>(),
                OccupancyMapSize =  terrainGenerator.OccupancyMap.width
            };

            generateColliderVerticesJob.Schedule(vertices.Length).Complete();

            for (int i = 0; i < dirtyParcels.Count; i++)
            {
                ParcelData parcelData = dirtyParcels[i];

                parcelData.Mesh.SetVertexBufferData(vertices, i * meshVertexCount, 0, meshVertexCount,
                    0, MeshUpdateFlags.DontValidateIndices | MeshUpdateFlags.DontRecalculateBounds);
            }

            var meshes = new NativeArray<int>(dirtyParcels.Count, Allocator.TempJob,
                NativeArrayOptions.UninitializedMemory);

            for (int i = 0; i < dirtyParcels.Count; i++)
                meshes[i] = dirtyParcels[i].Mesh.GetInstanceID();

            var bakeColliderMeshesJob = new BakeColliderMeshes() { Meshes = meshes };
            bakeColliderMeshesJob.Schedule(meshes.Length, 1).Complete();

            // Needed even if the mesh is already assigned to the collider. Doing it will cause the
            // collider to check if the mesh has changed.
            foreach (var parcelData in dirtyParcels)
                parcelData.Collider.sharedMesh = parcelData.Mesh;

            dirtyParcels.Clear();
        }

        [Query, All(typeof(PlayerComponent))]
        private void ApplyCharacterPositions(CharacterTransform character)
        {
            RectInt usedRect = PositionToParcelRect(float3(character.Position).xz, collisionRadius);

            for (int i = usedParcels.Count - 1; i >= 0; i--)
            {
                ParcelData parcelData = usedParcels[i];

                if (!usedRect.Contains(parcelData.Parcel))
                {
                    usedParcels.RemoveAtSwapBack(i);
                    freeParcels.Add(parcelData);
                }
            }

            for (int y = usedRect.yMin; y < usedRect.yMax; y++)
            for (int x = usedRect.xMin; x < usedRect.xMax; x++)
            {
                Vector2Int parcel = new Vector2Int(x, y);

                if (ContainsParcel(usedParcels, parcel))
                    continue;

                if (freeParcels.Count > 0)
                {
                    ParcelData? parcelData = null;

                    // First, check if the exact parcel we need is in the free list already. If so,
                    // there's nothing to do.
                    for (int i = freeParcels.Count - 1; i >= 0; i--)
                    {
                        ParcelData freeParcel = freeParcels[i];

                        if (freeParcel.Parcel == parcel)
                        {
                            parcelData = freeParcel;
                            freeParcels.RemoveAtSwapBack(i);
                            usedParcels.Add(parcelData);
                            break;
                        }
                    }

                    // Else, reuse the last parcel in the free list.
                    if (parcelData == null)
                    {
                        int lastIndex = freeParcels.Count - 1;
                        parcelData = freeParcels[lastIndex];
                        parcelData.Parcel = parcel;
                        dirtyParcels.Add(parcelData);
                        freeParcels.RemoveAt(lastIndex);
                        usedParcels.Add(parcelData);

                        parcelData.Collider.transform.position = new Vector3(
                            parcel.x * landscapeData.terrainData.parcelSize, 0f,
                            parcel.y * landscapeData.terrainData.parcelSize);

                        foreach (TreeInstance tree in parcelData.Trees)
                            treePools[tree.PrototypeIndex].Release(tree.GameObject);

                        parcelData.Trees.Clear();
                        GenerateTrees(parcel, parcelData);
                    }
                }
                else
                {
                    Mesh mesh = CreateParcelMesh();

                    MeshCollider collider = new GameObject("Parcel Collider")
                       .AddComponent<MeshCollider>();

                    collider.cookingOptions = MeshColliderCookingOptions.UseFastMidphase;
                    Transform transform = collider.transform;

                    transform.position = new Vector3(parcel.x * landscapeData.terrainData.parcelSize,
                        0f, parcel.y * landscapeData.terrainData.parcelSize);

                    transform.SetParent(terrainGenerator.TerrainRoot, true);

                    var parcelData = new ParcelData(collider, mesh);
                    dirtyParcels.Add(parcelData);
                    usedParcels.Add(parcelData);

                    GenerateTrees(parcel, parcelData);
                }
            }
        }

        private static bool ContainsParcel(List<ParcelData> parcels, Vector2Int parcel)
        {
            foreach (var parcelData in parcels)
                if (parcelData.Parcel == parcel)
                    return true;

            return false;
        }

        private static short[] CreateIndexBuffer(int parcelSize)
        {
            int index = 0;
            short[] indexBuffer = new short[parcelSize * parcelSize * 6];
            int sideVertexCount = parcelSize + 1;

            for (int z = 0; z < parcelSize; z++)
            {
                for (int x = 0; x < parcelSize; x++)
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

        private Mesh CreateParcelMesh()
        {
            var mesh = new Mesh() { name = "Ground Collider" };
            mesh.MarkDynamic();
            int sideVertexCount = landscapeData.terrainData.parcelSize + 1;
            mesh.SetVertexBufferParams(sideVertexCount * sideVertexCount, VERTEX_LAYOUT);
            mesh.SetIndexBufferParams(indexBuffer.Length, IndexFormat.UInt16);
            mesh.subMeshCount = 1;

            const MeshUpdateFlags FLAGS = MeshUpdateFlags.DontValidateIndices
                                          | MeshUpdateFlags.DontNotifyMeshUsers
                                          | MeshUpdateFlags.DontRecalculateBounds;

            mesh.SetIndexBufferData(indexBuffer, 0, 0, indexBuffer.Length, FLAGS);
            mesh.SetSubMesh(0, new SubMeshDescriptor(0, indexBuffer.Length), FLAGS);

            Vector3 parcelMax = new Vector3(landscapeData.terrainData.parcelSize,
                terrainGenerator.MaxHeight, landscapeData.terrainData.parcelSize);

            mesh.bounds = new Bounds(parcelMax * 0.5f, parcelMax);

            return mesh;
        }

        private static void GenerateTrees(Vector2Int parcel, ParcelData parcelData)
        {
            /*if (terrainData.IsOccupied(parcel))
                return;

            Random random = terrainData.GetRandom(parcel);
            ReadOnlySpan<Terrain.TreeInstance> instances = terrainData.GetTreeInstances(parcel);

            for (int i = 0; i < instances.Length; i++)
            {
                if (!terrainData.TryGenerateTree(parcel, instances[i], out float3 position,
                        out float rotationY, out float scaleXZ, out float scaleY))
                {
                    continue;
                }

                Terrain.TreeInstance instance = instances[i];
                PrefabInstancePool pool = treePools[instance.PrototypeIndex];

                if (!pool.IsCreated)
                    continue;

                var tree = new TreeInstance()
                {
                    prototypeIndex = instance.PrototypeIndex,
                    gameObject = pool.Get()
                };

                var t = tree.gameObject.transform;
                t.SetPositionAndRotation(position, Quaternion.Euler(0f, rotationY, 0f));
                t.localScale = new Vector3(scaleXZ, scaleY, scaleXZ);

                parcelData.Trees.Add(tree);
            }*/
        }

        private RectInt PositionToParcelRect(float2 center, float radius)
        {
            float invParcelSize = 1f / landscapeData.terrainData.parcelSize;
            int2 min = (int2)floor((center - radius) * invParcelSize);
            int2 size = (int2)ceil((center + radius) * invParcelSize) - min;
            return new RectInt(min.x, min.y, size.x, size.y);
        }

        private sealed class ParcelData
        {
            public Vector2Int Parcel;
            public readonly MeshCollider Collider;
            public readonly Mesh Mesh;
            public readonly List<TreeInstance> Trees;

            public ParcelData(MeshCollider collider, Mesh mesh)
            {
                Collider = collider;
                Mesh = mesh;
                Trees = new List<TreeInstance>();
            }
        }

        private struct TreeInstance
        {
            public int PrototypeIndex;
            public GameObject GameObject;
        }
    }
}
