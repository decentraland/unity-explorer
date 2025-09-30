using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Profiling;
using DCL.Utilities;
using GPUInstancerPro;
using System.Diagnostics;
using System.IO;
using System.Text;
using TerrainProto;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using Unity.Mathematics;
using UnityEngine;
using Utility;
using static Unity.Mathematics.math;
using JobHandle = Unity.Jobs.JobHandle;

namespace DCL.Landscape
{
    public class TerrainGenerator : IDisposable, ITerrain
    {
        public const int MAX_HEIGHT = 16;
        private const string TERRAIN_OBJECT_NAME = "Generated Terrain";
        private const float ROOT_VERTICAL_SHIFT = -0.1f; // fix for not clipping with scene (potential) floor

        private const float PROGRESS_COUNTER_EMPTY_PARCEL_DATA = 0.1f;
        private const float PROGRESS_COUNTER_OCCUPANCY_MAP = 0.4f;
        private const float PROGRESS_COUNTER_TERRAIN_COMPONENTS = 0.8f;

        private const int TERRAIN_SIZE_LIMIT = 512; // 512x512 parcels
        private const int TREE_INSTANCE_LIMIT = 262144; // 2^18 trees

        private readonly ReportData reportData;
        private readonly TimeProfiler timeProfiler;
        private readonly IMemoryProfiler profilingProvider;

        private TerrainGenerationData terrainGenData;
        private TerrainBoundariesGenerator boundariesGenerator;
        private TerrainFactory factory;
        private GPUIProfile treesProfile;

        private NativeList<int2> emptyParcels;
        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;
        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeHashSet<int2> ownedParcels;

        private int processedTerrainDataCount;
        private int spawnedTerrainDataCount;
        private bool isInitialized;

        private NativeArray<byte> occupancyMapData;
        private int occupancyMapSize;
        private int2 treeMinParcel;
        private int2 treeMaxParcel;
        private NativeArray<int> treeIndices;
        private NativeArray<TreeInstanceData> treeInstances;

        private int[]? gpuInstancerRendererKeys;
        private int[]? gpuInstancerInstanceCounts;

        public Transform TerrainRoot { get; private set; }

        public int ParcelSize { get; private set; }
        public Transform Ocean { get; private set; }
        public Transform Wind { get; private set; }
        public IReadOnlyList<Transform> Cliffs { get; private set; }

        [Obsolete]
        public IReadOnlyList<Terrain> Terrains => Array.Empty<Terrain>();

        public bool IsTerrainGenerated { get; private set; }
        public bool IsTerrainShown { get; private set; }

        public TerrainModel TerrainModel { get; private set; }
        public Texture2D OccupancyMap { get; private set; }
        public int OccupancyFloor { get; private set; }

        private static string treeFilePath => $"{Application.streamingAssetsPath}/Trees.bin";

        public TerrainGenerator(IMemoryProfiler profilingProvider, bool measureTime = false)
        {
            this.profilingProvider = profilingProvider;

            reportData = ReportCategory.LANDSCAPE;
            timeProfiler = new TimeProfiler(measureTime);
        }

        public void Initialize(TerrainGenerationData terrainGenData, GPUIProfile treesProfile, ref NativeList<int2> emptyParcels, ref NativeHashSet<int2> ownedParcels)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            this.treesProfile = treesProfile;

            ParcelSize = terrainGenData.parcelSize;
            factory = new TerrainFactory(terrainGenData);

            boundariesGenerator = new TerrainBoundariesGenerator(factory, ParcelSize);

            isInitialized = true;
        }

        public void Dispose()
        {
            if (!isInitialized) return;

            if (TerrainRoot != null)
                UnityObjectUtils.SafeDestroy(TerrainRoot);
        }

        [Obsolete]
        public void SetTerrainCollider(Vector2Int parcel, bool isEnabled) { }

        public bool Contains(Vector2Int parcel)
        {
            if (IsTerrainGenerated)
                return TerrainModel.IsInsideBounds(parcel);

            return true;
        }

