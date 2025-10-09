using DCL.Diagnostics;
using DCL.Landscape.Jobs;
using DCL.Landscape.NoiseGeneration;
using DCL.Landscape.Settings;
using DCL.Landscape.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using Unity.Collections;
using Unity.Mathematics;
using UnityEditor;
using UnityEngine;
using static Unity.Mathematics.math;

namespace DCL.Landscape.Config.Editor
{
    public sealed class TreeDataGenerator : ScriptableWizard
    {
        [field: SerializeField] private TerrainGenerationData? terrainData { get; set; }
        [field: SerializeField] private int2 minParcel { get; set; } = int2(-152, -152);
        [field: SerializeField] private int2 maxParcel { get; set; } = int2(165, 160);
        [field: SerializeField] private string treeFilePath { get; set; } = "Trees.bin";

        [MenuItem("Decentraland/Generate Tree Data")]
        private static void OnMenuItem() =>
            DisplayWizard<TreeDataGenerator>("Generate Tree Data", "Generate");

        private async void OnWizardCreate()
        {
            NativeList<int2> emptyParcels = default;
            NativeParallelHashMap<int2, int> emptyParcelsData = default;
            NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData = default;
            NativeHashSet<int2> ownedParcels = default;

            try
            {
                if (terrainData == null)
                    return;

                int terrainSize = AdjustTerrainSize();
                ownedParcels = new NativeHashSet<int2>(0, Allocator.TempJob);

                TerrainGenerationUtils.ExtractEmptyParcels(minParcel, maxParcel, ref emptyParcels,
                    ref ownedParcels);

                TerrainGenerationUtils.SetupEmptyParcelsJobs(ref emptyParcelsData,
                                           ref emptyParcelsNeighborData, emptyParcels.AsArray(),
                                           ref ownedParcels, minParcel, maxParcel,
                                           terrainData.heightScaleNerf)
                                      .Complete();

                var terrainChunkDataGenerator = new TerrainChunkDataGenerator(null,
                    new TimeProfiler(false), terrainData, ReportCategory.LANDSCAPE);

                terrainChunkDataGenerator.Prepare(terrainData.seed, terrainData.parcelSize,
                    ref emptyParcelsData, ref emptyParcelsNeighborData, new NoiseGeneratorCache());

                int sizeInUnits = terrainSize * terrainData.parcelSize;
                var treeInstances = new List<TreeInstance>();

                await terrainChunkDataGenerator.SetTreesAsync(minParcel, sizeInUnits, treeInstances, 1,
                    CancellationToken.None, false);

                var writer = new TreeInstanceWriter(terrainData.parcelSize, terrainData.treeAssets);
                writer.AddChunk(minParcel, sizeInUnits, treeInstances);

                await using var stream = new FileStream(
                    $"{Application.streamingAssetsPath}/{treeFilePath}", FileMode.Create,
                    FileAccess.Write);

                writer.Write(stream);
            }
            catch (Exception ex) { ReportHub.LogException(ex, ReportCategory.LANDSCAPE); }
            finally
            {
                if (emptyParcels.IsCreated)
                    emptyParcels.Dispose();

                if (emptyParcelsData.IsCreated)
                    emptyParcelsData.Dispose();

                if (emptyParcelsNeighborData.IsCreated)
                    emptyParcelsNeighborData.Dispose();

                if (ownedParcels.IsCreated)
                    ownedParcels.Dispose();
            }
        }

        private void OnWizardUpdate() =>
            isValid = terrainData != null;

        /// <summary>Called code assumes width == height.</summary>
        private int AdjustTerrainSize()
        {
            int2 halfSize = (maxParcel - minParcel + 1) / 2;
            int2 center = minParcel + halfSize;
            minParcel = center - halfSize;
            maxParcel = center + halfSize - 1;
            return cmax(halfSize) * 2;
        }
    }
}
