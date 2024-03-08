using DCL.Landscape.Config;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DCL.Landscape.Settings
{
    [Serializable]
    public class TerrainGenerationData : ScriptableObject
    {
        [Header("Terrain Settings")]
        public int terrainSize = 4800;
        public int chunkSize = 512;
        public float heightScaleNerf = 1;
        public int seed;
        public float terrainHoleEdgeSize;
        public float minHeight = 1f;
        public float pondDepth = 5;
        public NoiseDataBase terrainHeightNoise;

        [Header("Textures")]
        public Material terrainMaterial;
        public TerrainLayer[] terrainLayers;
        public List<NoiseData> layerNoise;

        [Header("Trees")]
        public LandscapeAsset[] treeAssets;

        [Header("Detail")]
        public DetailScatterMode detailScatterMode;
        public LandscapeAsset[] detailAssets;

        [Header("Cliffs")]
        public GameObject cliffSide;
        public GameObject cliffCorner;

        [Header("Water")]
        public GameObject ocean;

        [Header("Wind")]
        public GameObject wind;

        [Header("GrassRenderer")]
        public GameObject grassRenderer;
    }
}
