using Cysharp.Threading.Tasks;
using DCL.AsyncLoadReporting;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Utility;
using Object = UnityEngine.Object;
using Random = Unity.Mathematics.Random;

namespace DCL.Landscape
{
    public class WorldTerrainGenerator
    {
        private const int PARCEL_SIZE = 16;

        private const int UNITY_MAX_COVERAGE_VALUE = 255;
        private const int UNITY_MAX_INSTANCE_COUNT = 16;

        private const string TERRAIN_OBJECT_NAME = "World Generated Terrain";

        private readonly TerrainGenerationData terrainGenData;
        private readonly NoiseGeneratorCache noiseGenCache = new ();
        private readonly List<Terrain> terrains = new ();
        private readonly List<Transform> cliffs = new ();

        private GameObject rootGo;

        private int maxHeightIndex;
        private uint worldSeed;
        private TreePrototype[] treePrototypes;

        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborHeightsData;
        private NativeParallelHashMap<int2, int> emptyParcelHeights;
        private NativeArray<int2> emptyParcels;

        public Transform Ocean { get; private set; }

        public WorldTerrainGenerator(TerrainGenerationData terrainGenData)
        {
            this.terrainGenData = terrainGenData;
        }

        public void SwitchVisibility(bool isVisible)
        {
            if (rootGo != null)
            {
                if (!isVisible)
                {
                    emptyParcels.Dispose();
                    emptyParcelHeights.Dispose();
                    emptyParcelNeighborHeightsData.Dispose();
                }

                rootGo.SetActive(isVisible);
            }
        }

        public async UniTask GenerateTerrainAsync(NativeParallelHashSet<int2> ownedParcels, uint worldSeed = 1, CancellationToken cancellationToken = default)
        {
            rootGo = InstantiateSingletonTerrainRoot();

            this.worldSeed = worldSeed;
            var worldModel = new WorldModel(ownedParcels);
            var terrainModel = new TerrainModel(worldModel, 2 + Mathf.RoundToInt(0.1f * (worldModel.sizeInParcels.x + worldModel.sizeInParcels.y) / 2f));

            GenerateCliffs(terrainModel, terrainGenData.cliffSide, terrainGenData.cliffCorner);
            SpawnMiscAsync();
            GenerateBorderColliders(terrainModel);

            // Extract empty parcels
            {
                var tempEmptyParcels = new List<int2>();

                for (int x = terrainModel.minParcel.x; x <= terrainModel.maxParcel.x; x++)
                for (int y = terrainModel.minParcel.y; y <= terrainModel.maxParcel.y; y++)
                {
                    var currentParcel = new int2(x, y);

                    if (!ownedParcels.Contains(currentParcel))
                        tempEmptyParcels.Add(currentParcel);
                }

                // Now convert the list to NativeArray<int2>
                emptyParcels = new NativeArray<int2>(tempEmptyParcels.Count, Allocator.Persistent);

                for (var i = 0; i < tempEmptyParcels.Count; i++)
                    emptyParcels[i] = tempEmptyParcels[i];
            }

            // SetupEmptyParcelHeightDataAsync
            {
                emptyParcelHeights = new NativeParallelHashMap<int2, int>(emptyParcels.Length, Allocator.Persistent);
                emptyParcelNeighborHeightsData = new NativeParallelHashMap<int2, EmptyParcelNeighborData>(emptyParcels.Length, Allocator.Persistent);

                var job = new CalculateEmptyParcelBaseHeightJob(in emptyParcels, ownedParcels.AsReadOnly(), emptyParcelHeights.AsParallelWriter(),
                    terrainGenData.heightScaleNerf, terrainModel.minParcel, terrainModel.maxParcel);

                JobHandle handle = job.Schedule(emptyParcels.Length, 32);

                var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelNeighborHeightsData.AsParallelWriter(),
                    emptyParcelHeights.AsReadOnly(), terrainModel.minParcel, terrainModel.maxParcel);

                JobHandle handle2 = job2.Schedule(emptyParcels.Length, 32, handle);

                await handle2.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

                // Calculate this outside the jobs since they are Parallel
                foreach (KeyValue<int2, int> emptyParcelHeight in emptyParcelHeights)
                    if (emptyParcelHeight.Value > maxHeightIndex)
                        maxHeightIndex = emptyParcelHeight.Value;
            }

