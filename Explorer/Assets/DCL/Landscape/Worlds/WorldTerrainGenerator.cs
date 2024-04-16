using Cysharp.Threading.Tasks;
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
        private const float ROOT_VERTICAL_SHIFT = -0.1f; // fix for not clipping with scene (potential) floor

        private readonly TerrainGenerationData terrainGenData;
        private readonly NoiseGeneratorCache noiseGenCache = new ();

        private GameObject rootGo;

        private int maxHeightIndex;
        private uint worldSeed;
        private TreePrototype[] treePrototypes;

        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelNeighborHeightsData;
        private NativeParallelHashMap<int2, int> emptyParcelHeights;
        private NativeArray<int2> emptyParcels;

        private Transform ocean;

        private readonly TerrainFactory factory;

        public WorldTerrainGenerator(TerrainGenerationData terrainGenData)
        {
            this.terrainGenData = terrainGenData;
            factory = new TerrainFactory(terrainGenData);
        }

        public async UniTask GenerateTerrainAsync(NativeParallelHashSet<int2> ownedParcels, uint worldSeed = 1, CancellationToken cancellationToken = default)
        {
            this.worldSeed = worldSeed;

            rootGo = TerrainFactory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
            rootGo.transform.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

            factory.CreateOcean(rootGo.transform);

            var worldModel = new WorldModel(ownedParcels);
            var terrainModel = new TerrainModel(worldModel, 2 + Mathf.RoundToInt(0.1f * (worldModel.SizeInParcels.x + worldModel.SizeInParcels.y) / 2f));

            GenerateCliffs(terrainModel, terrainGenData.cliffSide, terrainGenData.cliffCorner);
            GenerateBorderColliders(terrainModel.MinInUnits, terrainModel.MaxInUnits, terrainModel.SizeInUnits);

            // Extract empty parcels
            {
                var tempEmptyParcels = new List<int2>();

                for (int x = terrainModel.MinParcel.x; x <= terrainModel.MaxParcel.x; x++)
                for (int y = terrainModel.MinParcel.y; y <= terrainModel.MaxParcel.y; y++)
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
                    terrainGenData.heightScaleNerf, terrainModel.MinParcel, terrainModel.MaxParcel);

                JobHandle handle = job.Schedule(emptyParcels.Length, 32);

                var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelNeighborHeightsData.AsParallelWriter(),
                    emptyParcelHeights.AsReadOnly(), terrainModel.MinParcel, terrainModel.MaxParcel);

                JobHandle handle2 = job2.Schedule(emptyParcels.Length, 32, handle);

                await handle2.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);

                // Calculate this outside the jobs since they Items = {List<Pair<int2, int>>} Count = 32 are Parallel
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
                    size = new Vector3(terrainModel.ChunkSizeInUnits, 0.1f, terrainModel.ChunkSizeInUnits),
                    terrainLayers = terrainGenData.terrainLayers,
                    treePrototypes = factory.GetTreePrototypes(),
                    detailPrototypes = factory.GetDetailPrototypes(),
                };

                chunkModel.TerrainData.SetDetailResolution(terrainModel.ChunkSizeInUnits, 32);
                SetHeightsAsync(terrainModel, chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, chunkModel.TerrainData, worldSeed, cancellationToken).Forget();
                SetTexturesAsync(chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, terrainModel.ChunkSizeInUnits, chunkModel.TerrainData, worldSeed, cancellationToken).Forget();
                SetDetailsAsync(chunkModel, chunkModel.MinParcel.x * PARCEL_SIZE, chunkModel.MinParcel.y * PARCEL_SIZE, terrainModel.ChunkSizeInUnits, worldSeed, cancellationToken).Forget();
                SetTreesAsync(terrainModel, chunkModel, chunkModel.TerrainData, worldSeed, cancellationToken).Forget();

                // Dig Holes
                {
                    var holes = new bool[terrainModel.ChunkSizeInUnits, terrainModel.ChunkSizeInUnits];

                    for (var x = 0; x < terrainModel.ChunkSizeInUnits; x++)
                    for (var y = 0; y < terrainModel.ChunkSizeInUnits; y++)
                        holes[x, y] = true;

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

                // height can not be 0
                terrain.transform.position = new Vector3(chunkModel.MinParcel.x * PARCEL_SIZE, 0f, chunkModel.MinParcel.y * PARCEL_SIZE);
                terrain.transform.SetParent(rootGo.transform, false);

                await UniTask.Yield();
            }
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

        private async UniTask SetHeightsAsync(TerrainModel terrainModel, int offsetX, int offsetZ, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            {
                int resolution = terrainModel.ChunkSizeInUnits + 1;
                var heightArray = new float[resolution, resolution];
                terrainData.SetHeights(0, 0, heightArray);

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
                    terrainModel.MinParcel,
                    PARCEL_SIZE
                );

                JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64, handle);

                try { await jobHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken); }
                finally { heights.Dispose(); }
            }
        }

        private void GenerateBorderColliders(int2 minInUnits, int2 maxInUnits, int2 sidesLength)
        {
            var collidersRoot = TerrainFactory.CreateCollidersRoot(rootGo.transform);

            const float HEIGHT = 50.0f; // Height of the collider
            const float THICKNESS = 10.0f; // Thickness of the collider

            // Create colliders along each side of the terrain
            AddCollider(minInUnits.x, minInUnits.y, sidesLength.x, "South Border Collider", new int2(0, -1), 0);
            AddCollider(minInUnits.x, maxInUnits.y, sidesLength.x, "North Border Collider", new int2(0, 1), 0);
            AddCollider(minInUnits.x, minInUnits.y, sidesLength.y, "West Border Collider", new int2(-1, 0), 90);
            AddCollider(maxInUnits.x, minInUnits.y, sidesLength.y, "East Border Collider", new int2(1, 0), 90);
            return;

            void AddCollider(float posX, float posY, float length, string name, int2 dir, float rotation)
            {
                float xShift = dir.x == 0 ? length / 2 : ((THICKNESS / 2) + PARCEL_SIZE) * dir.x;
                float yShift = dir.y == 0 ? length / 2 : ((THICKNESS / 2) + PARCEL_SIZE) * dir.y;

                TerrainFactory.CreateBorderCollider(name, collidersRoot,
                    size: new Vector3(length, HEIGHT, THICKNESS),
                    position: new Vector3(posX + xShift, HEIGHT / 2, posY + yShift), rotation);
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

        private async UniTask SetDetailsAsync(ChunkModel chunkModel, int offsetX, int offsetZ, int chunkSize, uint baseSeed,
            CancellationToken cancellationToken)
        {
            chunkModel.TerrainData.SetDetailScatterMode(terrainGenData.detailScatterMode);

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

                        int[,] detailLayer = chunkModel.TerrainData.GetDetailLayer(0, 0, chunkModel.TerrainData.detailWidth, chunkModel.TerrainData.detailHeight, i);

                        for (var y = 0; y < chunkSize; y++)
                        for (var x = 0; x < chunkSize; x++)
                        {
                            int f = terrainGenData.detailScatterMode == DetailScatterMode.CoverageMode ? UNITY_MAX_COVERAGE_VALUE : UNITY_MAX_INSTANCE_COUNT;
                            float value = result[x + (y * chunkSize)];

                            detailLayer[y, x] = Mathf.FloorToInt(value * f);
                        }

                        foreach (int2 parcel in chunkModel.OccupiedParcels)
                            for (int y = (-chunkModel.MinParcel.y + parcel.y) * PARCEL_SIZE; y < (-chunkModel.MinParcel.y + parcel.y + 1) * PARCEL_SIZE; y++)
                            for (int x = (-chunkModel.MinParcel.x + parcel.x) * PARCEL_SIZE; x < (-chunkModel.MinParcel.x + parcel.x + 1) * PARCEL_SIZE; x++)
                                detailLayer[y, x] = 0;

                        chunkModel.TerrainData.SetDetailLayer(0, 0, i, detailLayer);
                    }
                }
            }
        }

        private async UniTask SetTreesAsync(TerrainModel terrainModel, ChunkModel chunkModel, TerrainData terrainData, uint baseSeed,
            CancellationToken cancellationToken)
        {
            {
                cancellationToken.ThrowIfCancellationRequested();

                int chunkSize = terrainModel.ChunkSizeInUnits;

                var treeInstances = new NativeParallelHashMap<int2, TreeInstance>(chunkSize * chunkSize, Allocator.Persistent);
                var treeInvalidationMap = new NativeParallelHashMap<int2, bool>(chunkSize * chunkSize, Allocator.Persistent);
                var treeRadiusMap = new NativeHashMap<int, float>(terrainGenData.treeAssets.Length, Allocator.Persistent);
                var treeParallelRandoms = new NativeArray<Random>(chunkSize * chunkSize, Allocator.Persistent);

                JobHandle instancingHandle = default;

                try
                {
                    for (var treeAssetIndex = 0; treeAssetIndex < terrainGenData.treeAssets.Length; treeAssetIndex++)
                    {
                        LandscapeAsset treeAsset = terrainGenData.treeAssets[treeAssetIndex];
                        NoiseDataBase treeNoiseData = treeAsset.noiseData;

                        treeRadiusMap.Add(treeAssetIndex, treeAsset.radius);

                        INoiseGenerator generator = noiseGenCache.GetGeneratorFor(treeNoiseData, baseSeed);
                        var noiseDataPointer = new NoiseDataPointer(chunkSize, chunkModel.MinParcel.x, chunkModel.MinParcel.y);
                        JobHandle generatorHandle = generator.Schedule(noiseDataPointer, default);

                        var randomizer = new SetupRandomForParallelJobs(treeParallelRandoms, (int)worldSeed);
                        JobHandle randomizerHandle = randomizer.Schedule(generatorHandle);

                        NativeArray<float> resultReference = generator.GetResult(noiseDataPointer);

                        var treeInstancesJob = new GenerateTreeInstancesJob(
                            treeNoise: resultReference.AsReadOnly(),
                            treeInstances: treeInstances.AsParallelWriter(),
                            emptyParcelResult: emptyParcelNeighborHeightsData.AsReadOnly(),
                            treeRandomization: in treeAsset.randomization,
                            treeRadius: treeAsset.radius,
                            treeIndex: treeAssetIndex,
                            offsetX: chunkModel.MinParcel.x,
                            offsetZ: chunkModel.MinParcel.y,
                            chunkSize: chunkSize,
                            chunkDensity: chunkSize,
                            minWorldParcel: new int2(terrainModel.MinParcel.x, terrainModel.MinParcel.y),
                            randoms: treeParallelRandoms,
                            useRandomSpawnChance: false,
                            useValidations: true);

                        instancingHandle = treeInstancesJob.Schedule(resultReference.Length, 32, randomizerHandle);

                        generatorHandle.Complete();
                        randomizerHandle.Complete();
                        instancingHandle.Complete();
                    }

                    await instancingHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    instancingHandle.Complete();

                    var invalidationJob = new InvalidateTreesJob(treeInstances.AsReadOnly(), treeInvalidationMap.AsParallelWriter(), treeRadiusMap.AsReadOnly(), chunkSize);
                    JobHandle invalidationHandle = invalidationJob.Schedule(chunkSize * chunkSize, 8);
                    await invalidationHandle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
                    invalidationHandle.Complete();

                    var array = new List<TreeInstance>();

                    foreach (KeyValue<int2, TreeInstance> treeInstance in treeInstances)
                    {
                        // if its marked as invalid, do not use this tree
                        if (!treeInvalidationMap.TryGetValue(treeInstance.Key, out bool isInvalid)) continue;

                        foreach (int2 parcel in chunkModel.OccupiedParcels)
                            if (treeInstance.Key.x >= (-chunkModel.MinParcel.x + parcel.x) * PARCEL_SIZE && treeInstance.Key.x < (-chunkModel.MinParcel.x + parcel.x + 1) * PARCEL_SIZE &&
                                treeInstance.Key.y >= (-chunkModel.MinParcel.y + parcel.y) * PARCEL_SIZE && treeInstance.Key.y < (-chunkModel.MinParcel.y + parcel.y + 1) * PARCEL_SIZE)
                                isInvalid = true;

                        if (isInvalid) continue;

                        array.Add(treeInstance.Value);
                    }

                    TreeInstance[] instances = array.ToArray();
                    terrainData.SetTreeInstances(instances, true);
                }
                catch (Exception e) { }
                finally
                {
                    instancingHandle.Complete();

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

        private void GenerateCliffs(TerrainModel terrainModel, GameObject cliffSide, GameObject cliffCorner)
        {
            if (cliffSide == null || cliffCorner == null)
                return;

            var cliffsRoot = TerrainFactory.CreateCliffsRoot(rootGo.transform);

            factory.CreateCliffCorner(cliffsRoot, new Vector3(terrainModel.MinInUnits.x, 0, terrainModel.MinInUnits.y), Quaternion.Euler(0, 180, 0));
            factory.CreateCliffCorner(cliffsRoot,new Vector3(terrainModel.MinInUnits.x, 0, terrainModel.MaxInUnits.y), Quaternion.Euler(0, 270, 0));
            factory.CreateCliffCorner(cliffsRoot,new Vector3(terrainModel.MaxInUnits.x, 0, terrainModel.MinInUnits.y), Quaternion.Euler(0, 90, 0));
            factory.CreateCliffCorner(cliffsRoot,new Vector3(terrainModel.MaxInUnits.x, 0, terrainModel.MaxInUnits.y), Quaternion.identity);

            for (int i = terrainModel.MinInUnits.y; i < terrainModel.MaxInUnits.y; i += PARCEL_SIZE)
                factory.CreateCliffSide(cliffsRoot,  new Vector3(terrainModel.MaxInUnits.x, 0, i + PARCEL_SIZE),Quaternion.Euler(0, 90, 0));

            for (int i = terrainModel.MinInUnits.x; i < terrainModel.MaxInUnits.x; i += PARCEL_SIZE)
                factory.CreateCliffSide(cliffsRoot,  new Vector3(i, 0, terrainModel.MaxInUnits.y),Quaternion.identity);

            for (int i = terrainModel.MinInUnits.y; i < terrainModel.MaxInUnits.y; i += PARCEL_SIZE)
                factory.CreateCliffSide(cliffsRoot,  new Vector3(terrainModel.MinInUnits.x, 0, i),Quaternion.Euler(0, 270, 0));

            for (int i = terrainModel.MinInUnits.x; i < terrainModel.MaxInUnits.x; i += PARCEL_SIZE)
                factory.CreateCliffSide(cliffsRoot,   new Vector3(i + PARCEL_SIZE, 0, terrainModel.MinInUnits.y),Quaternion.Euler(0, 180, 0));

            cliffsRoot.SetParent(rootGo.transform);
            cliffsRoot.localPosition = Vector3.zero;
        }
    }
}
