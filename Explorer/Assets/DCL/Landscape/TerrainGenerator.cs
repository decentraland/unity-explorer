using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Serialization;
using Debug = UnityEngine.Debug;
using JobHandle = Unity.Jobs.JobHandle;
using Object = UnityEngine.Object;
using Random = System.Random;

namespace DCL.Landscape
{
    public class TerrainGenerator : ITerrainGenerator, IDisposable
    {
        private readonly TerrainGenerationData terrainGenData;
        private GameObject rootGo;
        private Random random;
        private Dictionary<NoiseData, NativeArray<float2>> octaves = new ();
        private List<Terrain> terrains;
        private TreePrototype[] treePrototypes;
        private NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelResult;
        private NativeArray<Vector2Int> emptyParcels;
        private NativeHashSet<Vector2Int> ownedParcels;
        private int maxHeightIndex;

        public TerrainGenerator(TerrainGenerationData terrainGenData, ref NativeArray<Vector2Int> emptyParcels, ref NativeHashSet<Vector2Int> ownedParcels)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
        }

        public void GenerateTerrain(bool withHoles = true, bool centerTerrain = true)
        {
            random = new Random(terrainGenData.seed);

            if (octaves != null)
                foreach (KeyValuePair<NoiseData, NativeArray<float2>> keyValuePair in octaves) { keyValuePair.Value.Dispose(); }

            octaves = new Dictionary<NoiseData, NativeArray<float2>>();

            SetupEmptyParcelData(emptyParcels, ownedParcels);

            foreach (NoiseData noise in terrainGenData.layerNoise)
            {
                if (noise != null)
                {
                    var octaveOffsets = new NativeArray<float2>(noise.settings.octaves, Allocator.Persistent);
                    Noise.CalculateOctaves(random, ref noise.settings, ref octaveOffsets);
                    octaves.Add(noise, octaveOffsets);
                }
            }

            try
            {
                // Remove this debug thing
                rootGo = GameObject.Find("Generated Terrain");
                if (rootGo != null) Object.DestroyImmediate(rootGo);
                rootGo = new GameObject("Generated Terrain");

                if (centerTerrain)
                    rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

                //rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

                Dictionary<Vector2Int, TerrainData> terrainDatas = new ();

                for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
                for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
                    terrainDatas.Add(new Vector2Int(x, z), GenerateTerrainData(x, z));

                if (withHoles)
                    DigHoles(terrainDatas);

                GenerateChunks(terrainDatas);
            }
            catch (Exception e) { Debug.LogException(e); }
        }

        private void SetupEmptyParcelData(NativeArray<Vector2Int> emptyParcels, NativeHashSet<Vector2Int> ownedParcels)
        {
            emptyParcelResult = new NativeHashMap<Vector2Int, EmptyParcelData>(emptyParcels.Length, Allocator.Persistent);

            foreach (Vector2Int emptyParcel in emptyParcels)
                emptyParcelResult.Add(emptyParcel, new EmptyParcelData());

            var job = new SetupEmptyParcels(in emptyParcels, in ownedParcels, ref emptyParcelResult) { heightNerf = terrainGenData.heightScaleNerf };
            JobHandle handle = job.Schedule();
            handle.Complete(); // not ideal

            maxHeightIndex = 0;

            foreach (KVPair<Vector2Int, EmptyParcelData> pair in emptyParcelResult)
                if (pair.Value.minIndex > maxHeightIndex)
                    maxHeightIndex = pair.Value.minIndex;
        }

        private void GenerateChunks(Dictionary<Vector2Int, TerrainData> terrainDatas)
        {
            var index = 0;
            terrains = new List<Terrain>();

            for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
            for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
            {
                TerrainData terrainData = terrainDatas[new Vector2Int(x, z)];

                //terrainData.SyncHeightmap();
                //terrainData.SyncTexture(TerrainData.HolesTextureName);

                terrains.Add(GenerateTerrainChunk(x, z, terrainData, terrainGenData.terrainMaterial));
                index++;
            }
        }