            // Generate TerrainData's
            foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
            {
                chunkModel.TerrainData = new TerrainData
                {
                    heightmapResolution = terrainModel.ChunkSizeInUnits + 1,
                    alphamapResolution = terrainModel.ChunkSizeInUnits,
                    size = new Vector3(terrainModel.ChunkSizeInUnits, maxHeightIndex, terrainModel.ChunkSizeInUnits),
                    terrainLayers = terrainGenData.terrainLayers,

                    treePrototypes = GetTreePrototypes(),
                    detailPrototypes = GetDetailPrototypes(),
                };

                chunkModel.TerrainData.SetDetailResolution(terrainModel.ChunkSizeInUnits, 32);

                // SetHeightsAsync(terrainModel, chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, chunkModel.TerrainData, worldSeed, cancellationToken).Forget();
                SetTreesAsync(chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, terrainModel.minParcel, cancellationToken).Forget();
                SetDetailsAsync(chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken).Forget();
                SetTexturesAsync(chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken).Forget();

                // Dig Holes
                {
                    var holes = new bool[terrainModel.ChunkSizeInUnits, terrainModel.ChunkSizeInUnits];

                    for (var x = 0; x < terrainModel.ChunkSizeInUnits; x++)
                    for (var y = 0; y < terrainModel.ChunkSizeInUnits; y++)
                        holes[x, y] = true;

                    if (chunkModel.OccupiedParcels.Count > 0)
                        foreach (int2 parcel in chunkModel.OccupiedParcels)
                        {
                            int x = (parcel.x - chunkModel.MinParcel.x) * PARCEL_SIZE;
                            int y = (parcel.y - chunkModel.MinParcel.y) * PARCEL_SIZE;

                            for (int i = x; i < x + PARCEL_SIZE; i++)
                            for (int j = y; j < y + PARCEL_SIZE; j++)
                                holes[j, i] = false;
                        }

                    if (chunkModel.OutOfTerrainParcels.Count > 0)
                        foreach (int2 parcel in chunkModel.OutOfTerrainParcels)
                        {
                            int x = (parcel.x - chunkModel.MinParcel.x) * PARCEL_SIZE;
                            int y = (parcel.y - chunkModel.MinParcel.y) * PARCEL_SIZE;

                            for (int i = x; i < x + PARCEL_SIZE; i++)
                            for (int j = y; j < y + PARCEL_SIZE; j++)
                                holes[j, i] = false;
                        }

                    chunkModel.TerrainData.SetHoles(0, 0, holes);
                }

                await UniTask.Yield(cancellationToken);
            }

            // Generate Terrain GameObjects
            foreach (ChunkModel chunkModel in terrainModel.ChunkModels)
            {
                Terrain terrain = Terrain.CreateTerrainGameObject(chunkModel.TerrainData).GetComponent<Terrain>();
                terrain.shadowCastingMode = ShadowCastingMode.Off;
                terrain.materialTemplate = terrainGenData.terrainMaterial;
                terrain.detailObjectDistance = 200;
                terrain.enableHeightmapRayTracing = false;
                terrain.drawHeightmap = true; // forced to true for the color map renderer
                terrain.drawTreesAndFoliage = true;

                terrain.transform.position = new Vector3(chunkModel.MinParcel.x * PARCEL_SIZE, -terrainGenData.minHeight, chunkModel.MinParcel.y * PARCEL_SIZE);
                terrain.transform.SetParent(rootGo.transform, false);

                terrains.Add(terrain);
                await UniTask.Yield();
            }

            ownedParcels.Dispose();
        }

