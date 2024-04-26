using Cysharp.Threading.Tasks;
using DCL.Landscape.Jobs;
using StylizedGrass;
using System.Collections.Generic;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

namespace DCL.Landscape
{
    public static class TerrainGenerationUtils
    {
        public static async UniTask AddColorMapRendererAsync(Transform parent, IReadOnlyList<Terrain> terrains, TerrainFactory factory)
        {
            // we wait at least one frame so all the terrain chunks are properly rendered so we can render the color map
            await UniTask.Yield();

            (GrassColorMapRenderer colorMapRenderer, GrassColorMap grassColorMap) = factory.CreateColorMapRenderer(parent);

            foreach (Terrain terrain in terrains)
                colorMapRenderer.terrainObjects.Add(terrain.gameObject);

            colorMapRenderer.RecalculateBounds();

            grassColorMap.bounds.center = new Vector3(grassColorMap.bounds.center.x, 0, grassColorMap.bounds.center.z);

            colorMapRenderer.Render();
        }

        public static void ExtractEmptyParcels(TerrainModel terrainModel, ref NativeList<int2> emptyParcels, ref NativeParallelHashSet<int2> ownedParcels)
        {
            if (!emptyParcels.IsCreated)
                emptyParcels = new NativeList<int2>(Allocator.Persistent);

            for (int x = terrainModel.MinParcel.x; x <= terrainModel.MaxParcel.x; x++)
            for (int y = terrainModel.MinParcel.y; y <= terrainModel.MaxParcel.y; y++)
            {
                var currentParcel = new int2(x, y);

                if (!ownedParcels.Contains(currentParcel))
                    emptyParcels.Add(currentParcel);
            }
        }

        public static JobHandle SetupEmptyParcelsJobs(
            ref NativeParallelHashMap<int2, int> emptyParcelsData,
            ref NativeParallelHashMap<int2, EmptyParcelNeighborData> emptyParcelsNeighborData,
            in NativeArray<int2> emptyParcels,
            ref NativeParallelHashSet<int2> ownedParcels,
            int2 minParcel, int2 maxParcel,
            float heightScaleNerf)
        {
            emptyParcelsData = new NativeParallelHashMap<int2, int>(emptyParcels.Length, Allocator.Persistent);
            emptyParcelsNeighborData = new NativeParallelHashMap<int2, EmptyParcelNeighborData>(emptyParcels.Length, Allocator.Persistent);

            var job = new CalculateEmptyParcelBaseHeightJob(in emptyParcels, ownedParcels.AsReadOnly(), emptyParcelsData.AsParallelWriter(),
                heightScaleNerf, minParcel, maxParcel);

            JobHandle handle = job.Schedule(emptyParcels.Length, 32);

            var job2 = new CalculateEmptyParcelNeighbourHeights(in emptyParcels, in ownedParcels, emptyParcelsNeighborData.AsParallelWriter(),
                emptyParcelsData.AsReadOnly(), minParcel, maxParcel);

            return job2.Schedule(emptyParcels.Length, 32, handle);
        }

        /// <summary>
        ///     Here we convert the result of the noise generation of the terrain texture layers
        /// </summary>
        public static float[,,] GenerateAlphaMaps(this NativeArray<float>[] textureResults, int width, int height, int terrainLayersAmount)
        {
            var result = new float[width, height, terrainLayersAmount];

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

        // Convert flat NativeArray to a 2D array (is there another way?)
        public static float[,] To2DArray(this NativeArray<float> array, int width, int height)
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
