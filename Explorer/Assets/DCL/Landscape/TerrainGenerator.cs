using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
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
        private List<Terrain> terrains;
        private TreePrototype[] treePrototypes;
        private NativeHashMap<int2, EmptyParcelData> emptyParcelResult;
        private NativeArray<int2> emptyParcels;
        private NativeHashSet<int2> ownedParcels;
        private int maxHeightIndex;
        private Random random;
        private readonly NoiseGeneratorCache noiseGenCache;

        public TerrainGenerator(TerrainGenerationData terrainGenData, ref NativeArray<int2> emptyParcels, ref NativeHashSet<int2> ownedParcels)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            noiseGenCache = new NoiseGeneratorCache();
        }

        public void GenerateTerrain(uint worldSeed = 1, bool withHoles = true, bool centerTerrain = true)
        {
            random = new Random((uint)terrainGenData.seed);

            SetupEmptyParcelData(emptyParcels, ownedParcels);


            try
            {
                // Remove this debug thing
                rootGo = GameObject.Find("Generated Terrain");
                if (rootGo != null) Object.DestroyImmediate(rootGo);
                rootGo = new GameObject("Generated Terrain");

                GenerateCliffs();

                if (centerTerrain)
                    rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

                //rootGo.transform.position = new Vector3(-terrainGenData.terrainSize / 2f, 0, -terrainGenData.terrainSize / 2f);

                Dictionary<int2, TerrainData> terrainDatas = new ();

                for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
                for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
                    terrainDatas.Add(new int2(x, z), GenerateTerrainData(x, z, worldSeed));

                if (withHoles)
                    DigHoles(terrainDatas);

                GenerateChunks(terrainDatas);


            }
            catch (Exception e) { Debug.LogException(e); }
        }

        //           size,size
        //      N
        //    W + E
        //      S
        //0,0

        private void GenerateCliffs()
        {
            if (terrainGenData.cliffSide == null || terrainGenData.cliffCorner == null)
                return;

            CreateCliffCornerAt(new Vector3(terrainGenData.terrainSize, 0, terrainGenData.terrainSize), Quaternion.identity);
            CreateCliffCornerAt(new Vector3(terrainGenData.terrainSize, 0, 0), Quaternion.Euler(0, 90, 0));
            CreateCliffCornerAt(new Vector3(0, 0, 0), Quaternion.Euler(0, 180, 0));
            CreateCliffCornerAt(new Vector3(0, 0, terrainGenData.terrainSize), Quaternion.Euler(0, 270, 0));

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(terrainGenData.terrainSize, 0, i + 16);
                side.rotation = Quaternion.Euler(0, 90, 0);
                side.SetParent(rootGo.transform, true);
            }

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(i, 0, terrainGenData.terrainSize);
                side.rotation = Quaternion.identity;
                side.SetParent(rootGo.transform, true);
            }

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(0, 0, i);
                side.rotation = Quaternion.Euler(0, 270, 0);
                side.SetParent(rootGo.transform, true);
            }

            for (var i = 0; i < terrainGenData.terrainSize; i += 16)
            {
                Transform side = Object.Instantiate(terrainGenData.cliffSide).transform;
                side.position = new Vector3(i + 16, 0, 0);
                side.rotation = Quaternion.Euler(0, 180, 0);
                side.SetParent(rootGo.transform, true);
            }
        }

        private void CreateCliffCornerAt(Vector3 position, Quaternion rotation)
        {
            Transform neCorner = Object.Instantiate(terrainGenData.cliffCorner).transform;
            neCorner.position = position;
            neCorner.rotation = rotation;
            neCorner.SetParent(rootGo.transform, true);
        }

        private void SetupEmptyParcelData(NativeArray<int2> emptyParcels, NativeHashSet<int2> ownedParcels)
        {
            emptyParcelResult = new NativeHashMap<int2, EmptyParcelData>(emptyParcels.Length, Allocator.Persistent);

            foreach (int2 emptyParcel in emptyParcels)
                emptyParcelResult.Add(new int2(emptyParcel.x, emptyParcel.y), new EmptyParcelData());

            var job = new SetupEmptyParcels(in emptyParcels, in ownedParcels, ref emptyParcelResult) { heightNerf = terrainGenData.heightScaleNerf };
            JobHandle handle = job.Schedule();
            handle.Complete(); // not ideal

            maxHeightIndex = 0;

            foreach (KVPair<int2, EmptyParcelData> pair in emptyParcelResult)
                if (pair.Value.minIndex > maxHeightIndex)
                    maxHeightIndex = pair.Value.minIndex;
        }

        private void GenerateChunks(Dictionary<int2, TerrainData> terrainDatas)
        {
            var index = 0;
            terrains = new List<Terrain>();

            for (var z = 0; z < terrainGenData.terrainSize; z += terrainGenData.chunkSize)
            for (var x = 0; x < terrainGenData.terrainSize; x += terrainGenData.chunkSize)
            {
                TerrainData terrainData = terrainDatas[new int2(x, z)];
                terrains.Add(GenerateTerrainChunk(x, z, terrainData, terrainGenData.terrainMaterial));
                index++;
            }
        }

        private void DigHoles(Dictionary<int2, TerrainData> terrainDatas)
        {
            int resolution = terrainGenData.chunkSize;
            var failedHoles = 0;
            var goodHoles = 0;

            var parcelSizeHole = new bool[16, 16];

            for (var i = 0; i < 16; i++)
            for (var j = 0; j < 16; j++)
                parcelSizeHole[i, j] = false;

            foreach (int2 ownedParcel in ownedParcels)
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
                    TerrainData terrainData = terrainDatas[new int2(chunkX * resolution, chunkY * resolution)];
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
                                      detailPrototype.useInstancing = true;
                                      detailPrototype.renderMode = DetailRenderMode.VertexLit;
                                      detailPrototype.density = a.TerrainDetailSettings.detailDensity;
                                      detailPrototype.alignToGround = a.TerrainDetailSettings.alignToGround / 100f;
                                      detailPrototype.holeEdgePadding = a.TerrainDetailSettings.holeEdgePadding / 100f;
                                      detailPrototype.minWidth = a.TerrainDetailSettings.minWidth;
                                      detailPrototype.maxWidth = a.TerrainDetailSettings.maxWidth;
                                      detailPrototype.minHeight = a.TerrainDetailSettings.minHeight;
                                      detailPrototype.maxHeight = a.TerrainDetailSettings.maxHeight;
                                      detailPrototype.noiseSeed = a.TerrainDetailSettings.noiseSeed;
                                      detailPrototype.noiseSpread = a.TerrainDetailSettings.noiseSpread;
                                      detailPrototype.useDensityScaling = a.TerrainDetailSettings.affectedByGlobalDensityScale;
                                      detailPrototype.positionJitter = a.TerrainDetailSettings.positionJitter / 100f;
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

                INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(detailAsset.noiseData, baseSeed);

                JobHandle handle = noiseGenerator.Schedule(detailSize, offsetX, offsetZ);
                handle.Complete();

                for (var y = 0; y < detailSize; y++)
                {
                    for (var x = 0; x < detailSize; x++)
                    {
                        var f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? 255 : 16;
                        var index = x + (y * detailSize);
                        var value = noiseGenerator.GetValue(index);
                        detailLayer[y, x] = Mathf.FloorToInt(value * f); //random.NextInt(0,255);
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
            Debug.Log($"{offsetX}, {offsetZ}");

            for (var i = 0; i < terrainGenData.layerNoise.Count; i++)
            {
                NoiseData noiseData = terrainGenData.layerNoise[i];
                if (noiseData == null) continue;

                INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(noiseData, baseSeed);
                JobHandle handle = noiseGenerator.Schedule(chunkSize, offsetX, offsetZ);

                handle.Complete();

                float[,,] result3D = ConvertTo3DArray(noiseGenerator.GetResultCopy(), chunkSize, chunkSize);
                terrainData.SetAlphamaps(0, 0, result3D);
            }
        }


        private void SetTrees(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed)
        {
            var treeInstances = new NativeHashMap<int2, TreeInstance>(5000, Allocator.Persistent);
            for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
            {
                LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                NoiseDataBase treeNoiseData = treeAsset.noiseData;

                int chunkDensity = chunkSize; //Mathf.FloorToInt(chunkSize / 16f * treeAsset.density);

                INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                JobHandle handle = generator.Schedule(chunkDensity, offsetX, offsetZ);

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
            var array = new TreeInstance[treeInstances.Count];

            var index = 0;

            foreach (KVPair<int2, TreeInstance> treeInstance in treeInstances)
            {
                array[index] = treeInstance.Value;
                index++;
            }

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

                result[z, x, 0] = 1 - array[i];
                result[z, x, 1] = array[i];
            }

            return result;
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
