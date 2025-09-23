using Cysharp.Threading.Tasks;
using DCL.Diagnostics;
using DCL.Landscape.Jobs;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using System.Collections.Generic;
using System.Threading;
using DCL.Profiling;
using DCL.Utilities;
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

        internal const int TERRAIN_SIZE_LIMIT = 512; // 512x512 parcels
        internal const int TREE_INSTANCE_LIMIT = 262144; // 2^18 trees

        private readonly ReportData reportData;
        private readonly TimeProfiler timeProfiler;
        private readonly IMemoryProfiler profilingProvider;

        private TerrainGenerationData terrainGenData;
        private TerrainBoundariesGenerator boundariesGenerator;
        private TerrainFactory factory;
        public TreeData? Trees { get; private set; }

        private NativeList<int2> emptyParcels;
        private NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData;
        private NativeParallelHashMap<int2, int> emptyParcelsData;
        private NativeParallelHashSet<int2> ownedParcels;

        private int processedTerrainDataCount;
        private int spawnedTerrainDataCount;
        private bool isInitialized;
        public NativeArray<byte> OccupancyMapData { get; private set; }
        public int OccupancyMapSize { get; private set; }
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

        public TerrainGenerator(IMemoryProfiler profilingProvider, bool measureTime = false)
        {
            this.profilingProvider = profilingProvider;

            reportData = ReportCategory.LANDSCAPE;
            timeProfiler = new TimeProfiler(measureTime);
        }

        public void Initialize(TerrainGenerationData terrainGenData, int[] treeRendererKeys,
            ref NativeList<int2> emptyParcels, ref NativeParallelHashSet<int2> ownedParcels)
        {
            this.ownedParcels = ownedParcels;
            this.emptyParcels = emptyParcels;
            this.terrainGenData = terrainGenData;
            Trees = new TreeData(treeRendererKeys, terrainGenData);

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

            Trees!.Show();

            IsTerrainShown = true;

            postRealmLoadReport.SetProgress(1f);
        }

        public void Hide()
        {
            if (!isInitialized) return;

            if (TerrainRoot != null && TerrainRoot.gameObject.activeSelf)
            {
                TerrainRoot.gameObject.SetActive(false);

                Trees!.Hide();

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

                    OccupancyMapData = OccupancyMap.GetRawTextureData<byte>();
                    OccupancyMapSize = OccupancyMap.width; // width == height

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

                    await Trees!.LoadAsync($"{Application.streamingAssetsPath}/GenesisTrees.bin");

                    Trees!.SetTerrainData(TerrainModel.MinParcel, TerrainModel.MaxParcel,
                        OccupancyMapData, OccupancyMapSize, OccupancyFloor);

                    Trees.Instantiate();

                    processReport?.SetProgress(1f);

                    IsTerrainShown = true;
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

        internal static Texture2D CreateOccupancyMap(NativeParallelHashSet<int2> ownedParcels, int2 minParcel,
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

        internal static int WriteInteriorChamferOnWhite(Texture2D r8, int2 minParcel, int2 maxParcel, int padding)
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

            if (maxPixelDistance == 1) // means on distance field applied
                return 255;

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

        internal static float GetParcelNoiseHeight(float x, float z, NativeArray<byte> occupancyMapData,
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

            float minValue = occupancyFloor / 255.0f; // 0.68

            if (occupancy <= minValue)
            {
                // Flat surface (occupied parcels and above minValue threshold)
                return 0f;
            }
            else
            {
                // Calculate normalized height first
                float normalizedHeight = (occupancy - minValue) / (1f - minValue);

                // the result from the heightmap should be equal to this function
                float noiseH = MountainsNoise.GetHeight(x, z);

                const float SATURATION_FACTOR = 20;
                float y = (normalizedHeight * MAX_HEIGHT) + (noiseH * saturate(normalizedHeight * SATURATION_FACTOR));

                // Ensure no negative heights
                return max(0f, y);
            }
        }

        public float GetHeight(float x, float z) =>
            GetParcelNoiseHeight(x, z, OccupancyMapData, OccupancyMapSize, ParcelSize, OccupancyFloor);

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
    }
}