        public int GetChunkSize() =>
            terrainGenData.chunkSize;

        public async UniTask ShowAsync(AsyncLoadProcessReport postRealmLoadReport)
        {
            if (!isInitialized) return;

            if (TerrainRoot != null)
                TerrainRoot.gameObject.SetActive(true);

            // TODO is it necessary to yield?
            await UniTask.Yield(PlayerLoopTiming.LastPostLateUpdate);

            ShowGPUInstancerInstances();

            IsTerrainShown = true;

            postRealmLoadReport.SetProgress(1f);
        }

        public void Hide()
        {
            if (!isInitialized) return;

            if (TerrainRoot != null && TerrainRoot.gameObject.activeSelf)
            {
                TerrainRoot.gameObject.SetActive(false);

                HideGPUInstancerInstances();

                IsTerrainShown = false;
            }
        }

        public async UniTask GenerateGenesisTerrainAndShowAsync(AsyncLoadProcessReport? processReport = null,
            CancellationToken cancellationToken = default)
        {
            if (!isInitialized) return;

            var worldModel = new WorldModel(ownedParcels);
            TerrainModel = new TerrainModel(ParcelSize, worldModel, terrainGenData.borderPadding);

            float startMemory = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);

            try
            {
                using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"Terrain generation was done in {t / 1000f:F2} seconds")))
                {
                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Empty Parcel Setup")))
                    {
                        TerrainGenerationUtils.ExtractEmptyParcels(TerrainModel.MinParcel,
                            TerrainModel.MaxParcel, ref emptyParcels, ref ownedParcels);

                        await SetupEmptyParcelDataAsync(TerrainModel, cancellationToken);
                    }

                    processReport?.SetProgress(PROGRESS_COUNTER_EMPTY_PARCEL_DATA);

                    // The MinParcel, MaxParcel already has padding, so padding zero works here,
                    // but in reality we should remove integrated padding and utilise padding parameter as it would make things more scalable and less confusing
                    OccupancyMap = CreateOccupancyMap(ownedParcels, TerrainModel.MinParcel, TerrainModel.MaxParcel, 0);
                    OccupancyFloor = WriteInteriorChamferOnWhite(OccupancyMap, TerrainModel.MinParcel, TerrainModel.MaxParcel, 0);

                    // OccupancyMap.filterMode = FilterMode.Point; // DEBUG use for clear step-like pyramid terrain base height
                    OccupancyMap.Apply(updateMipmaps: false, makeNoLongerReadable: false);

                    occupancyMapData = OccupancyMap.GetRawTextureData<byte>();
                    occupancyMapSize = OccupancyMap.width; // width == height

                    processReport?.SetProgress(PROGRESS_COUNTER_OCCUPANCY_MAP);

                    using (timeProfiler.Measure(t => ReportHub.Log(reportData, $"[{t:F2}ms] Misc & Cliffs, Border Colliders")))
                    {
                        TerrainRoot = factory.InstantiateSingletonTerrainRoot(TERRAIN_OBJECT_NAME);
                        TerrainRoot.position = new Vector3(0, ROOT_VERTICAL_SHIFT, 0);

                        Ocean = factory.CreateOcean(TerrainRoot);
                        Wind = factory.CreateWind();

                        Cliffs = boundariesGenerator.SpawnCliffs(TerrainModel.MinInUnits, TerrainModel.MaxInUnits);
                        boundariesGenerator.SpawnBorderColliders(TerrainModel.MinInUnits, TerrainModel.MaxInUnits, TerrainModel.SizeInUnits);
                    }

                    processReport?.SetProgress(PROGRESS_COUNTER_TERRAIN_COMPONENTS);

                    await LoadTreesAsync();
                    InstantiateTrees();

                    processReport?.SetProgress(1f);
                }
            }
            catch (OperationCanceledException)
            {
                if (TerrainRoot != null)
                    UnityObjectUtils.SafeDestroy(TerrainRoot);
            }
            catch (Exception e) when (e is not OperationCanceledException) { ReportHub.LogException(e, reportData); }
            finally
            {
                float beforeCleaning = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
                FreeMemory();

                IsTerrainGenerated = true;
                IsTerrainShown = true;

                emptyParcels.Dispose();
                ownedParcels.Dispose();

                float afterCleaning = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);

                ReportHub.Log(ReportCategory.LANDSCAPE,
                    $"The landscape cleaning process cleaned {afterCleaning - beforeCleaning}MB of memory");
            }

            float endMemory = profilingProvider.SystemUsedMemoryInBytes / (1024 * 1024);
            ReportHub.Log(ReportCategory.LANDSCAPE, $"The landscape generation took {endMemory - startMemory}MB of memory");
        }

        private async UniTask SetupEmptyParcelDataAsync(TerrainModel terrainModel, CancellationToken cancellationToken)
        {
            JobHandle handle = TerrainGenerationUtils.SetupEmptyParcelsJobs(
                ref emptyParcelsData, ref emptyParcelsNeighborData,
                emptyParcels.AsArray(), ref ownedParcels,
                terrainModel.MinParcel, terrainModel.MaxParcel,
                terrainGenData.heightScaleNerf);

            await handle.ToUniTask(PlayerLoopTiming.Update).AttachExternalCancellation(cancellationToken);
        }

        // This should free up all the NativeArrays used for random generation, this wont affect the already generated terrain
        private void FreeMemory()
        {
            emptyParcelsNeighborData.Dispose();
            emptyParcelsData.Dispose();
        }

        private static Texture2D CreateOccupancyMap(NativeHashSet<int2> ownedParcels, int2 minParcel,
            int2 maxParcel, int padding)
        {
            int2 terrainSize = maxParcel - minParcel + 1;
            int2 citySize = terrainSize - (padding * 2);
            int textureSize = ceilpow2(cmax(terrainSize) + 2);
            int textureHalfSize = textureSize / 2;

            var occupancyMap = new Texture2D(textureSize, textureSize, TextureFormat.R8, false,
                true);

            NativeArray<byte> data = occupancyMap.GetRawTextureData<byte>();

            // 1) memset all to 0 (OCCUPIED)
            unsafe
            {
                void* ptr = NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(data);
                UnsafeUtility.MemSet(ptr, 0, data.Length);
            }

            // 2) Fill wide horizontal WHITE stripe for FREE area rows (full width), then trim with vertical black stripes
            {
                int freeMinX = minParcel.x - padding;
                int freeMinY = minParcel.y - padding;
                int freeMaxX = maxParcel.x + padding;
                int freeMaxY = maxParcel.y + padding;

                // Clamp logical coordinates to texture logical range [-textureHalfSize .. textureHalfSize-1]
                freeMinX = Math.Max(freeMinX, -textureHalfSize);
                freeMinY = Math.Max(freeMinY, -textureHalfSize);
                freeMaxX = Math.Min(freeMaxX, textureHalfSize - 1);
                freeMaxY = Math.Min(freeMaxY, textureHalfSize - 1);

                int startRow = textureHalfSize + freeMinY;
                int endRowInclusive = textureHalfSize + freeMaxY;
                int rowCount = Math.Max(0, endRowInclusive - startRow + 1);

                // Fill entire horizontal stripe WHITE in one MemSet call
                if (rowCount > 0)
                {
                    unsafe
                    {
                        var basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(data);
                        int stripeStartByte = startRow * textureSize;
                        int stripeSizeBytes = rowCount * textureSize;
                        UnsafeUtility.MemSet(basePtr + stripeStartByte, 255, stripeSizeBytes);
                    }
                }

                // 3) Trim with vertical BLACK stripes to define actual FREE area boundaries
                if (rowCount > 0)
                {
                    int leftCol = textureHalfSize + freeMinX - 1;
                    int rightCol = textureHalfSize + freeMaxX + 1;

                    // Clamp columns to texture bounds
                    leftCol = Math.Clamp(leftCol, 0, textureSize - 1);
                    rightCol = Math.Clamp(rightCol, 0, textureSize - 1);

                    unsafe
                    {
                        var basePtr = (byte*)NativeArrayUnsafeUtility.GetUnsafeBufferPointerWithoutChecks(data);

                        // Left vertical stripe
                        for (int row = startRow; row <= endRowInclusive; row++)
                            *(basePtr + (row * textureSize) + leftCol) = 0;

                        // Right vertical stripe
                        for (int row = startRow; row <= endRowInclusive; row++)
                            *(basePtr + (row * textureSize) + rightCol) = 0;
                    }
                }
            }

            // 4) mark owned parcels as BLACK (occupied)
            foreach (int2 owned in ownedParcels)
            {
                int tx = owned.x + textureHalfSize;
                int ty = owned.y + textureHalfSize;
                int idx = (ty * textureSize) + tx;

                if ((uint)idx < (uint)data.Length)
                    data[idx] = 0;
            }

            return occupancyMap;
        }

        private static int WriteInteriorChamferOnWhite(Texture2D r8, int2 minParcel, int2 maxParcel, int padding)
        {
            int w = r8.width, h = r8.height, n = w * h;
            NativeArray<byte> src = r8.GetRawTextureData<byte>();
            if (!src.IsCreated || src.Length != n) return 0;

            int textureHalfSize = w / 2; // Assuming square texture

            // Calculate working area bounds in texture coordinates (with padding)
            int workMinX = minParcel.x - padding;
            int workMinY = minParcel.y - padding;
            int workMaxX = maxParcel.x + padding;
            int workMaxY = maxParcel.y + padding;

            // Clamp to texture logical range [-textureHalfSize .. textureHalfSize-1]
            workMinX = Math.Max(workMinX, -textureHalfSize);
            workMinY = Math.Max(workMinY, -textureHalfSize);
            workMaxX = Math.Min(workMaxX, textureHalfSize - 1);
            workMaxY = Math.Min(workMaxY, textureHalfSize - 1);

            // Convert to texture pixel coordinates and expand by 1 to include border stripes
            int texMinX = workMinX + textureHalfSize - 1;
            int texMinY = workMinY + textureHalfSize - 1;
            int texMaxX = workMaxX + textureHalfSize + 1;
            int texMaxY = workMaxY + textureHalfSize + 1;

            // Clamp to actual texture bounds
            texMinX = Math.Max(texMinX, 0);
            texMinY = Math.Max(texMinY, 0);
            texMaxX = Math.Min(texMaxX, w - 1);
            texMaxY = Math.Min(texMaxY, h - 1);

            const int INF = 1 << 28;
            const int ORTH = 3; // 3-4 chamfer (good Euclidean approx)
            const int DIAG = 4;

            // Seed distances at BLACK pixels (occupied parcels - leave them 0), propagate into WHITE (free parcels)
            var dist = new int[n];
            bool anyBlack = false, anyWhite = false;

            // Initialize only working area pixels
            for (int y = texMinY; y <= texMaxY; y++)
            for (int x = texMinX; x <= texMaxX; x++)
            {
                int i = (y * w) + x;

                if (src[i] == 0)
                {
                    dist[i] = 0;
                    anyBlack = true;
                } // Black pixels (occupied) are seeds
                else
                {
                    dist[i] = INF;
                    anyWhite = true;
                } // White pixels (free) will get distances
            }

            if (!anyBlack || !anyWhite)
                return 0; // Nothing to do if no black or no white regions exist.

            // Forward pass - only within working area
            for (int y = texMinY; y <= texMaxY; y++)
            {
                int row = y * w;

                for (int x = texMinX; x <= texMaxX; x++)
                {
                    int i = row + x;
                    int d = dist[i];

                    if (d != 0) // skip black seeds
                    {
                        if (x > texMinX) d = Mathf.Min(d, dist[i - 1] + ORTH);
                        if (y > texMinY) d = Mathf.Min(d, dist[i - w] + ORTH);
                        if (x > texMinX && y > texMinY) d = Mathf.Min(d, dist[i - w - 1] + DIAG);
                        if (x < texMaxX && y > texMinY) d = Mathf.Min(d, dist[i - w + 1] + DIAG);
                        dist[i] = d;
                    }
                }
            }

            // Backward pass - only within working area
            for (int y = texMaxY; y >= texMinY; y--)
            {
                int row = y * w;

                for (int x = texMaxX; x >= texMinX; x--)
                {
                    int i = row + x;
                    int d = dist[i];

                    if (d != 0) // skip black seeds
                    {
                        if (x < texMaxX) d = Mathf.Min(d, dist[i + 1] + ORTH);
                        if (y < texMaxY) d = Mathf.Min(d, dist[i + w] + ORTH);
                        if (x < texMaxX && y < texMaxY) d = Mathf.Min(d, dist[i + w + 1] + DIAG);
                        if (x > texMinX && y < texMaxY) d = Mathf.Min(d, dist[i + w - 1] + DIAG);
                        dist[i] = d;
                    }
                }
            }

            // Find maximum distance and convert to pixel distance - only within working area
            var maxD = 0;

            for (int y = texMinY; y <= texMaxY; y++)
            for (int x = texMinX; x <= texMaxX; x++)
            {
                int i = (y * w) + x;

                if (src[i] != 0 && dist[i] < INF && dist[i] > maxD)
                    maxD = dist[i]; // Check white pixels
            }

            if (maxD == 0)
                return 0;

            // Convert chamfer distance to approximate pixel distance
            int maxPixelDistance = maxD / ORTH;

            // Check for overflow and warn if needed
            bool hasOverflow = maxPixelDistance > 255;

            if (hasOverflow)
            {
                ReportHub.LogError(ReportCategory.LANDSCAPE, $"Distance field overflow! Max distance {maxPixelDistance} pixels, clamping to 255");
                maxPixelDistance = 255;
            }

            // Calculate adaptive stepSize to avoid merging with black pixels
            int stepSize;

            if (maxPixelDistance <= 25)
            {
                stepSize = 10; // Default stepSize for reasonable distances
            }
            else
            {
                // Adaptive stepSize to fit large distances, ensuring minValue >= 1
                stepSize = (255 - 1) / maxPixelDistance; // 254 / maxPixelDistance
                stepSize = Mathf.Max(stepSize, 1); // Ensure at least 1
            }

            int minValue = 255 - (stepSize * maxPixelDistance);
            ReportHub.Log(ReportCategory.LANDSCAPE, $"Distance field: max chamfer={maxD}, max maxPixelDistance={maxPixelDistance}, stepSize={stepSize}, range=[{minValue}, 255]");

            // Write back: keep black at 0, map distances to [minValue, 255] range - only within working area
            for (int y = texMinY; y <= texMaxY; y++)
            {
                for (int x = texMinX; x <= texMaxX; x++)
                {
                    int i = (y * w) + x;

                    if (src[i] == 0)
                    {
                        src[i] = 0;
                        continue;
                    } // keep black pixels (occupied)

                    // Convert chamfer distance to pixel distance
                    int pixelDist = dist[i] / ORTH;
                    pixelDist = Mathf.Min(pixelDist, maxPixelDistance); // Clamp to max

                    // Map [1, maxPixelDistance] to [minValue, 255]
                    int value;

                    if (maxPixelDistance == 1)
                        value = 255; // Only one distance level, use max value
                    else
                    {
                        value = minValue + ((pixelDist - 1) * (255 - minValue) / (maxPixelDistance - 1));
                        value = Mathf.Clamp(value, minValue, 255);
                    }

                    src[i] = (byte)value;
                }
            }

            return minValue;
        }

        public bool IsParcelOccupied(int2 parcel)
        {
            if (any(parcel < TerrainModel.MinParcel) || any(parcel > TerrainModel.MaxParcel))
                return false;

            if (occupancyMapSize <= 0)
                return false;

            parcel += occupancyMapSize / 2;
            int index = (parcel.y * occupancyMapSize) + parcel.x;
            return occupancyMapData[index] == 0;
        }

        private static float GetParcelNoiseHeight(float x, float z, NativeArray<byte> occupancyMapData,
            int occupancyMapSize, int parcelSize, int occupancyFloor)
        {
            float occupancy;

            if (occupancyMapSize > 0)
            {
                // Take the bounds of the terrain, put a single pixel border around it, increase the
                // size to the next power of two, map xz=0,0 to uv=0.5,0.5 and parcelSize to pixel size,
                // and that's the occupancy map.
                float2 uv = ((float2(x, z) / parcelSize) + (occupancyMapSize * 0.5f)) / occupancyMapSize;
                occupancy = SampleBilinearClamp(occupancyMapData, int2(occupancyMapSize, occupancyMapSize), uv);
            }
            else
                occupancy = 0f;

            // float height = SAMPLE_TEXTURE2D_LOD(HeightMap, HeightMap.samplerstate, uv, 0.0).r;
            float minValue = occupancyFloor / 255.0f; // 0.68

            if (occupancy <= minValue) return 0f;

            const float SATURATION_FACTOR = 20;
            float normalizedHeight = (occupancy - minValue) / (1 - minValue);
            return (normalizedHeight * MAX_HEIGHT) + (MountainsNoise.GetHeight(x, z) * saturate(normalizedHeight * SATURATION_FACTOR));
        }

        public float GetHeight(float x, float z) =>
            GetParcelNoiseHeight(x, z, occupancyMapData, occupancyMapSize, ParcelSize, OccupancyFloor);

        public static float GetHeight(float x, float z, int parcelSize,
            NativeArray<byte> occupancyMapData, int occupancyMapSize, int occupancyFloor)
        {
            // var parcel = (int2)floor(float2(x, z) / parcelSize);
            return GetParcelNoiseHeight(x, z, occupancyMapData, occupancyMapSize, parcelSize, occupancyFloor);
        }

        private static float SampleBilinearClamp(NativeArray<byte> texture, int2 textureSize, float2 uv)
        {
            uv = (uv * textureSize) - 0.5f;
            var min = (int2)floor(uv);

            // A quick prayer for Burst to SIMD this. 🙏
            int4 index = (clamp(min.y + int4(1, 1, 0, 0), 0, textureSize.y - 1) * textureSize.x) +
                         clamp(min.x + int4(0, 1, 1, 0), 0, textureSize.x - 1);

            float2 t = frac(uv);
            float top = lerp(texture[index.w], texture[index.z], t.x);
            float bottom = lerp(texture[index.x], texture[index.y], t.x);
            return lerp(top, bottom, t.y) * (1f / 255f);
        }

        public ReadOnlySpan<TreeInstanceData> GetTreeInstances(int2 parcel)
        {
            // If tree data has not been loaded, minParcel == maxParcel, and so this is false, and we
            // don't need to check if treeInstances is empty or anything like that.
            if (parcel.x < treeMinParcel.x || parcel.x >= treeMaxParcel.x
                                           || parcel.y < treeMinParcel.y || parcel.y >= treeMaxParcel.y) { return ReadOnlySpan<TreeInstanceData>.Empty; }

            int index = ((parcel.y - treeMinParcel.y) * (treeMaxParcel.x - treeMinParcel.x))
                + parcel.x - treeMinParcel.x;

            int start = treeIndices[index++];
            int end = index < treeIndices.Length ? treeIndices[index] : treeInstances.Length;
            return treeInstances.AsReadOnlySpan().Slice(start, end - start);
        }

        public bool GetTreeTransform(int2 parcel, TreeInstanceData instance, out Vector3 position,
            out Quaternion rotation, out Vector3 scale)
        {
            position.x = (parcel.x + (instance.PositionX * (1f / 255f))) * ParcelSize;
            position.z = (parcel.y + (instance.PositionZ * (1f / 255f))) * ParcelSize;

            if (OverlapsOccupiedParcel(float2(position.x, position.z),
                    terrainGenData.treeAssets[instance.PrototypeIndex].radius))
            {
                position.y = 0f;
                rotation = default(Quaternion);
                scale = default(Vector3);
                return false;
            }

            position.y = GetParcelNoiseHeight(position.x, position.z, occupancyMapData, occupancyMapSize, ParcelSize, OccupancyFloor);

            rotation = Quaternion.Euler(0f, instance.RotationY * (360f / 255f), 0f);

            scale = terrainGenData.treeAssets[instance.PrototypeIndex]
                                  .randomization
                                  .LerpScale(float2(instance.ScaleXZ, instance.ScaleY) * (1f / 255f))
                                  .xyx;

            return true;
        }

        private bool OverlapsOccupiedParcel(float2 position, float radius)
        {
            var parcel = (int2)floor(position * (1f / ParcelSize));

            if (IsParcelOccupied(parcel))
                return true;

            float2 localPosition = position - (parcel * ParcelSize);

            if (localPosition.x < radius)
            {
                if (IsParcelOccupied(int2(parcel.x - 1, parcel.y)))
                    return true;

                if (localPosition.y < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x - 1, parcel.y - 1)))
                        return true;
                }
            }

            if (ParcelSize - localPosition.x < radius)
            {
                if (IsParcelOccupied(int2(parcel.x + 1, parcel.y)))
                    return true;

                if (ParcelSize - localPosition.y < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x + 1, parcel.y + 1)))
                        return true;
                }
            }

            if (localPosition.y < radius)
            {
                if (IsParcelOccupied(int2(parcel.x, parcel.y - 1)))
                    return true;

                if (ParcelSize - localPosition.x < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x + 1, parcel.y - 1)))
                        return true;
                }
            }

            if (ParcelSize - localPosition.y < radius)
            {
                if (IsParcelOccupied(int2(parcel.x, parcel.y + 1)))
                    return true;

                if (localPosition.x < radius)
                {
                    if (IsParcelOccupied(int2(parcel.x - 1, parcel.y + 1)))
                        return true;
                }
            }

            return false;
        }

        private async UniTask LoadTreesAsync()
        {
            try
            {
                await using var stream = new FileStream(treeFilePath, FileMode.Open, FileAccess.Read,
                    FileShare.Read);

                using var reader = new BinaryReader(stream, new UTF8Encoding(false), false);

                treeMinParcel.x = reader.ReadInt32();
                treeMinParcel.y = reader.ReadInt32();
                treeMaxParcel.x = reader.ReadInt32();
                treeMaxParcel.y = reader.ReadInt32();

                int2 treeIndexSize = treeMaxParcel - treeMinParcel;

                if (any(treeIndexSize > TERRAIN_SIZE_LIMIT))
                    throw new IOException(
                        $"Tree index size of ({treeIndexSize.x}, {treeIndexSize.y}) exceeds the limit of {TERRAIN_SIZE_LIMIT}");

                treeIndices = new NativeArray<int>(treeIndexSize.x * treeIndexSize.y,
                    Allocator.Persistent, NativeArrayOptions.UninitializedMemory);

                ReadReliably(reader, treeIndices);

                int treeInstanceCount = reader.ReadInt32();

                if (treeInstanceCount > TREE_INSTANCE_LIMIT)
                    throw new IOException(
                        $"Tree instance count of {treeInstanceCount} exceeds the limit of {TREE_INSTANCE_LIMIT}");

                treeInstances = new NativeArray<TreeInstanceData>(treeInstanceCount, Allocator.Persistent,
                    NativeArrayOptions.UninitializedMemory);

                ReadReliably(reader, treeInstances);
            }
            catch (Exception ex)
            {
                if (ex is FileNotFoundException)
                    ReportHub.LogWarning(ReportCategory.LANDSCAPE,
                        $"Tree instance data file not found, path: {treeFilePath}");
                else
                    ReportHub.LogException(ex, ReportCategory.LANDSCAPE);

                if (treeIndices.IsCreated)
                    treeIndices.Dispose();

                if (treeInstances.IsCreated)
                    treeInstances.Dispose();

                treeIndices = new NativeArray<int>(0, Allocator.Persistent);
                treeInstances = new NativeArray<TreeInstanceData>(0, Allocator.Persistent);
            }
        }

        private static unsafe void ReadReliably<T>(BinaryReader reader, NativeArray<T> array)
            where T: unmanaged
        {
            var buffer = new Span<byte>(array.GetUnsafePtr(), array.Length * sizeof(T));

            while (buffer.Length > 0)
            {
                int read = reader.Read(buffer);

                if (read <= 0)
                    throw new EndOfStreamException("Read zero bytes");

                buffer = buffer.Slice(read);
            }
        }

        [Conditional("GPUI_PRO_PRESENT")]
        private void ShowGPUInstancerInstances()
        {
            if (gpuInstancerRendererKeys == null || gpuInstancerInstanceCounts == null)
                return;

            for (var i = 0; i < gpuInstancerRendererKeys.Length; i++)
                GPUICoreAPI.SetInstanceCount(gpuInstancerRendererKeys[i], gpuInstancerInstanceCounts[i]);
        }

        [Conditional("GPUI_PRO_PRESENT")]
        private void HideGPUInstancerInstances()
        {
            if (gpuInstancerRendererKeys == null)
                return;

            for (var i = 0; i < gpuInstancerRendererKeys.Length; i++)
                GPUICoreAPI.SetInstanceCount(gpuInstancerRendererKeys[i], 0);
        }

        [Conditional("GPUI_PRO_PRESENT")]
        private void InstantiateTrees()
        {
            LandscapeAsset[] prototypes = terrainGenData.treeAssets;
            int stride = treeMaxParcel.x - treeMinParcel.x;
            var transforms = new List<Matrix4x4>[prototypes.Length];

            for (var i = 0; i < transforms.Length; i++)
                transforms[i] = new List<Matrix4x4>();

            for (var i = 0; i < treeIndices.Length; i++)
            {
                int2 parcel = int2(i % stride, i / stride) + treeMinParcel;
                ReadOnlySpan<TreeInstanceData> instances = GetTreeInstances(parcel);

                foreach (TreeInstanceData instance in instances)
                {
                    if (GetTreeTransform(parcel, instance,
                            out Vector3 position, out Quaternion rotation, out Vector3 scale))
                    {
                        transforms[instance.PrototypeIndex]
                           .Add(Matrix4x4.TRS(position, rotation, scale));
                    }
                }
            }

            gpuInstancerRendererKeys = new int[terrainGenData.treeAssets.Length];
            gpuInstancerInstanceCounts = new int[gpuInstancerRendererKeys.Length];

            for (var prototypeIndex = 0; prototypeIndex < terrainGenData.treeAssets.Length;
                 prototypeIndex++)
            {
                GPUICoreAPI.RegisterRenderer(TerrainRoot, terrainGenData.treeAssets[prototypeIndex].asset, treesProfile, out gpuInstancerRendererKeys[prototypeIndex]);

                List<Matrix4x4> matrices = transforms[prototypeIndex];

                gpuInstancerInstanceCounts[prototypeIndex] = matrices.Count;

                GPUICoreAPI.SetTransformBufferData(gpuInstancerRendererKeys[prototypeIndex],
                    matrices, 0, 0, matrices.Count);
            }
        }
    }
}
