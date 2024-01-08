using Cysharp.Threading.Tasks;
using DCL.Landscape.Config;
using DCL.Landscape.Jobs;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.Rendering;
using Debug = UnityEngine.Debug;
using JobHandle = Unity.Jobs.JobHandle;
using Random = System.Random;

public class TerrainGenerator : MonoBehaviour
{
    public TextAsset ownedParcels;
    public bool liveUpdates;
    public bool holes;
    public int terrainSize = 4800;
    public int chunkSize = 512;
    public float maxHeight = 10f;
    public int terrainScale = 15;
    public int seed;
    private GameObject rootGo;
    public Vector2Int offset;
    public Material terrain;

    public TerrainLayer[] layers;
    public List<NoiseData> layerNoise;
    private Random random;

    private Dictionary<NoiseData, NativeArray<float2>> octaves = new ();

    private void Start()
    {
        GenerateTerrainGrid();
    }

    [ContextMenu("Generate")]
    public void GenerateTerrainGrid()
    {
        random = new Random(seed);

        if (octaves != null)
        {
            foreach (KeyValuePair<NoiseData, NativeArray<float2>> keyValuePair in octaves) { keyValuePair.Value.Dispose(); }
        }

        octaves = new Dictionary<NoiseData, NativeArray<float2>>();

        foreach (NoiseData noise in layerNoise)
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
            rootGo = GameObject.Find("Generated Terrain");
            if (rootGo != null) DestroyImmediate(rootGo);
            rootGo = new GameObject("Generated Terrain");
            rootGo.transform.position = new Vector3(-terrainSize / 2f, 0, -terrainSize / 2f);

            var watch = new Stopwatch();
            watch.Start();
            Dictionary<Vector2Int, TerrainData> terrainDatas = new ();

            for (var z = 0; z < terrainSize; z += chunkSize)
            for (var x = 0; x < terrainSize; x += chunkSize)
                terrainDatas.Add(new Vector2Int(x, z), GenerateTerrainData(x, z));

            watch.Stop();
            Debug.Log($"Parallel task completed in {watch.Elapsed.Seconds:F2}s");

            if (holes)
            {
                List<Vector2Int> ownedParcelList = new ();
                Parse(ownedParcels, ownedParcelList);
                DigHoles(ownedParcelList, terrainDatas);
            }

            watch.Start();
            GenerateChunks(terrainDatas);

            watch.Stop();
            Debug.Log($"Main Thread task completed in {watch.Elapsed.Seconds:F2}s");
        }
        catch (Exception e) { Debug.LogException(e); }
    }

    private void GenerateChunks(Dictionary<Vector2Int, TerrainData> terrainDatas)
    {
        var index = 0;
        var terrains = new List<Terrain>();

        for (var z = 0; z < terrainSize; z += chunkSize)
        for (var x = 0; x < terrainSize; x += chunkSize)
        {
            TerrainData terrainData = terrainDatas[new Vector2Int(x, z)];
            terrains.Add(GenerateTerrainChunk(x, z, terrainData, terrain));
            index++;
        }
    }

    private void DigHoles(List<Vector2Int> ownedParcelList, Dictionary<Vector2Int, TerrainData> terrainDatas)
    {
        Debug.Log($"Setting {ownedParcelList.Count} holes");

        var failedHoles = 0;
        var goodHoles = 0;

        var parcelSizeHole = new bool[16, 16];

        for (var i = 0; i < 16; i++)
        for (var j = 0; j < 16; j++)
            parcelSizeHole[i, j] = false;

        foreach (Vector2Int ownedParcel in ownedParcelList)
        {
            int parcelGlobalX = (150 + ownedParcel.x) * 16;
            int parcelGlobalY = (150 + ownedParcel.y) * 16;

            // Calculate the terrain chunk index for the parcel
            int chunkX = Mathf.FloorToInt((float)parcelGlobalX / chunkSize);
            int chunkY = Mathf.FloorToInt((float)parcelGlobalY / chunkSize);

            // Calculate the position within the terrain chunk
            int localX = parcelGlobalX - (chunkX * chunkSize);
            int localY = parcelGlobalY - (chunkY * chunkSize);

            try
            {
                TerrainData terrainData = terrainDatas[new Vector2Int(chunkX * chunkSize, chunkY * chunkSize)];
                terrainData.SetHoles(localX, localY, parcelSizeHole);
                goodHoles++;
            }
            catch (Exception e) { failedHoles++; }
        }

        Debug.Log($"Holes digged {goodHoles}");

        if (failedHoles > 0) { Debug.LogError($"Failed to set {failedHoles} holes"); }
    }

    private static void Parse(TextAsset textAsset, List<Vector2Int> list)
    {
        string[] lines = textAsset.text.Split('\n');

        foreach (string line in lines)
        {
            string[] coordinates = line.Trim().Split(',');

            if (coordinates.Length == 2 && int.TryParse(coordinates[0], out int x) && int.TryParse(coordinates[1], out int y)) { list.Add(new Vector2Int(x, y)); }
            else
                Debug.LogWarning("Invalid line: " + line);
        }
    }

    private Terrain GenerateTerrainChunk(int offsetX, int offsetZ, TerrainData terrainData, Material material)
    {
        GameObject terrainObject = Terrain.CreateTerrainGameObject(terrainData);
        Terrain terrain = terrainObject.GetComponent<Terrain>();
        terrain.shadowCastingMode = ShadowCastingMode.Off;
        terrain.materialTemplate = material;
        terrainObject.transform.position = new Vector3(offsetX, 0, offsetZ);
        terrainObject.transform.SetParent(rootGo.transform, false);
        return terrain;

        // Set up neighbors
        //SetTerrainNeighbors(terrain, offsetX, offsetZ);
    }

    private TerrainData GenerateTerrainData(int offsetX, int offsetZ)
    {
        var terrainData = new TerrainData
        {
            heightmapResolution = chunkSize,
            alphamapResolution = chunkSize,
            size = new Vector3(chunkSize, maxHeight, chunkSize),
            terrainLayers = layers,
            enableHolesTextureCompression = false,
        };

        int chunkSizePlus = chunkSize + 1;
        var heights = new NativeArray<float>(chunkSizePlus * chunkSizePlus, Allocator.TempJob);

        var modifyJob = new ModifyTerrainJob
        {
            heights = heights,
            terrainWidth = chunkSizePlus,
            offsetX = offsetX,
            offsetZ = offsetZ,
            terrainScale = terrainScale,
        };

        JobHandle jobHandle = modifyJob.Schedule(heights.Length, 64);
        jobHandle.Complete();
        float[,] heightArray = ConvertTo2DArray(heights, chunkSizePlus, chunkSizePlus);
        terrainData.SetHeights(0, 0, heightArray);
        heights.Dispose();

        // calculate texture layers
        for (var i = 0; i < layerNoise.Count; i++)
        {
            NoiseData noiseData = layerNoise[i];
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

        return terrainData;
    }

    // Convert flat NativeArray to a 2D array
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
        var result = new float[width, height, layers.Length];

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
        public NativeArray<float> heights;
        public int terrainWidth;
        public int offsetX;
        public int offsetZ;
        public float terrainScale;

        public void Execute(int index)
        {
            int x = index % terrainWidth;
            int z = index / terrainWidth;

            float worldX = (x + offsetX) * 1f / terrainScale;
            float worldZ = (z + offsetZ) * 1f / terrainScale;

            heights[index] = Mathf.PerlinNoise(worldX, worldZ);
        }
    }
}