        private void DigHoles(Dictionary<Vector2Int, TerrainData> terrainDatas)
        {
            int resolution = terrainGenData.chunkSize;
            var failedHoles = 0;
            var goodHoles = 0;

            var parcelSizeHole = new bool[16, 16];

            for (var i = 0; i < 16; i++)
            for (var j = 0; j < 16; j++)
                parcelSizeHole[i, j] = false;

            foreach (Vector2Int ownedParcel in ownedParcels)
            {
                int parcelGlobalX = (150 + ownedParcel.x) * 16;
                int parcelGlobalY = (150 + ownedParcel.y) * 16;

                // Calculate the terrain chunk index for the parcel
                int chunkX = Mathf.FloorToInt((float)parcelGlobalX / resolution);
                int chunkY = Mathf.FloorToInt((float)parcelGlobalY / resolution);

                // Calculate the position within the terrain chunk
                int localX = parcelGlobalX - (chunkX * resolution);
                int localY = parcelGlobalY - (chunkY * resolution);

                try
                {
                    TerrainData terrainData = terrainDatas[new Vector2Int(chunkX * resolution, chunkY * resolution)];
                    terrainData.SetHoles(localX, localY, parcelSizeHole);
                    goodHoles++;
                }
                catch (Exception e) { failedHoles++; }
            }

            Debug.Log($"Holes digged {goodHoles}");

            if (failedHoles > 0) { Debug.LogError($"Failed to set {failedHoles} holes"); }
        }

        private Terrain GenerateTerrainChunk(int offsetX, int offsetZ, TerrainData terrainData, Material material)
        {
            //terrainData.SyncTexture(TerrainData.HolesTextureName);
            GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
            Terrain terrain = terrainObject.GetComponent<Terrain>();
            terrain.shadowCastingMode = ShadowCastingMode.Off;
            terrain.materialTemplate = material;
            terrainObject.transform.position = new Vector3(offsetX, 0, offsetZ);
            terrainObject.transform.SetParent(rootGo.transform, false);
            return terrain;
        }

        // not completely optimized
        private TerrainData GenerateTerrainData(int offsetX, int offsetZ)
        {
            int resolution = terrainGenData.chunkSize;
            int chunkSize = terrainGenData.chunkSize;

            var terrainData = new TerrainData
            {
                heightmapResolution = resolution,
                alphamapResolution = resolution,
                size = new Vector3(chunkSize, maxHeightIndex, chunkSize),
                terrainLayers = terrainGenData.terrainLayers,

                //enableHolesTextureCompression = true,
                treePrototypes = GetTreePrototypes(),
            };

            SetHeights(offsetX, offsetZ, terrainData);
            SetTextures(offsetX, offsetZ, resolution, terrainData);

            SetTrees(offsetX, offsetZ, chunkSize, terrainData);

            return terrainData;
        }

