using DCL.Landscape.Config;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    public class TerrainGenerationData : ScriptableObject
    {
        [Header("Boundaries")]
        public Vector2Int parcelLimitX;
        public Vector2Int parcelLimitZ;

        [Header("Terrain Settings")]
        public int terrainSize = 4800;
        public int chunkSize = 512;
        public int terrainScale = 15;
        public float heightScaleNerf = 1;
        public int seed;

        [Header("Textures")]
        public Material terrainMaterial;
        public TerrainLayer[] terrainLayers;
        public List<NoiseData> layerNoise;

        [Header("Trees")]
        public LandscapeAsset[] treeAssets;

        [Header("Detail")]
        public DetailScatterMode detailScatterMode;
        public LandscapeAsset[] detailAssets;
    }
}
