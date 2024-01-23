using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using JobHandle = Unity.Jobs.JobHandle;
using Object = UnityEngine.Object;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    public class TerrainGenerator : ITerrainGenerator, IDisposable
    {
        private readonly TerrainGenerationData terrainGenData;
        private GameObject rootGo;
        private Dictionary<NoiseData, NativeArray<float2>> octaves = new ();
        private List<Terrain> terrains;
        private TreePrototype[] treePrototypes;
        private NativeHashMap<Vector2Int, EmptyParcelData> emptyParcelResult;
        private NativeArray<Vector2Int> emptyParcels;
        private NativeHashSet<Vector2Int> ownedParcels;
        private int maxHeightIndex;
        private Random random;
        private Dictionary<INoiseDataFactory, INoiseGenerator> cachedGenerators;

        public TerrainGenerator(TerrainGenerationData terrainGenData, ref NativeArray<Vector2Int> emptyParcels, ref NativeHashSet<Vector2Int> ownedParcels)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
        }

        public void GenerateTerrain(uint worldSeed = 1, bool withHoles = true, bool centerTerrain = true)
        {
            random = new Random((uint)terrainGenData.seed);

            if (octaves != null)
                foreach (KeyValuePair<NoiseData, NativeArray<float2>> keyValuePair in octaves) { keyValuePair.Value.Dispose(); }

            octaves = new Dictionary<NoiseData, NativeArray<float2>>();

            SetupEmptyParcelData(emptyParcels, ownedParcels);

            foreach (NoiseData noise in terrainGenData.layerNoise)
            {
                if (noise != null)
                {
                    var octaveOffsets = new NativeArray<float2>(noise.settings.octaves, Allocator.Persistent);
                    Noise.CalculateOctaves(ref random, ref noise.settings, ref octaveOffsets);
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
                    terrainDatas.Add(new Vector2Int(x, z), GenerateTerrainData(x, z, worldSeed));

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
            terrain.detailObjectDistance = 200;
            terrain.enableHeightmapRayTracing = false;
            terrainObject.transform.position = new Vector3(offsetX, 0, offsetZ);
            terrainObject.transform.SetParent(rootGo.transform, false);
            return terrain;
        }

        // not completely optimized
        private TerrainData GenerateTerrainData(int offsetX, int offsetZ, uint baseSeed)
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
                detailPrototypes = GetDetailPrototypes(),
            };

            terrainData.SetDetailResolution(chunkSize, 32);

            SetHeights(offsetX, offsetZ, terrainData);
            SetTextures(offsetX, offsetZ, resolution, terrainData, baseSeed);

            SetTrees(offsetX, offsetZ, chunkSize, terrainData, baseSeed);
            SetDetails(offsetX, offsetZ, chunkSize, terrainData, baseSeed);

            return terrainData;
        }

        private DetailPrototype[] GetDetailPrototypes()
        {
            return terrainGenData.detailAssets.Select(a =>
                                  {
                                      var detailPrototype = new DetailPrototype();
                                      // TODO: CONFIGURE THIS FOR EACH PROTOTYPE
                                      detailPrototype.usePrototypeMesh = true;
                                      detailPrototype.prototype = a.asset;
                                      detailPrototype.density = a.radius;
                                      detailPrototype.useInstancing = true;
                                      detailPrototype.renderMode = DetailRenderMode.VertexLit;
                                      detailPrototype.density = 3;
                                      detailPrototype.alignToGround = 1;
                                      detailPrototype.holeEdgePadding = 0.75f;
                                      detailPrototype.minWidth = 1;
                                      detailPrototype.maxWidth = 1.5f;
                                      detailPrototype.minHeight = 1;
                                      detailPrototype.maxHeight = 4;
                                      detailPrototype.noiseSeed = 40;
                                      detailPrototype.noiseSpread = 172.4f;
                                      return detailPrototype;
                                  })
                                 .ToArray();
        }

        private void SetDetails(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            terrainData.SetDetailScatterMode(terrainGenData.detailScatterMode);

            var detailSize = chunkSize;

            for (int i = 0; i < terrainGenData.detailAssets.Length; i++)
            {
                var detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, i);

                LandscapeAsset detailAsset = terrainGenData.detailAssets[i];

                var noiseGenerator = GetGeneratorFor(detailAsset.noiseData, baseSeed);

                var handle = noiseGenerator.Schedule(detailSize, offsetZ, offsetX);
                handle.Complete();

                for (var y = 0; y < detailSize; y++)
                {
                    for (var x = 0; x < detailSize; x++)
                    {
                        var f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? 255 : 16;
                        var index = x + (y * detailSize);
                        var value = noiseGenerator.GetValue(index);
                        detailLayer[x, y] = Mathf.FloorToInt(value * f); //random.NextInt(0,255);
                    }
                }

                terrainData.SetDetailLayer(0, 0, i, detailLayer);
            }
        }

        private void SetHeights(int offsetX, int offsetZ, TerrainData terrainData)
        {
            int resolution = terrainGenData.chunkSize + 1;
            var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

            var modifyJob = new ModifyTerrainHeightJob(ref heights, in emptyParcelResult)
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

        private void SetTextures(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            for (var i = 0; i < terrainGenData.layerNoise.Count; i++)
            {
                NoiseData noiseData = terrainGenData.layerNoise[i];
                if (noiseData == null) continue;

                var noiseGenerator = GetGeneratorFor(noiseData, baseSeed);
                JobHandle handle = noiseGenerator.Schedule(chunkSize, offsetZ, offsetX);

                handle.Complete();

                float[,,] result3D = ConvertTo3DArray(noiseGenerator.GetResultCopy(), chunkSize, chunkSize);
                terrainData.SetAlphamaps(0, 0, result3D);
            }
        }

        private INoiseGenerator GetGeneratorFor(NoiseData noiseData, uint baseSeed)
        {
            if (noiseData == null)
                throw new Exception("Noise data is null, check the terrain generation data");

            cachedGenerators ??= new Dictionary<INoiseDataFactory, INoiseGenerator>();

            if (noiseData is not INoiseDataFactory bridge)
                throw new Exception("INoiseDataFactory not implemented?");

            if (cachedGenerators.TryGetValue(bridge, out INoiseGenerator noiseGen))
                return noiseGen;

            var generator = bridge.GetGenerator(baseSeed);
            cachedGenerators.Add(bridge, generator);

            return cachedGenerators[bridge];
        }

        private void SetTrees(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            var treeInstances = new NativeList<TreeInstance>(5000, Allocator.Persistent);

            for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
            {
                LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                NoiseData treeNoiseData = treeAsset.noiseData;

                int chunkDensity = chunkSize; //Mathf.FloorToInt(chunkSize / 16f * treeAsset.density);

                var generator = GetGeneratorFor(treeNoiseData, baseSeed);
                JobHandle handle = generator.Schedule(chunkDensity, offsetZ, offsetX);

                // TODO: NOT IDEAL!
                handle.Complete();

                NativeArray<float> resultCopy = generator.GetResultCopy();

                var treeInstancesJob = new GenerateTreeInstancesJob(
                    in resultCopy,
                    ref treeInstances,
                    in emptyParcelResult,
                    in treeAsset.randomization,
                    treeAsset.radius,
                    treeAssetIndex,
                    offsetX,
                    offsetZ,
                    chunkSize,
                    chunkDensity,
                    ref random);

                var h = treeInstancesJob.Schedule();

                // TODO: NOT IDEAL!
                h.Complete();
            }

            // We do a horrible array copy because that's what the terrain API expects, it is what it is
            var array = new TreeInstance[treeInstances.Length];

            for (var i = 0; i < treeInstances.Length; i++)
                array[i] = treeInstances[i];

            terrainData.SetTreeInstances(array, true);
            treeInstances.Dispose();
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

        public void Dispose()
        {
            foreach (KeyValuePair<INoiseDataFactory,INoiseGenerator> cachedGenerator in cachedGenerators)
                cachedGenerator.Value.Dispose();

            emptyParcelResult.Dispose();
            emptyParcels.Dispose();
            ownedParcels.Dispose();
        }
    }

    public interface ITerrainGenerator { }
}