        private void GenerateBorderColliders(TerrainModel terrainModel)
        {
            var collidersRoot = new GameObject("BorderColliders");
            collidersRoot.transform.SetParent(rootGo.transform);

            var height = 50.0f; // Height of the collider
            var thickness = 10.0f; // Thickness of the collider

            // Offsets are 21 units beyond the cliff
            var offset = 21.0f;
            // Extend the colliders slightly to ensure corners are fully closed
            float extension = offset + (thickness / 2);

            // Create colliders along each side of the terrain
            AddCollider(terrainModel.minInUnits.x, terrainModel.minInUnits.y, terrainModel.sizeInUnits.x, "North Border Collider", 0);
            // AddCollider(terrainModel.minInUnits.x - extension, terrainModel.maxInUnits.y + offset, terrainModel.maxInUnits.x - terrainModel.minInUnits.x + (2 * extension), colliderHeight, colliderThickness, "South Border Collider", 0);
            // AddCollider(terrainModel.minInUnits.x - offset, terrainModel.minInUnits.y - extension, terrainModel.maxInUnits.y - terrainModel.minInUnits.y + (2 * extension), colliderHeight, colliderThickness, "East Border Collider", 90);
            // AddCollider(terrainModel.maxInUnits.x + offset, terrainModel.minInUnits.y - extension, terrainModel.maxInUnits.y - terrainModel.minInUnits.y + (2 * extension), colliderHeight, colliderThickness, "West Border Collider", 90);

            void AddCollider(float posX, float posY, float length, string name, float rotation)
            {
                var colliderGo = new GameObject(name);
                colliderGo.transform.SetParent(collidersRoot.transform);
                BoxCollider collider = colliderGo.AddComponent<BoxCollider>();

                collider.size = new Vector3(length, height, thickness);
                collider.center = new Vector3(posX + (length / 2), height / 2, posY + (thickness / 2) - 26);

                // Set rotation
                colliderGo.transform.rotation = Quaternion.Euler(0, rotation, 0);
            }
        }