        private void SetHeights(int offsetX, int offsetZ, TerrainData terrainData)
        {
            int resolution = terrainGenData.chunkSize + 1;
            var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

            var modifyJob = new ModifyTerrainJob(ref heights, in emptyParcelResult)
            {
                terrainWidth = resolution,
                offsetX = offsetX,
                offsetZ = offsetZ,
                terrainScale = terrainGenData.terrainScale,
                maxHeight = maxHeightIndex,
            };

            JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64);
            jobHandle.Complete();
            float[,] heightArray = ConvertTo2DArray(heights, resolution, resolution);
            terrainData.SetHeights(0, 0, heightArray);
            heights.Dispose();
        }

        private void SetTextures(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData)
        {
            for (var i = 0; i < terrainGenData.layerNoise.Count; i++)
            {
                NoiseData noiseData = terrainGenData.layerNoise[i];
                if (noiseData == null) continue;

                var result = new NativeArray<float>(chunkSize * chunkSize, Allocator.TempJob);
                var offset = new float2(offsetZ, offsetX);
                var noiseJob = new NoiseJob(ref result, octaves[noiseData], chunkSize, chunkSize, noiseData.settings, 1, offset, NoiseJobOperation.SET);
                JobHandle handle = noiseJob.Schedule(chunkSize * chunkSize, 32);

                handle.Complete();

                float[,,] result3D = ConvertTo3DArray(result, chunkSize, chunkSize);
                terrainData.SetAlphamaps(0, 0, result3D);
                result.Dispose();
            }
        }

        private Vector2Int WorldToParcelCoord(Vector3 worldPos)
        {
            int parcelX = Mathf.FloorToInt(worldPos.x / 16f);
            int parcelZ = Mathf.FloorToInt(worldPos.z / 16f);
            return new Vector2Int(-150 + parcelX, -150 + parcelZ);
        }

        private Vector3 ParcelToWorld(Vector2Int parcel)
        {
            int posX = (parcel.x + 150) * 16;
            int posZ = (parcel.y + 150) * 16;
            return new Vector3(posX, 0, posZ);
        }

        private void SetTrees(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData)
        {
            // this should run in another job, but well, lets do it quick
            var treeInstances = new List<TreeInstance>();

            for (var i = 0; i < terrainGenData.treeAssets.Length; i++)
            {
                LandscapeAsset treeAsset = terrainGenData.treeAssets[i];
                NoiseData treeNoiseData = treeAsset.noiseData;

                var treeOctaves = new NativeArray<float2>(treeNoiseData.settings.octaves, Allocator.Persistent);
                Noise.CalculateOctaves(random, ref treeNoiseData.settings, ref treeOctaves);

                int chunkDensity = Mathf.FloorToInt(chunkSize / 16f * treeAsset.density);

                var result = new NativeArray<float>(chunkDensity * chunkDensity, Allocator.TempJob);
                var offset = new float2(offsetZ, offsetX);
                var noiseJob = new NoiseJob(ref result, treeOctaves, chunkDensity, chunkDensity, treeNoiseData.settings, 1, offset, NoiseJobOperation.SET);
                JobHandle handle = noiseJob.Schedule(chunkDensity * chunkDensity, 32);
                handle.Complete(); // TODO: NOT IDEAL!
                var onlyOne = false;

                // TODO: JOBIFY THIS
                for (var y = 0; y < chunkDensity; y++)
                {
                    if (onlyOne)
                        break;

                    for (var x = 0; x < chunkDensity; x++)
                    {
                        if (onlyOne)
                            break;

                        int index = x + (y * chunkDensity);
                        float value = result[index];

                        Vector3 randomness = treeAsset.randomization.GetRandomizedPositionOffset(random) / chunkDensity;
                        Vector3 positionWithinTheChunk = new Vector3((float)x / chunkDensity, 0, (float)y / chunkDensity) + randomness;
                        Vector3 worldPosition = (positionWithinTheChunk * chunkSize) + new Vector3(offsetX, 0, offsetZ);
                        Vector2Int parcelCoord = WorldToParcelCoord(worldPosition);
                        Vector3 parcelWorldPos = ParcelToWorld(parcelCoord);

                        if (value > 0 && emptyParcelResult.TryGetValue(parcelCoord, out EmptyParcelData item))
                        {
                            Vector2 randomScale = treeAsset.randomization.randomScale;
                            float scale = Mathf.Lerp(randomScale.x, randomScale.y, random.Next(0, 100) / 100f);

                            float radius = treeAsset.radius * scale;

                            bool u = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.up, 0, radius);
                            bool ur = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.up + Vector2Int.right, 0, radius);
                            bool r = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.right, 0, radius);
                            bool rd = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.right + Vector2Int.down, 0, radius);
                            bool d = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.down, 0, radius);
                            bool dl = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.down + Vector2Int.left, 0, radius);
                            bool l = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.left, 0, radius);
                            bool lu = CheckAssetPosition(item, parcelCoord, parcelWorldPos, worldPosition, Vector2Int.left + Vector2Int.up, 0, radius);

                            //onlyOne = true;

                            if (!u || !ur || !r || !rd || !d || !dl || !l || !lu)
                                continue;

                            Vector2 randomRotation = treeAsset.randomization.randomRotationY * Mathf.Deg2Rad;
                            float rotation = Mathf.Lerp(randomRotation.x, randomRotation.y, random.Next(0, 100) / 100f);

                            var treeInstance = new TreeInstance
                            {
                                position = positionWithinTheChunk,
                                prototypeIndex = i,
                                rotation = rotation,
                                widthScale = scale * value,
                                heightScale = scale * value,
                                color = Color.white,
                                lightmapColor = Color.white,
                            };

                            treeInstances.Add(treeInstance);
                        }
                    }
                }

                treeOctaves.Dispose();
                result.Dispose();
            }

            terrainData.SetTreeInstances(treeInstances.ToArray(), true);
        }

        private bool CheckAssetPosition(EmptyParcelData item, Vector2Int currentParcel, Vector3 parcelWorldPos, Vector3 assetPosition, Vector2Int direction,
            int depth, float radius)
        {
            if (GetHeightDirection(item, direction) >= 0)
            {
                int nextDepth = depth + 1;

                if (emptyParcelResult.TryGetValue(currentParcel + (direction * nextDepth), out EmptyParcelData parcel))
                    return CheckAssetPosition(parcel, currentParcel, parcelWorldPos, assetPosition, direction, nextDepth, radius);
            }
            else
            {
                var v3Dir = new Vector3(direction.x, 0, direction.y);
                Vector3 posToCheck = parcelWorldPos + (v3Dir * 8f) + (depth * v3Dir * 16);
                float distance = Vector3.Distance(assetPosition, posToCheck);
                return distance > radius;
            }

            return false;
        }

        private int GetHeightDirection(EmptyParcelData item, Vector2Int dir)
        {
            if (dir == Vector2Int.up) return item.upHeight;
            if (dir == Vector2Int.up + Vector2Int.right) return item.upRigthHeight;
            if (dir == Vector2Int.right) return item.rightHeight;
            if (dir == Vector2Int.right + Vector2Int.down) return item.downRightHeight;
            if (dir == Vector2Int.down) return item.downHeight;
            if (dir == Vector2Int.down + Vector2Int.left) return item.downLeftHeight;
            if (dir == Vector2Int.left) return item.leftHeight;
            if (dir == Vector2Int.left + Vector2Int.up) return item.upLeftHeight;
            return -1;
        }

        private TreePrototype[] GetTreePrototypes()
        {
            if (treePrototypes != null)
                return treePrototypes;

            treePrototypes = terrainGenData.treeAssets.Select(t => new TreePrototype
                                            {
                                                prefab = t.asset,
                                            })
                                           .ToArray();

            return treePrototypes;
        }

        // Convert flat NativeArray to a 2D array (is there another way?)
        private float[,] ConvertTo2DArray(NativeArray<float> array, int width, int height)
        {
            var result = new float[width, height];

            for (var i = 0; i < array.Length; i++)
            {
                int x = i % width;
                int z = i / width;
                result[z, x] = array[i];
            }

            return result;
        }

        // TODO: RE DO THIS TO SUPPORT MORE LAYERS
        private float[,,] ConvertTo3DArray(NativeArray<float> array, int width, int height)
        {
            var result = new float[width, height, terrainGenData.terrainLayers.Length];

            for (var i = 0; i < array.Length; i++)
            {
                int x = i % width;
                int z = i / width;

                result[x, z, 0] = 1 - array[i];
                result[x, z, 1] = array[i];
            }

            return result;
        }

        private struct ModifyTerrainJob : IJobParallelFor
        {
            private NativeArray<float> heights;
            [ReadOnly] private NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelData;
            public int terrainWidth;
            public int offsetX;
            public int offsetZ;
            public int maxHeight;
            public float terrainScale;

            public ModifyTerrainJob(ref NativeArray<float> heights, in NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelData) : this()
            {
                this.heights = heights;
                this.emptyParcelData = emptyParcelData;
            }

            public void Execute(int index)
            {
                int x = index % terrainWidth;
                int z = index / terrainWidth;

                int worldX = x + offsetX; // * 1f / terrainScale;
                int worldZ = z + offsetZ; // * 1f / terrainScale;

                int parcelX = worldX / 16;
                int parcelZ = worldZ / 16;

                var coord = new Vector2Int(-150 + parcelX, -150 + parcelZ);

                if (emptyParcelData.TryGetValue(coord, out EmptyParcelData data))
                {
                    float noise = Mathf.PerlinNoise((x + offsetX) * 1f / terrainScale, (z + offsetZ) * 1f / terrainScale);
                    float currentHeight = data.minIndex;

                    float lx = x % 16 / 16f;
                    float lz = z % 16 / 16f;

                    float lxRight = (lx - 0.5f) * 2;
                    float lxLeft = lx * 2;

                    float lzUp = (lz - 0.5f) * 2;
                    float lzDown = lz * 2;

                    float xLerp = lx >= 0.5f
                        ? math.lerp(currentHeight, data.rightHeight, lxRight)
                        : math.lerp(data.leftHeight, currentHeight, lxLeft);

                    float zLerp = lz >= 0.5f
                        ? math.lerp(currentHeight, data.upHeight, lzUp)
                        : math.lerp(data.downHeight, currentHeight, lzDown);

                    float corner = currentHeight;

                    if (lx >= 0.5f && lz >= 0.5f) // up right
                        corner = math.min(
                            math.lerp(currentHeight, data.upRigthHeight, lxRight),
                            math.lerp(currentHeight, data.upRigthHeight, lzUp));

                    if (lx < 0.5f && lz >= 0.5f) // up left
                        corner = math.min(
                            math.lerp(data.upLeftHeight, currentHeight, lxLeft),
                            math.lerp(currentHeight, data.upLeftHeight, lzUp));

                    if (lx >= 0.5f && lz < 0.5f) // down right
                        corner = math.min(
                            math.lerp(currentHeight, data.downRightHeight, lxRight),
                            math.lerp(data.downRightHeight, currentHeight, lzDown));

                    if (lx < 0.5f && lz < 0.5f) // down left
                        corner = math.min(
                            math.lerp(data.downLeftHeight, currentHeight, lxLeft),
                            math.lerp(data.downLeftHeight, currentHeight, lzDown));

                    float finalHeight = math.max(math.max(corner, math.max(xLerp, zLerp)), currentHeight);
                    heights[index] = finalHeight * noise / maxHeight;

                    //heights[index] = currentHeight / maxHeight;
                }
                else
                    heights[index] = 0;
            }
        }

        public struct EmptyParcelData
        {
            public int downHeight;
            public int upHeight;
            public int leftHeight;
            public int rightHeight;

            public int downLeftHeight;
            public int downRightHeight;
            public int upLeftHeight;
            public int upRigthHeight;

            public int minIndex;
        }

        [BurstCompile]

        // not a parallel job since NativeHashMap does not support parallel write, we need to figure out a better way of doing this
        private struct SetupEmptyParcels : IJob
        {
            [ReadOnly] private readonly NativeArray<Vector2Int> emptyParcels;
            [ReadOnly] private NativeHashSet<Vector2Int> ownedParcels;
            private NativeHashMap<Vector2Int, EmptyParcelData> result;
            public float heightNerf;

            public SetupEmptyParcels(in NativeArray<Vector2Int> emptyParcels, in NativeHashSet<Vector2Int> ownedParcels, ref NativeHashMap<Vector2Int, EmptyParcelData> result)
            {
                this.emptyParcels = emptyParcels;
                this.ownedParcels = ownedParcels;
                this.result = result;
                heightNerf = 0;
            }

            public void Execute()
            {
                // first calculate the base height
                foreach (Vector2Int position in emptyParcels)
                {
                    EmptyParcelData data = result[position];

                    data.minIndex = (int)Empower(GetNearestParcelDistance(position, 1));
                    result[position] = data;
                }

                // then get all the neighbour heights
                foreach (Vector2Int position in emptyParcels)
                {
                    EmptyParcelData data = result[position];

                    data.leftHeight = SafeGet(position + Vector2Int.left);
                    data.rightHeight = SafeGet(position + Vector2Int.right);
                    data.upHeight = SafeGet(position + Vector2Int.up);
                    data.downHeight = SafeGet(position + Vector2Int.down);

                    data.upLeftHeight = SafeGet(position + Vector2Int.up + Vector2Int.left);
                    data.upRigthHeight = SafeGet(position + Vector2Int.up + Vector2Int.right);
                    data.downLeftHeight = SafeGet(position + Vector2Int.down + Vector2Int.left);
                    data.downRightHeight = SafeGet(position + Vector2Int.down + Vector2Int.right);

                    result[position] = data;
                }
            }

            private float Empower(float height) =>
                height / heightNerf;

            //math.pow(height, 2f) / heightNerf;

            private int SafeGet(Vector2Int pos)
            {
                if (IsOutOfBounds(pos.x) || IsOutOfBounds(pos.y) || ownedParcels.Contains(pos))
                    return -1;

                return result[pos].minIndex;
            }

            private int GetNearestParcelDistance(Vector2Int emptyParcelCoords, int radius)
            {
                for (int x = -radius; x <= radius; x++)
                {
                    for (int y = -radius; y <= radius; y++)
                    {
                        var direction = new Vector2Int(x, y);
                        Vector2Int nextPos = emptyParcelCoords + direction;

                        if (IsOutOfBounds(nextPos.x) || IsOutOfBounds(nextPos.y))
                            return radius - 1;

                        if (ownedParcels.Contains(nextPos))
                            return radius - 1;
                    }
                }

                return GetNearestParcelDistance(emptyParcelCoords, radius + 1);
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static bool IsOutOfBounds(int value) =>
                value is > 150 or < -150;
        }

        public void Dispose()
        {
            emptyParcelResult.Dispose();
            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }
    }

    public interface ITerrainGenerator { }
}