        private async UniTask SetHeightsAsync(TerrainModel terrainModel, int offsetX, int offsetZ, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            {
                int resolution = terrainModel.ChunkSizeInUnits + 1;
                var heights = new NativeArray<float>(resolution * resolution, Allocator.TempJob);

                INoiseGenerator terrainHeightNoise = noiseGenCache.GetGeneratorFor(terrainGenData.terrainHeightNoise, baseSeed);
                var noiseDataPointer = new NoiseDataPointer(resolution, offsetX, offsetZ);
                JobHandle handle = terrainHeightNoise.Schedule(noiseDataPointer, default(JobHandle));

                NativeArray<float> terrainNoise = terrainHeightNoise.GetResult(noiseDataPointer);

                var modifyJob = new ModifyTerrainHeightJob(
                    ref heights,
                    in emptyParcelNeighborHeightsData, in emptyParcelHeights,
                    in terrainNoise,
                    terrainGenData.terrainHoleEdgeSize,
                    terrainGenData.minHeight,
                    terrainGenData.pondDepth,
                    resolution,
                    offsetX,
                    offsetZ,
                    maxHeightIndex,
                    terrainModel.minParcel,
                    PARCEL_SIZE
                );

                JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64, handle);

                try
                {
                    await jobHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

                    {
                        float[,] heightArray = ConvertTo2DArray(heights, resolution, resolution);
                        terrainData.SetHeights(0, 0, heightArray);
                    }
                }
                finally { heights.Dispose(); }
            }
        }

        private async UniTask SetTexturesAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            {
                cancellationToken.ThrowIfCancellationRequested();
                var noiseGenerators = new List<INoiseGenerator>();
                var noiseTasks = new List<UniTask>();
                var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);

                foreach (NoiseData noiseData in terrainGenData.layerNoise)
                {
                    if (noiseData == null) continue;

                    INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(noiseData, baseSeed);
                    JobHandle handle = noiseGenerator.Schedule(noiseDataPointer, default(JobHandle));

                    UniTask noiseTask = handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    noiseTasks.Add(noiseTask);
                    noiseGenerators.Add(noiseGenerator);
                }

                await UniTask.WhenAll(noiseTasks).AttachExternalCancellation(cancellationToken);

                {
                    float[,,] result3D = GenerateAlphaMaps(noiseGenerators.Select(ng => ng.GetResult(noiseDataPointer)).ToArray(), chunkSize, chunkSize);
                    terrainData.SetAlphamaps(0, 0, result3D);
                }
            }
        }

        private async UniTask SetDetailsAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            terrainData.SetDetailScatterMode(terrainGenData.detailScatterMode);

            {
                var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);
                var generators = new List<INoiseGenerator>();

                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var tasks = new List<UniTask>();

                    for (var i = 0; i < terrainGenData.detailAssets.Length; i++)
                    {
                        LandscapeAsset detailAsset = terrainGenData.detailAssets[i];

                        try
                        {
                            INoiseGenerator noiseGenerator = noiseGenCache.GetGeneratorFor(detailAsset.noiseData, baseSeed);
                            JobHandle handle = noiseGenerator.Schedule(noiseDataPointer, default(JobHandle));
                            UniTask task = handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                            generators.Add(noiseGenerator);
                            tasks.Add(task);
                        }
                        catch (Exception e) when (e is not OperationCanceledException)
                        {
                            ReportHub.LogError(null, $"Failed to set detail layer for {detailAsset.name}");
                            ReportHub.LogException(e, null);
                        }
                    }

                    await UniTask.WhenAll(tasks).AttachExternalCancellation(cancellationToken);
                }

                {
                    for (var i = 0; i < terrainGenData.detailAssets.Length; i++)
                    {
                        NativeArray<float> result = generators[i].GetResult(noiseDataPointer);

                        int[,] detailLayer = terrainData.GetDetailLayer(0, 0, terrainData.detailWidth, terrainData.detailHeight, i);

                        for (var y = 0; y < chunkSize; y++)
                        for (var x = 0; x < chunkSize; x++)
                        {
                            int f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? UNITY_MAX_COVERAGE_VALUE : UNITY_MAX_INSTANCE_COUNT;
                            int index = x + (y * chunkSize);
                            float value = result[index];
                            detailLayer[y, x] = Mathf.FloorToInt(value * f);
                        }

                        terrainData.SetDetailLayer(0, 0, i, detailLayer);
                    }
                }
            }
        }

        private async UniTask SetTreesAsync(int offsetX, int offsetZ, int chunkSize, TerrainData terrainData, uint baseSeed,
            int2 minParcel,
            CancellationToken cancellationToken)
        {
            {
                cancellationToken.ThrowIfCancellationRequested();

                var treeInstances = new NativeParallelHashMap<int2, TreeInstance>(chunkSize * chunkSize, Allocator.Persistent);
                var treeInvalidationMap = new NativeParallelHashMap<int2, bool>(chunkSize * chunkSize, Allocator.Persistent);
                var treeRadiusMap = new NativeHashMap<int, float>(terrainGenData.treeAssets.Length, Allocator.Persistent);
                var treeParallelRandoms = new NativeArray<Random>(chunkSize * chunkSize, Allocator.Persistent);

                try
                {
                    {
                        var instancingHandle = default(JobHandle);

                        for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
                        {
                            LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                            NoiseDataBase treeNoiseData = treeAsset.noiseData;

                            treeRadiusMap.Add(treeAssetIndex, treeAsset.radius);

                            INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                            var noiseDataPointer = new NoiseDataPointer(chunkSize, offsetX, offsetZ);
                            JobHandle generatorHandle = generator.Schedule(noiseDataPointer, instancingHandle);

                            var randomizer = new SetupRandomForParallelJobs(treeParallelRandoms, (int)worldSeed);
                            JobHandle randomizerHandle = randomizer.Schedule(generatorHandle);

                            NativeArray<float> resultReference = generator.GetResult(noiseDataPointer);

                            var treeInstancesJob = new GenerateTreeInstancesJob(
                                resultReference.AsReadOnly(),
                                treeInstances.AsParallelWriter(),
                                emptyParcelNeighborHeightsData.AsReadOnly(),
                                in treeAsset.randomization,
                                treeAsset.radius,
                                treeAssetIndex,
                                offsetX,
                                offsetZ,
                                chunkSize,
                                chunkSize,
                                minParcel,
                                treeParallelRandoms,
                                false);

                            instancingHandle = treeInstancesJob.Schedule(resultReference.Length, 32, randomizerHandle);
                        }

                        await instancingHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                        instancingHandle.Complete();
                    }

                    // {
                    //     var invalidationJob = new InvalidateTreesJob(treeInstances.AsReadOnly(), treeInvalidationMap.AsParallelWriter(), treeRadiusMap.AsReadOnly(), chunkSize);
                    //     await invalidationJob.Schedule(chunkSize * chunkSize, 8).ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    // }

                    var array = new List<TreeInstance>();

                    foreach (KeyValue<int2, TreeInstance> treeInstance in treeInstances)
                    {
                        // // if its marked as invalid, do not use this tree
                        // if (!treeInvalidationMap.TryGetValue(treeInstance.Key, out bool isInvalid)) continue;
                        // if (isInvalid) continue;

                        array.Add(treeInstance.Value);
                    }

                    {
                        TreeInstance[] instances = array.ToArray();
                        terrainData.SetTreeInstances(instances, true);
                    }
                }
                catch (Exception e)
                {
                    // todo
                }
                finally
                {
                    treeInstances.Dispose();
                    treeInvalidationMap.Dispose();
                    treeRadiusMap.Dispose();
                    treeParallelRandoms.Dispose();
                }
            }
        }

        /// <summary>
        ///     Here we convert the result of the noise generation of the terrain texture layers
        /// </summary>
        private float[,,] GenerateAlphaMaps(NativeArray<float>[] textureResults, int width, int height)
        {
            var result = new float[width, height, terrainGenData.terrainLayers.Length];

            // every layer has the same direction, so we use the first
            int length = textureResults[0].Length;

            for (var i = 0; i < length; i++)
            {
                int x = i % width;
                int z = i / width;

                float summary = 0;

                // Get the texture value for each layer at this spot
                for (var j = 0; j < textureResults.Length; j++)
                {
                    float f = textureResults[j][i];
                    summary += f;
                }

                // base value is always the unfilled spot where other layers didn't draw texture
                float baseValue = Mathf.Max(0, 1 - summary);
                summary += baseValue;

                // we set the base value manually since its not part of the textureResults list
                result[z, x, 0] = baseValue / summary;

                // set the rest of the values
                for (var j = 0; j < textureResults.Length; j++)
                    result[z, x, j + 1] = textureResults[j][i] / summary;
            }

            return result;
        }

        private GameObject InstantiateSingletonTerrainRoot()
        {
            rootGo = GameObject.Find(TERRAIN_OBJECT_NAME);

            if (rootGo != null)
                UnityObjectUtils.SafeDestroy(rootGo);

            return new GameObject(TERRAIN_OBJECT_NAME);
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

        private DetailPrototype[] GetDetailPrototypes()
        {
            return terrainGenData.detailAssets.Select(a =>
                                  {
                                      var detailPrototype = new DetailPrototype
                                      {
                                          usePrototypeMesh = true,
                                          prototype = a.asset,
                                          useInstancing = true,
                                          renderMode = DetailRenderMode.VertexLit,
                                          density = a.TerrainDetailSettings.detailDensity,
                                          alignToGround = a.TerrainDetailSettings.alignToGround / 100f,
                                          holeEdgePadding = a.TerrainDetailSettings.holeEdgePadding / 100f,
                                          minWidth = a.TerrainDetailSettings.minWidth,
                                          maxWidth = a.TerrainDetailSettings.maxWidth,
                                          minHeight = a.TerrainDetailSettings.minHeight,
                                          maxHeight = a.TerrainDetailSettings.maxHeight,
                                          noiseSeed = a.TerrainDetailSettings.noiseSeed,
                                          noiseSpread = a.TerrainDetailSettings.noiseSpread,
                                          useDensityScaling = a.TerrainDetailSettings.affectedByGlobalDensityScale,
                                          positionJitter = a.TerrainDetailSettings.positionJitter / 100f,
                                      };

                                      return detailPrototype;
                                  })
                                 .ToArray();
        }

        private void GenerateCliffs(TerrainModel terrainModel, GameObject cliffSide, GameObject cliffCorner)
        {
            if (cliffSide == null || cliffCorner == null)
                return;

            Transform cliffsRoot = new GameObject("Cliffs").transform;
            cliffsRoot.SetParent(rootGo.transform);

            CreateCliffCornerAt(new Vector3(terrainModel.minInUnits.x, 0, terrainModel.minInUnits.y), Quaternion.Euler(0, 180, 0));
            CreateCliffCornerAt(new Vector3(terrainModel.maxInUnits.x, 0, terrainModel.maxInUnits.y), Quaternion.identity);
            CreateCliffCornerAt(new Vector3(terrainModel.maxInUnits.x, 0, terrainModel.minInUnits.y), Quaternion.Euler(0, 90, 0));
            CreateCliffCornerAt(new Vector3(terrainModel.minInUnits.x, 0, terrainModel.maxInUnits.y), Quaternion.Euler(0, 270, 0));

            for (int i = terrainModel.minInUnits.y; i < terrainModel.maxInUnits.y; i += PARCEL_SIZE)
            {
                Transform side = Object.Instantiate(cliffSide).transform;
                side.position = new Vector3(terrainModel.maxInUnits.x, 0, i + PARCEL_SIZE);
                side.rotation = Quaternion.Euler(0, 90, 0);
                side.SetParent(cliffsRoot, true);
                cliffs.Add(side);
            }

            for (int i = terrainModel.minInUnits.x; i < terrainModel.maxInUnits.x; i += PARCEL_SIZE)
            {
                Transform side = Object.Instantiate(cliffSide).transform;
                side.position = new Vector3(i, 0, terrainModel.maxInUnits.y);
                side.rotation = Quaternion.identity;
                side.SetParent(cliffsRoot, true);
                cliffs.Add(side);
            }

            for (int i = terrainModel.minInUnits.y; i < terrainModel.maxInUnits.y; i += PARCEL_SIZE)
            {
                Transform side = Object.Instantiate(cliffSide).transform;
                side.position = new Vector3(terrainModel.minInUnits.x, 0, i);
                side.rotation = Quaternion.Euler(0, 270, 0);
                side.SetParent(cliffsRoot, true);
                cliffs.Add(side);
            }

            for (int i = terrainModel.minInUnits.x; i < terrainModel.maxInUnits.x; i += PARCEL_SIZE)
            {
                Transform side = Object.Instantiate(cliffSide).transform;
                side.position = new Vector3(i + PARCEL_SIZE, 0, terrainModel.minInUnits.y);
                side.rotation = Quaternion.Euler(0, 180, 0);
                side.SetParent(cliffsRoot, true);
                cliffs.Add(side);
            }

            cliffsRoot.localPosition = Vector3.zero;
            return;

            void CreateCliffCornerAt(Vector3 position, Quaternion rotation)
            {
                Transform neCorner = Object.Instantiate(cliffCorner).transform;
                neCorner.position = position;
                neCorner.rotation = rotation;
                neCorner.SetParent(cliffsRoot, true);
                cliffs.Add(neCorner);
            }
        }

        private void SpawnMiscAsync()
        {
            Ocean = Object.Instantiate(terrainGenData.ocean).transform;
            Ocean.SetParent(rootGo.transform, true);

            Transform wind = Object.Instantiate(terrainGenData.wind).transform;
            wind.SetParent(rootGo.transform, true);
        }

        // Convert flat NativeArray to a 2D array (is there another way?)
        private static float[,] ConvertTo2DArray(NativeArray<float> array, int width, int height)
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
    }
}
